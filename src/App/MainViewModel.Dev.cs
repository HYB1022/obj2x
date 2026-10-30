using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Input;

namespace Obj2x.UI {

// 개발자 옵션 (HyperOS처럼 정보 탭의 '버전'을 7번 누르면 메뉴가 생긴다)
public partial class MainViewModel {
    int versionTaps; DateTime lastTap;
    public ICommand VersionTapCmd { get; private set; }
    public ICommand BugReportCmd { get; private set; }
    public ICommand DevCheckCmd { get; private set; }
    public ICommand ReinstallCmd { get; private set; }
    public ICommand SelfTestCmd { get; private set; }
    public ICommand LangReloadCmd { get; private set; }
    public ICommand OpenIniCmd { get; private set; }
    public ICommand WelcomeAgainCmd { get; private set; }
    static readonly double[] AnimSteps = new double[] { 0, 0.5, 1, 1.5, 2, 5, 10 };

    void InitDev() {
        Motion.Scale = S.DevMode ? S.DevAnim : 1;
        VersionTapCmd = new Cmd(delegate { TapVersion(); });
        BugReportCmd = new Cmd(delegate { Updater.OpenUrl(AppInfo.RepoUrl + "/issues/new"); });
        DevCheckCmd = new Cmd(delegate { StartUpdateCheck(true); }, delegate(object p) { return S.DevMode; });
        ReinstallCmd = new Cmd(delegate { Reinstall(); }, delegate(object p) { return S.DevMode && !updInstalling; });
        SelfTestCmd = new Cmd(delegate { RunSelfTest(); }, delegate(object p) { return S.DevMode && !selfTesting; });
        LangReloadCmd = new Cmd(delegate {
            L.Load(); languages = null; Loc.SetLang(S.Lang); ui.ApplyLook();
            Raise("Languages"); Raise("Lang");
            ui.Toast(L.T("toast_lang_reloaded", L.Languages.Length));
        }, delegate(object p) { return S.DevMode; });
        OpenIniCmd = new Cmd(delegate { S.Save(); try { Process.Start("notepad.exe", "\"" + S.Path + "\""); } catch (Exception ex) { ui.Msg(ex.Message); } }, delegate(object p) { return S.DevMode; });
        WelcomeAgainCmd = new Cmd(delegate { S.Agreed = false; S.Save(); ui.Toast(L.T("toast_welcome")); }, delegate(object p) { return S.DevMode; });
    }

    // HyperOS/안드로이드처럼: 3번째부터 남은 횟수를 알려 주고, 7번째에 켠다. 2초 넘게 쉬면 처음부터.
    void TapVersion() {
        DateTime now = DateTime.Now;
        if ((now - lastTap).TotalSeconds > 2) versionTaps = 0;
        lastTap = now;
        if (S.DevMode) { ui.Toast(L.T("toast_dev_already")); return; }
        versionTaps++;
        int left = 7 - versionTaps;
        if (left <= 0) { versionTaps = 0; DevMode = true; ui.Toast(L.T("toast_dev_on")); }
        else if (versionTaps >= 3) ui.Toast(L.T("toast_dev_steps", left));
    }

    // 맨 위 '개발자 옵션' 스위치. 끄면 메뉴가 사라지고 정보 탭으로 돌아간다.
    public bool DevMode {
        get { return S.DevMode; }
        set {
            if (value == S.DevMode) return;
            S.DevMode = value; S.Save();
            Motion.Scale = value ? S.DevAnim : 1;
            Raise("DevMode", "DevFps", "DevBounds");
            CommandManager.InvalidateRequerySuggested();
            if (!value && Page == "dev") Page = "about";
        }
    }
    public bool DevFps { get { return S.DevMode && S.DevFps; } set { S.DevFps = value; S.Save(); Raise("DevFps"); } }
    public bool DevBounds { get { return S.DevMode && S.DevBounds; } set { S.DevBounds = value; S.Save(); Raise("DevBounds"); } }
    public bool DevVerbose { get { return S.DevVerbose; } set { S.DevVerbose = value; S.Save(); Raise("DevVerbose"); } }

    // 애니메이션 배율 (HyperOS의 '창 애니메이션 배율'처럼 고르는 목록)
    List<TrItem> animChoices;
    public List<TrItem> AnimChoices {
        get {
            if (animChoices == null) {
                animChoices = new List<TrItem>();
                foreach (double a in AnimSteps) {
                    string code = a.ToString(CultureInfo.InvariantCulture);
                    animChoices.Add(new TrItem(code, a == 0 ? (Func<string>)delegate { return L.T("anim_off"); } : delegate { return L.T("anim_x", code); }));
                }
            }
            return animChoices;
        }
    }
    public string DevAnim {
        get { return S.DevAnim.ToString(CultureInfo.InvariantCulture); }
        set {
            double a;
            if (value == null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out a) || a == S.DevAnim) return;
            S.DevAnim = a; S.Save();
            if (S.DevMode) Motion.Scale = a;
            Raise("DevAnim");
        }
    }

    // ---- 업데이트
    public string DevCheckValue { get { return UpdValue; } }
    void Reinstall() {
        Thread th = new Thread(delegate() {
            UpdateInfo u = Updater.Latest();
            ui.Post(delegate {
                if (u == null) { ui.Toast(L.T("toast_upd_fail")); return; }
                // 바로 설치하지 않고 새 버전이 나온 것처럼 '업데이트' 표시만 띄운다. 설치는 정보 탭에서 직접 누른다.
                upd = u; updText = "upd_link_new"; RaiseUpdate();
                ui.Toast(L.T("toast_reinstall_ready", u.Version));
            });
        });
        th.IsBackground = true; th.Start();
    }

    // ---- 자체 점검: 같은 exe를 --selftest로 따로 실행하고 결과 파일을 읽는다 (지금 창은 그대로)
    bool selfTesting;
    string selfTestText;
    public string SelfTestValue { get { return selfTesting ? L.T("dev_selftest_running") : (selfTestText ?? L.T("dev_selftest_sub")); } }
    void RunSelfTest() {
        if (selfTesting) return;
        selfTesting = true; Raise("SelfTestValue"); CommandManager.InvalidateRequerySuggested();
        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string dir = Path.Combine(Path.GetTempPath(), "obj2x_devtest");
        Thread th = new Thread(delegate() {
            int pass = 0, fail = 0; string err = null;
            try {
                Directory.CreateDirectory(dir);
                // 자체 점검은 결과를 exe 폴더에 쓰므로 임시 폴더에 exe와 글꼴을 복사해서 돌린다
                string copy = Path.Combine(dir, "obj2x.exe");
                File.Copy(exe, copy, true);
                string fonts = Path.Combine(Here, "fonts");
                if (Directory.Exists(fonts)) {
                    Directory.CreateDirectory(Path.Combine(dir, "fonts"));
                    foreach (string f in Directory.GetFiles(fonts)) File.Copy(f, Path.Combine(dir, "fonts", Path.GetFileName(f)), true);
                }
                ProcessStartInfo psi = new ProcessStartInfo(copy, "--selftest"); psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.WorkingDirectory = dir;
                using (Process p = Process.Start(psi)) p.WaitForExit(120000);
                foreach (string line in File.ReadAllLines(Path.Combine(dir, "obj2x_selftest.txt"))) {
                    if (line.StartsWith("PASS")) pass++;
                    else if (line.StartsWith("FAIL")) fail++;
                }
            } catch (Exception ex) { err = ex.Message; }
            ui.Post(delegate {
                selfTesting = false;
                selfTestText = err ?? L.T("dev_selftest_result", pass, fail);
                Raise("SelfTestValue"); CommandManager.InvalidateRequerySuggested();
                ui.Toast(selfTestText);
            });
        });
        th.IsBackground = true; th.Start();
    }

    // ---- 앱 정보 (읽기 전용)
    public string InfoRuntime { get { return Environment.Version + " · " + (Environment.Is64BitProcess ? "x64" : "x86"); } }
    public string InfoRender {
        get {
            int tier = System.Windows.Media.RenderCapability.Tier >> 16;
            return tier > 0 ? L.T("dev_render_hw", tier) : L.T("dev_render_sw");
        }
    }
    public string InfoScale { get { return ui.ScaleText; } }
    public string InfoPath { get { return Here.TrimEnd('\\'); } }
    public string InfoIni { get { return S.Path ?? ""; } }
}
}
