using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace Obj2x.UI {

public partial class MainViewModel {
    readonly StringBuilder logText = new StringBuilder();
    readonly StringBuilder logPending = new StringBuilder();   // 아직 화면에 안 붙인 줄 (모아서 한 번에)
    bool logFlushQueued;
    public ObservableCollection<ResItem> Results { get; private set; }
    public ICommand StartWizardCmd { get; private set; }
    public ICommand StopCmd { get; private set; }
    public ICommand RetryCmd { get; private set; }
    public ICommand ClearCmd { get; private set; }
    public ICommand SaveLogCmd { get; private set; }
    public ICommand TabCmd { get; private set; }
    public ICommand SetPathsCmd { get; private set; }

    void InitDashboard() {
        Results = new ObservableCollection<ResItem>();
        StartWizardCmd = new Cmd(delegate { Page = "wizard"; });
        StopCmd = new Cmd(delegate(object p) { StopConversion(); }, delegate(object p) { return running && !stopping; });
        RetryCmd = new Cmd(delegate { OnRetry(); });
        ClearCmd = new Cmd(delegate { ClearLog(); Results.Clear(); });
        SaveLogCmd = new Cmd(delegate { SaveLog(); });
        TabCmd = new Cmd(delegate(object p) { Tab = (string)p; });
        SetPathsCmd = new Cmd(delegate { Page = "advanced"; ui.ScrollToEnd("advanced"); });
    }

    // 상태 카드 아래 줄 / 진행 막대 아래 줄: 문구 키면 언어를 바꿀 때 다시 번역한다
    string lastKey = "dash_no_job", lastText = "", statKey = "status_ready", statText = "";
    public string LastText { get { return lastKey != null ? L.T(lastKey) : lastText; } }
    public string StatText { get { return statKey != null ? L.T(statKey) : statText; } }
    void SetStat(string key, string text) { statKey = key; statText = text ?? ""; Raise("StatText"); }

    double progress;
    public double Progress { get { return progress; } set { Set(ref progress, value); } }

    string tab = "log";
    public string Tab { get { return tab; } set { if (Set(ref tab, value)) { Raise("LogTab", "ResTab", "LogVisible", "ResVisible"); } } }
    public string LogTab { get { return tab == "res" ? "off" : "on"; } }
    public string ResTab { get { return tab == "res" ? "on" : "off"; } }
    public bool LogVisible { get { return tab != "res"; } }
    public bool ResVisible { get { return tab == "res"; } }

    public bool StopVisible { get { return running; } }
    public bool RetryVisible { get { return !running && E.FailCount > 0; } }

    void Say(string t) {
        if (t == null) return;
        logText.Append(t).Append("\r\n");
        logPending.Append(t).Append("\r\n");
        if (t.Length > 0) { lastKey = null; lastText = t.Substring(0, Math.Min(90, t.Length)); }
        if (!logFlushQueued) { logFlushQueued = true; ui.Post(FlushLog); }
    }
    void FlushLog() {
        logFlushQueued = false;
        if (logPending.Length > 0 && LogAppended != null) LogAppended(logPending.ToString());
        logPending.Length = 0;
        Raise("LastText");
    }
    void ClearLog() { logText.Length = 0; logPending.Length = 0; if (LogCleared != null) LogCleared(); }
    public string LogContent { get { return logText.ToString(); } }

    void SaveLog() {
        string f = ui.SaveFile("BusDepot_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "Text File (*.txt)|*.txt");
        if (f == null) return;
        File.WriteAllText(f, logText.ToString(), new UTF8Encoding(true));
        ui.Msg(L.T("m_log_saved"));
    }

    // ------------------------------------------------------------ 엔진 연결
    void WireEngine() {
        E.OnSay = delegate(string t) { ui.Post(delegate { Say(t); }); };
        E.OnProgress = delegate(double f) { ui.Post(delegate { Progress = f; }); };
        E.OnStat = delegate(string t) { ui.Post(delegate { SetStat(null, t); }); };
        E.OnRow = delegate(ResRow r) {
            ui.Post(delegate {
                ResItem it = new ResItem(); it.Name = r.Name; it.V = r.V; it.T = r.T; it.M = r.M; it.Sec = r.Sec; it.Status = r.Status; it.Kind = r.Kind;
                Results.Add(it);
            });
        };
        E.OnFailedChanged = delegate { ui.Post(delegate { Raise("RetryVisible"); }); };
        E.OnFinish = delegate(bool stopped) {
            ui.Post(delegate {
                stopping = false;
                Running = false;
                if (stopped) SetStat("status_stopped", null);
                // 실패한 것이 있으면 결과 표를 보여 주고 '실패한 것만 다시'를 알려 준다
                else if (E.FailCount > 0) { Tab = "res"; ui.Toast(L.T("toast_failed", E.FailCount)); }
                ui.Finished(!stopped && E.FailCount > 0);
            });
        };
    }

    // ------------------------------------------------------------ 변환 시작/중지/재시도
    public void StartConversion() {
        if (running) return;
        RunOptions o = Conversion.BuildOptions(S, wizMode, PyPath);
        string err, note;
        List<ObjItem> objs = Conversion.Prepare(S, o, out err, out note);
        if (objs == null) { ui.Msg(L.T(err)); return; }
        // 변환 전 검사: mtl에 적혀 있는데 없는 텍스처가 있으면 먼저 알려 주고 계속할지 묻는다
        List<string> miss = Omsi.MissingTextures(objs);
        if (miss.Count > 0 && !SkipTexCheck) {
            List<string> show = miss.GetRange(0, Math.Min(10, miss.Count));
            if (miss.Count > show.Count) show.Add(L.T("tex_more", miss.Count - show.Count));
            ui.Confirm(L.T("tex_missing_title", miss.Count), L.T("tex_missing_body") + "\n\n" + string.Join("\n", show.ToArray()), L.T("btn_continue"),
                delegate(bool ok) { if (ok) Begin(o, objs, note, miss); });
            return;
        }
        Begin(o, objs, note, miss);
    }
    public bool SkipTexCheck;   // 개발용(--readme-shots)

    void Begin(RunOptions o, List<ObjItem> objs, string note, List<string> miss) {
        if (running) return;
        S.DoConv = (wizMode == "std"); S.Fast = (wizMode == "fast");
        S.AddRecent(o.In); S.Save();
        RecentRefresh();
        ClearLog();
        if (note != null) Say(note);
        foreach (string m in miss) Say(L.T("m_tex_missing", m));
        Page = "dashboard";
        Results.Clear();
        Progress = 0;
        Tab = wizMode == "fast" ? "res" : "log";
        Running = true;
        E.Start(o, objs);
    }

    void StopConversion() {
        if (!running) return;
        stopping = true;
        CommandManager.InvalidateRequerySuggested();
        SetStat("m_stopping", null);
        E.Stop();
    }

    void OnRetry() {
        if (E.FailCount == 0 || running) return;
        string mode = E.LastMode;
        if (mode != "fast" && E.FailedObjs.Count > 0 && !Conversion.BlenderOk(S)) { ui.Msg(L.T("msg_err_bl")); return; }
        if (mode == "std" && !Conversion.ConvOk(S)) { ui.Msg(L.T("msg_err_conv")); return; }
        S.Save();
        Results.Clear();
        Running = true;
        Progress = 0;
        Tab = mode == "fast" ? "res" : "log";
        Say(L.T("m_retry_start", E.FailCount));
        E.StartRetry(Conversion.BuildOptions(S, mode, PyPath));
    }

    // ------------------------------------------------------------ 끌어다 놓기 (창이나 exe 위에 폴더/파일)
    // obj 폴더(또는 obj 파일) → 마법사 경로 단계, o3d 폴더/파일을 오브젝트 만들기 화면에 놓으면 그 화면의 모델 폴더
    public void DropPath(string path) {
        if (string.IsNullOrEmpty(path)) return;
        string dir = Directory.Exists(path) ? path : (File.Exists(path) ? Path.GetDirectoryName(path) : null);
        if (dir == null) return;
        string ext = File.Exists(path) ? Path.GetExtension(path).ToLowerInvariant() : "";
        // 맵: 맵 폴더, global.cfg, tile_*.map / HOF
        if (Directory.Exists(path) && File.Exists(Path.Combine(path, "global.cfg")) || ext == ".map" || File.Exists(path) && Path.GetFileName(path).ToLowerInvariant() == "global.cfg") { Page = "map"; LoadMap(path); return; }
        if (ext == ".hof") { Page = "map"; LoadHof(path); return; }
        if (ext == ".sco" && page != "bus") { Page = "object"; LoadSco(path); return; }
        if (ext == ".sli") { Page = "spline"; LoadSli(path); return; }
        if (ext == ".cfg" || ext == ".sco" || ext == ".bus" || ext == ".ovh") { Page = "bus"; LoadModel(path); return; }
        if (page == "object" || ext == ".o3d") { Page = "object"; ObjModels = dir; ui.Toast(L.T("toast_drop_obj")); return; }
        if (page == "spline" && File.Exists(path) && IsImage(ext)) { DropTexture(path); return; }
        if (running) { ui.Toast(L.T("toast_drop_busy")); return; }
        Page = "wizard";
        InPath = dir;
        WizStep = 2;
        ui.Toast(L.T("toast_drop"));
    }
    static bool IsImage(string ext) { return ext == ".bmp" || ext == ".tga" || ext == ".dds" || ext == ".png" || ext == ".jpg"; }

    // 개발용(--readme-shots): 정해진 방식으로 바로 변환을 시작한다
    public void ShotRun(string mode) { wizMode = mode; StartConversion(); }
}
}
