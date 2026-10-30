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
        SetPathsCmd = new Cmd(delegate { Page = "advanced"; });
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
    public bool RetryVisible { get { return !running && E.Failed.Count > 0; } }

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
        string f = ui.SaveFile("obj2x_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt", "Text File (*.txt)|*.txt");
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
        S.DoConv = (wizMode == "std"); S.Fast = (wizMode == "fast");
        S.AddRecent(o.In); S.Save();
        RecentRefresh();
        ClearLog();
        if (note != null) Say(note);
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
        if (E.Failed.Count == 0 || running) return;
        if (!Conversion.ConvOk(S)) { ui.Msg(L.T("msg_err_conv")); return; }
        S.Save();
        Running = true;
        Progress = 0;
        Tab = "log";
        E.StartRetry(Conversion.BuildOptions(S, "std", PyPath));
    }

    // 개발용(--readme-shots): 정해진 방식으로 바로 변환을 시작한다
    public void ShotRun(string mode) { wizMode = mode; StartConversion(); }
}
}
