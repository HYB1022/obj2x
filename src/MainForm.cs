using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Obj2x {

public partial class MainForm : Form {
    public Settings S;
    public Engine E = new Engine();
    public string Here;
    public string PyPath;
    string curPage = "dashboard";
    bool running;
    bool building = true;

    // 셸
    StatusPanel pnlStatus; Dot lblStatusDot; Lbl lblStatusText, lblVersion;
    SidePanel pnlSide; LogoPanel logo; Lbl lblTools;
    Dictionary<string, NavButton> navs = new Dictionary<string, NavButton>();
    Panel pnlMain;
    Dictionary<string, Panel> pages = new Dictionary<string, Panel>();

    // ------------------------------------------------------------ 작은 도우미
    public static T Put<T>(Control p, T c, int x, int y) where T : Control { c.Location = new Point(x, y); p.Controls.Add(c); return c; }
    public static T PutR<T>(Control p, T c, int rightMargin, int y) where T : Control {
        c.Tag = PinInfo.R(rightMargin);
        c.Location = new Point(Math.Max(0, p.Width - rightMargin - c.Width), y);
        p.Controls.Add(c); return c;
    }
    Lbl LK(Control p, string key, FR f, LR r, int x, int y) { Lbl l = Lbl.K(key, f, r); return Put(p, l, x, y); }
    Tx TB(Control p, int x, int y, int stretchRight) {
        Tx t = new Tx(); t.Size = new Size(200, 26); t.Tag = PinInfo.S(stretchRight);
        t.RefreshUi(); return Put(p, t, x, y);
    }

    public MainForm(Settings s) {
        S = s;
        Here = AppDomain.CurrentDomain.BaseDirectory;
        PyPath = Path.Combine(Here, "obj2x.py");
        L.Lang = S.Lang;
        Theme.Apply(S.Theme, S.Accent);
        Ui.BuildFonts(S.SysFont);

        Text = L.T("app_title");
        Icon = AppIcon.Get();
        ClientSize = new Size((int)(1040 * Ui.D), (int)(686 * Ui.D));
        MinimumSize = new Size((int)(920 * Ui.D), (int)(640 * Ui.D));
        StartPosition = FormStartPosition.CenterScreen;
        Font = Ui.F(FR.Main);
        BackColor = Theme.Bg; ForeColor = Theme.Text;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;

        BuildShell();
        BuildDashboard();
        BuildWizard();
        BuildAdvanced();
        BuildSettings();
        BuildAbout();
        WireEngine();

        building = false;
        SwitchPage("dashboard");
        RefreshEverything();

        HandleCreated += delegate { TitleBar(); };
        Resize += delegate {
            if (WindowState != lastState) { lastState = WindowState; UpdateZoom(); }
            LayoutDash(); LayoutWizard();
        };
        ResizeEnd += delegate { UpdateZoom(); };
        Shown += delegate { lastState = WindowState; UpdateZoom(); LayoutDash(); LayoutWizard(); Pin.Apply(pages[curPage]); };
        FormClosing += delegate { SaveAll(); E.Stop(); };
        WindowState = FormWindowState.Maximized;
    }

    // ------------------------------------------------------------ 화면 배율
    // 기준 크기(1040x686)보다 창이 크면 글꼴과 여백을 함께 키운다. 윈도우 배율(DPI)은 따로 반영하고, 추가 확대는 0.1 단위로 최대 1.3배.
    FormWindowState lastState = FormWindowState.Normal;
    public void UpdateZoom() {
        if (WindowState == FormWindowState.Minimized || pnlMain == null) return;
        Size cs = ClientSize;
        if (cs.Width < 200 || cs.Height < 200) return;
        // 윈도우 배율을 뺀 논리 크기로 비교하고, 남는 공간만큼만 조금 키운다 (최대 1.3배)
        float lw = cs.Width / Ui.D, lh = cs.Height / Ui.D;
        float z = Math.Min(lw / 1040f, lh / 686f);
        z = (float)Math.Floor(z * 10 + 0.001) / 10f;
        z = Math.Max(1f, Math.Min(1.3f, z));
        SetZoom(z * Ui.D);
    }

    public void SetZoom(float z) {
        if (Math.Abs(z - Ui.Z) < 0.01f) return;
        float f = z / Ui.Z;
        SuspendLayout();
        foreach (Panel p in pages.Values) { if (p.AutoScroll) p.AutoScrollPosition = Point.Empty; }
        if (wizContent != null) wizContent.AutoScrollPosition = Point.Empty;
        SizeF sf = new SizeF(f, f);
        pnlSide.Scale(sf); pnlStatus.Scale(sf);
        foreach (Panel p in pages.Values) foreach (Control c in p.Controls) c.Scale(sf);
        Ui.Z = z;
        Ui.BuildFonts(S.SysFont);
        Font = Ui.F(FR.Main);
        Ui.RefreshAll(this);
        StyleBanner(); UpdateStatus(); RefreshDynamic();
        ResumeLayout(true);
        LayoutDash(); LayoutWizard();
        Pin.Apply(pages[curPage]); Pin.Apply(pnlStatus);
        if (recent != null) { recent.SetPaths(recent.Paths); cardRecent.Height = recent.Height + Ui.S(8); }
        pnlSide.Rescaled(navs[curPage]);
        Invalidate(true);
    }

    // ------------------------------------------------------------ 껍데기 (사이드바/상태바)
    void BuildShell() {
        pnlStatus = new StatusPanel(); pnlStatus.Dock = DockStyle.Bottom; pnlStatus.Height = 34;
        Controls.Add(pnlStatus);
        lblStatusText = new Lbl("", FR.Main, LR.Muted); Put(pnlStatus, lblStatusText, 38, 8);
        lblStatusDot = new Dot(); lblStatusDot.Beside = lblStatusText; pnlStatus.Controls.Add(lblStatusDot); lblStatusDot.Align();
        lblVersion = new Lbl("v" + Program.AppVersion, FR.Sub, LR.Muted);
        PutR(pnlStatus, lblVersion, 16, 8);

        pnlSide = new SidePanel(); pnlSide.Dock = DockStyle.Left; pnlSide.Width = 210;
        Controls.Add(pnlSide);
        logo = new LogoPanel(); Put(pnlSide, logo, 0, 0);
        AddNav("dashboard", "nav_dashboard", 68);
        AddNav("wizard", "nav_wizard", 114);
        lblTools = Lbl.K("nav_tools_hdr", FR.Tiny, LR.Muted); Put(pnlSide, lblTools, 22, 172);
        AddNav("advanced", "nav_advanced", 196);
        AddNav("settings", "nav_settings", 242);
        AddNav("about", "nav_info", 288);

        pnlMain = new Panel(); pnlMain.Dock = DockStyle.Fill; pnlMain.BackColor = Theme.Bg;
        Controls.Add(pnlMain); pnlMain.BringToFront();
        foreach (string n in new string[] { "dashboard", "wizard", "advanced", "settings", "about" }) {
            Page p = new Page(); p.Dock = DockStyle.Fill;
            p.AutoScroll = (n != "dashboard" && n != "wizard");
            p.Visible = (n == "dashboard");
            Page pgRef = p;
            p.Resize += delegate { Pin.Apply(pgRef); };
            pages[n] = p; pnlMain.Controls.Add(p);
        }
        pnlSide.BringToFront(); pnlStatus.SendToBack();
        pnlStatus.BringToFront();
        pnlMain.BringToFront();
    }

    void AddNav(string page, string key, int y) {
        NavButton b = new NavButton(); b.Page = page; b.Key = key;
        b.Location = new Point(10, y);
        b.Click += delegate { SwitchPage(page); };
        pnlSide.Controls.Add(b); navs[page] = b;
        b.RefreshUi();
    }

    public void SwitchPage(string name) {
        SwapOverlay sw = null;
        if (!building && !Anim.Off && !Anim.Mono && name != curPage && Visible && WindowState != FormWindowState.Minimized && pnlMain.Width > 10 && pnlMain.Height > 10) {
            try {
                Bitmap old = new Bitmap(pnlMain.Width, pnlMain.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(old)) g.CopyFromScreen(pnlMain.PointToScreen(Point.Empty), Point.Empty, pnlMain.Size);
                sw = new SwapOverlay(old); sw.Bounds = pnlMain.ClientRectangle;
                pnlMain.Controls.Add(sw); sw.BringToFront(); sw.Refresh();
            } catch (Exception) { sw = null; }
        }
        if (name == "wizard") { wizStep = 1; WizSync(); }
        curPage = name;
        foreach (KeyValuePair<string, Panel> kv in pages) kv.Value.Visible = (kv.Key == name);
        Pin.Apply(pages[name]);
        Pin.Apply(pnlStatus);
        foreach (KeyValuePair<string, NavButton> kv in navs) { kv.Value.Active = (kv.Key == name); kv.Value.Invalidate(); }
        pnlSide.MovePill(navs[name]);
        if (name == "dashboard") LayoutDash();
        if (name == "wizard") LayoutWizard();
        if (name == "about" && hero != null) hero.StartIntro();
        if (sw != null) {
            try {
                Control pg = pages[name];
                Bitmap nb = new Bitmap(pnlMain.Width, pnlMain.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                pg.DrawToBitmap(nb, new Rectangle(pg.Left, pg.Top, pg.Width, pg.Height));
                sw.Start(nb, null);
            } catch (Exception) { sw.Finish(); }
        }
    }

    void TitleBar() {
        try { if (Theme.Dark) W.DarkTitle(Handle); else W.LightTitle(Handle); } catch (Exception) { }
    }

    // ------------------------------------------------------------ 테마/언어 적용
    public void ApplyTheme() {
        Theme.Apply(S.Theme, S.Accent);
        BackColor = Theme.Bg; ForeColor = Theme.Text;
        pnlMain.BackColor = Theme.Bg;
        Ui.RefreshAll(this);
        StyleBanner();
        UpdateStatus();
        RefreshDynamic();
        TitleBar();
    }

    public void ApplyLanguage() {
        L.Lang = S.Lang;
        Text = L.T("app_title");
        Ui.RefreshAll(this);
        lv.RetextColumns();
        RefreshDynamic();
        UpdateStatus();
    }

    public void ApplyFont() {
        Ui.BuildFonts(S.SysFont);
        Font = Ui.F(FR.Main);
        Ui.RefreshAll(this);
        LayoutDash(); LayoutWizard();
    }

    void RefreshEverything() {
        Ui.RefreshAll(this);
        StyleBanner();
        UpdateStatus();
        RefreshDynamic();
        TitleBar();
    }

    public void SaveAll() { SyncSettingsFromUi(); S.Save(); }

    // ------------------------------------------------------------ 경로 검사 / 상태 표시
    public static string Clean(string p) {
        try { return Environment.ExpandEnvironmentVariables((p ?? "").Trim().Trim('"')); } catch (Exception) { return ""; }
    }
    public bool BlenderOk() {
        string p = Clean(S.Blender);
        return p.Length > 0 && File.Exists(p) && string.Equals(Path.GetFileName(p), "blender.exe", StringComparison.OrdinalIgnoreCase);
    }
    public bool ConvOk() {
        string p = Clean(S.Conv);
        return p.Length > 0 && File.Exists(p) && string.Equals(Path.GetFileName(p), "OmsiXConv.exe", StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateStatus() {
        bool ok = BlenderOk() && ConvOk();
        Color dot; string txt;
        if (running) { dot = Theme.Warn; txt = L.T("status_converting"); }
        else if (ok) { dot = Theme.Ok; txt = L.T("status_ready"); }
        else { dot = Theme.Warn; txt = L.T("status_blender_missing"); }
        DotState ds = running ? DotState.Busy : (ok ? DotState.Ok : DotState.Warn);
        lblStatusText.Text = txt; lblStatusDot.State = ds; dashDot.State = ds;
        // 상태줄 글자는 줄 높이의 가운데에
        lblStatusText.Top = Math.Max(0, (pnlStatus.Height - lblStatusText.Height) / 2);
        lblVersion.Top = Math.Max(0, (pnlStatus.Height - lblVersion.Height) / 2);
        lblStatusDot.Align(); dashDot.Align();
        dashText.Text = txt;
        UpdateBanner();
        WizAvailability();
    }
}
}
