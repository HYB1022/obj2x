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
    public string Lang { get { return S.Lang; } set { if (value == S.Lang) return; S.Lang = value; Loc.SetLang(value); S.Save(); } }
    public string ThemeName { get { return S.Theme; } set { if (value == S.Theme) return; S.Theme = value; ui.ApplyLook(); S.Save(); Raise("ThemeName"); } }
    public string AccentName { get { return S.Accent; } set { if (value == S.Accent) return; S.Accent = value; ui.ApplyLook(); S.Save(); Raise("AccentName"); } }
    public bool SysFont { get { return S.SysFont; } set { S.SysFont = value; ui.ApplyLook(); S.Save(); Raise("SysFont"); } }

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
            else if (!updChecking) { updText = "upd_link_checking"; RaiseUpdate(); StartUpdateCheck(); }
        });
        RepoCmd = new Cmd(delegate { Updater.OpenUrl(AppInfo.RepoUrl); });
        DisclaimerCmd = new Cmd(delegate { ui.Disclaimer(true, null); });
    }

    public bool UpdIsNew { get { return upd != null; } }
    public string UpdVersion { get { return upd != null ? upd.Version : ""; } }
    public bool UpdBusy { get { return updInstalling; } }
    public string UpdNavBadge { get { return upd != null ? "about_pill_update" : null; } }
    public string UpdValue {
        get {
            if (updText == "upd_link_new") return L.T("about_upd_new", UpdVersion);
            if (updText == "upd_link_latest") return L.T("upd_link_latest", AppInfo.Version);
            if (updText != null) return L.T(updText);
            return L.T("about_upd_idle");
        }
    }
    void RaiseUpdate() { Raise("UpdIsNew"); Raise("UpdVersion"); Raise("UpdBusy"); Raise("UpdNavBadge"); Raise("UpdValue"); }

    // GitHub 최신 릴리스 조회 (백그라운드). 새 버전이 있으면 창을 띄우지 않고 정보 탭의 업데이트 줄과 메뉴의 [업데이트] 표시로만 알린다.
    public void StartUpdateCheck() {
        if (updChecking) return;
        updChecking = true;
        Thread th = new Thread(delegate() {
            UpdateInfo u = Updater.Latest();
            ui.Post(delegate {
                updChecking = false;
                if (u == null) updText = "upd_link_fail";
                else if (Updater.IsNewer(u.Version, AppInfo.Version)) { upd = u; updText = "upd_link_new"; }
                else updText = "upd_link_latest";
                RaiseUpdate();
            });
        });
        th.IsBackground = true; th.Start();
    }

    // 개발용(--shots): 새 버전이 있는 것처럼 표시한다
    public void FakeUpdate(string ver) { upd = new UpdateInfo(); upd.Version = ver; updText = "upd_link_new"; RaiseUpdate(); }

    void Install() {
        if (updInstalling) return;
        if (running) { ui.Msg(L.T("upd_busy")); return; }
        UpdateInfo u = upd;
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
