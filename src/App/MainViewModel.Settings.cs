using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Input;

namespace Obj2x.UI {

public partial class MainViewModel {
    static readonly string[] CopNames = new string[] { "X-File: z up", "Separate Files", "Show Details of X", "Remove Specular", "Opt. Materials", "Opt. Vertices", "All Normals Up", "Use Submesh Local Pos. as Global Pos." };
    public List<CoptItem> Copts { get; private set; }
    public ICommand BrowseBlenderCmd { get; private set; }
    public ICommand BrowseConvCmd { get; private set; }
    public ICommand OpenBackupCmd { get; private set; }
    public ICommand LangFolderCmd { get; private set; }
    public ICommand CleanTempCmd { get; private set; }

    void InitSettings() {
        Copts = new List<CoptItem>();
        for (int i = 0; i < 8; i++) Copts.Add(new CoptItem(S, Settings.CoptKeys[i], CopNames[i]));
        BrowseBlenderCmd = new Cmd(delegate {
            string f = ui.PickFile(L.T("lbl_blender_path"), "blender.exe|blender.exe|Executable (*.exe)|*.exe", BlenderPath);
            if (f != null) BlenderPath = f;
        });
        BrowseConvCmd = new Cmd(delegate {
            string f = ui.PickFile(L.T("lbl_conv_path"), "OmsiXConv.exe|OmsiXConv.exe|Executable (*.exe)|*.exe", ConvPath);
            if (f != null) ConvPath = f;
        });
        // 번역용 틀(_template.json)을 만들고 lang 폴더를 연다
        LangFolderCmd = new Cmd(delegate {
            try { Process.Start(L.WriteTemplate()); } catch (Exception ex) { ui.Msg(ex.Message); }
        });
        OpenBackupCmd = new Cmd(delegate {
            try { if (!Directory.Exists(BackupPath)) Directory.CreateDirectory(BackupPath); Process.Start(BackupPath); } catch (Exception) { }
        });
        CleanTempCmd = new Cmd(delegate {
            foreach (string p in TempFiles()) { try { File.Delete(p); } catch (Exception) { } }
            Raise("TempText");
            ui.Msg(L.T("msg_temp_clean"));
        });
    }

    // ---- 고급
    public string ExOpt { get { return S.ExOpt; } set { S.ExOpt = value ?? ""; } }
    public string BlenderPath { get { return S.Blender; } set { S.Blender = (value ?? "").Trim(); Raise("BlenderPath"); RefreshStatus(); S.Save(); } }
    public string ConvPath { get { return S.Conv; } set { S.Conv = (value ?? "").Trim(); Raise("ConvPath"); RefreshStatus(); S.Save(); } }

    // ---- 설정: 모양
    // 한 번만 만든다: 언어를 바꿀 때 화면이 모든 속성을 다시 읽어도 드롭다운 목록이 새로 바뀌지 않게 (바뀌면 선택이 풀려 빈 값이 들어온다)
    List<LangItem> languages;
    public List<LangItem> Languages {
        get {
            if (languages == null) { languages = new List<LangItem>(); foreach (string[] l in L.Languages) languages.Add(new LangItem(l[0], l[1])); }
            return languages;
        }
    }
    public string Lang {
        get { return S.Lang; }
        set {
            if (value == null || value == S.Lang) return;
            bool known = false; foreach (string[] l in L.Languages) if (l[0] == value) known = true;
            if (!known) return;
            S.Lang = value; Loc.SetLang(value); ui.ApplyLook(); S.Save();
        }
    }
    public string ThemeName { get { return S.Theme; } set { if (value == S.Theme) return; S.Theme = value; ui.ApplyLook(); S.Save(); Raise("ThemeName"); } }
    public string AccentName { get { return S.Accent; } set { if (value == S.Accent) return; S.Accent = value; ui.ApplyLook(); S.Save(); Raise("AccentName"); } }
    // 글꼴 목록은 이름이 언어마다 달라서 언어를 바꿀 때 새로 만든다. 그때 드롭다운이 null을 보내도 무시한다.
    public List<LangItem> Fonts {
        get { return new List<LangItem> { new LangItem("nanum", L.T("font_nanum")), new LangItem("noto", L.T("font_noto")), new LangItem("system", L.T("font_system")) }; }
    }
    public string FontName {
        get { return S.Font; }
        set {
            if (value == null || value == S.Font || (value != "nanum" && value != "noto" && value != "system")) return;
            S.Font = value; ui.ApplyLook(); S.Save(); Raise("FontName");
        }
    }

    // ---- 설정: 동작
    public bool UpdCheck { get { return S.UpdCheck; } set { S.UpdCheck = value; S.Save(); Raise("UpdCheck"); } }
    public bool DelX { get { return S.DelX; } set { S.DelX = value; S.Save(); Raise("DelX"); } }

    // ---- 설정: 파일
    public string BackupPath { get { return Path.Combine(Here, "obj2x_backup"); } }
    List<string> TempFiles() { return new List<string> { Conversion.TempJobFile, Conversion.TempLogFile }; }
    public string TempText {
        get {
            long sz = 0;
            foreach (string p in TempFiles()) { try { if (File.Exists(p)) sz += new FileInfo(p).Length; } catch (Exception) { } }
            return L.T("lbl_temp_sub", Math.Round(sz / 1048576.0, 2).ToString("0.00") + " MB");
        }
    }

    // ------------------------------------------------------------ 업데이트
    UpdateInfo upd;                 // 새 버전이 있을 때만
    string updText;                 // 정보 탭 업데이트 줄 문구 키
    bool updChecking, updInstalling;
    public ICommand UpdateCmd { get; private set; }
    public ICommand RepoCmd { get; private set; }
    public ICommand DisclaimerCmd { get; private set; }

    void InitUpdate() {
        UpdateCmd = new Cmd(delegate {
            if (upd != null) Install();
            else if (!updChecking) StartUpdateCheck(false);
        });
        RepoCmd = new Cmd(delegate { Updater.OpenUrl(AppInfo.RepoUrl); });
        DisclaimerCmd = new Cmd(delegate { ui.Disclaimer(true, null); });
    }

    public bool UpdIsNew { get { return upd != null; } }
    public string UpdVersion { get { return upd != null ? upd.Version : ""; } }
    public bool UpdBusy { get { return updInstalling; } }
    public string UpdNavBadge { get { return upd != null ? "about_pill_update" : null; } }
    // 정보 탭 '업데이트' 줄 오른쪽: 버전 번호 없이 짧게. 새 버전이면 '업데이트' 알약이 올라오며 나타난다.
    public string UpdValue {
        get {
            if (updInstalling) return L.T("about_upd_installing");
            if (updText == "upd_link_new") return L.T("about_pill_update");
            if (updText == "upd_link_latest") return L.T("about_upd_latest");
            if (updText != null) return L.T(updText);
            return L.T("about_upd_idle");
        }
    }
    void RaiseUpdate() { Raise("UpdIsNew", "UpdVersion", "UpdBusy", "UpdNavBadge", "UpdValue", "DevCheckValue"); }

    // GitHub 최신 릴리스 조회 (백그라운드). 새 버전이 있으면 창을 띄우지 않고 정보 탭의 업데이트 줄과 메뉴의 [업데이트] 표시로만 알린다.
    public void StartUpdateCheck() { StartUpdateCheck(false); }
    // toast=true: 결과를 알림으로도 보여 준다 (개발자 옵션 '지금 업데이트 확인')
    public void StartUpdateCheck(bool toast) {
        if (updChecking) return;
        updChecking = true; updText = "upd_link_checking"; RaiseUpdate();
        Thread th = new Thread(delegate() {
            UpdateInfo u = Updater.Latest();
            ui.Post(delegate {
                updChecking = false;
                if (u == null) updText = "upd_link_fail";
                else if (Updater.IsNewer(u.Version, AppInfo.Version)) { upd = u; updText = "upd_link_new"; }
                else updText = "upd_link_latest";
                RaiseUpdate();
                if (toast) ui.Toast(u == null ? L.T("toast_upd_fail") : (upd != null ? L.T("about_pill_update") + " · v" + upd.Version : L.T("toast_upd_none")));
            });
        });
        th.IsBackground = true; th.Start();
    }

    // 개발용(--shots): 새 버전이 있는 것처럼 표시한다
    public void FakeUpdate(string ver) { upd = new UpdateInfo(); upd.Version = ver; updText = "upd_link_new"; RaiseUpdate(); }

    void Install() { if (upd != null) Install(upd); }
    void Install(UpdateInfo u) {
        if (updInstalling || u == null) return;
        if (running) { ui.Msg(L.T("upd_busy")); return; }
        if (!u.ZipUrl.StartsWith("https://")) { Updater.OpenUrl(u.PageUrl); return; }
        updInstalling = true; RaiseUpdate();
        Say(L.T("upd_dl", u.Version));
        S.Save();
        string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
        Thread th = new Thread(delegate() {
            string err = Updater.Install(u, Here, exe);
            ui.Post(delegate {
                if (err != null) {
                    updInstalling = false; RaiseUpdate();
                    ui.Msg(L.T("upd_error", err));
                    if (u.PageUrl.Length > 0) Updater.OpenUrl(u.PageUrl);
                    return;
                }
                Say(L.T("upd_installed"));
                try { Process.Start(exe); } catch (Exception) { }
                ui.Exit();
            });
        });
        th.IsBackground = true; th.Start();
    }
}
}
