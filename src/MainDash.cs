using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Obj2x {

public partial class MainForm {
    Card cardStatus, cardHist;
    Dot dashDot; Lbl dashText, dashLast;
    RButton btnWizard, btnStop, btnRetry;
    BannerPanel banner; Lbl bnTitle, bnText; RButton bnSet;
    Lbl histTitle; RButton btnClear, btnSave, tabLog, tabRes;
    SlimBar bar; Lbl lblStat; Tx log; ResList lv;
    string tab = "log";
    bool bannerOn;
    bool stopping;

    void BuildDashboard() {
        Panel pg = pages["dashboard"];
        cardStatus = new Card(); cardStatus.Size = new Size(780, 96); pg.Controls.Add(cardStatus);
        LK(cardStatus, "dash_status_title", FR.Bold, LR.Text, 20, 14);
        dashText = Put(cardStatus, new Lbl("", FR.Main, LR.Text), 38, 45);
        dashDot = new Dot(); dashDot.Beside = dashText; cardStatus.Controls.Add(dashDot); dashDot.Align();
        dashLast = Lbl.K("dash_no_job", FR.Sub, LR.Muted); Put(cardStatus, dashLast, 20, 68);

        btnWizard = RButton.K("btn_start_wizard", true, 36);
        PutR(cardStatus, btnWizard, 20, 30);
        btnWizard.Click += delegate { SwitchPage("wizard"); };
        btnRetry = RButton.K("btn_retry", false, 36);
        PutR(cardStatus, btnRetry, 20 + btnWizard.Width + 10, 30);
        btnRetry.Visible = false;
        btnRetry.Click += delegate { OnRetry(); };
        btnStop = RButton.K("btn_stop", false, 36);
        PutR(cardStatus, btnStop, 20 + btnWizard.Width + 10, 30);
        btnStop.Visible = false;
        btnStop.Click += delegate { StopConversion(); };

        // 경고 배너
        banner = new BannerPanel(); banner.Size = new Size(780, 72); banner.Visible = false;
        pg.Controls.Add(banner);
        bnTitle = new Lbl(); bnTitle.Key = "dash_banner_title"; bnTitle.FontRole = FR.H2; bnTitle.Role = LR.Banner;
        Put(banner, bnTitle, 68, 12);
        bnText = new Lbl(); bnText.FontRole = FR.Sub; bnText.Role = LR.Banner; bnText.AutoSize = false; bnText.Size = new Size(500, 32);
        Put(banner, bnText, 68, 38);
        bnSet = RButton.K("btn_set_blender", true, 36); bnSet.Banner = true;
        PutR(banner, bnSet, 20, 18);
        bnSet.Click += delegate { PickBlender(); };

        // 작업 내역
        cardHist = new Card(); pg.Controls.Add(cardHist);
        histTitle = LK(cardHist, "card_history_title", FR.H2, LR.Text, 20, 16);
        btnClear = RButton.K("btn_clear", false, 28); btnClear.Radius = 6;
        btnSave = RButton.K("btn_save", false, 28); btnSave.Radius = 6;
        tabLog = RButton.K("tab_log", true, 28); tabLog.Radius = 6;
        tabRes = RButton.K("tab_res", false, 28); tabRes.Radius = 6;
        foreach (RButton b in new RButton[] { tabRes, tabLog, btnSave, btnClear }) { cardHist.Controls.Add(b); }
        bar = new SlimBar(); Put(cardHist, bar, 20, 48);
        lblStat = Lbl.K("status_ready", FR.Sub, LR.Muted); Put(cardHist, lblStat, 20, 56);
        log = new Tx(); log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        log.RefreshUi(); Put(cardHist, log, 20, 80);
        lv = new ResList(); lv.Visible = false; lv.Init(); lv.RefreshUi();
        Put(cardHist, lv, 20, 80);

        btnClear.Click += delegate { log.Text = ""; lv.Items.Clear(); };
        btnSave.Click += delegate { SaveLog(); };
        tabLog.Click += delegate { ShowTab("log"); };
        tabRes.Click += delegate { ShowTab("res"); };
        pg.Resize += delegate { LayoutDash(); };
        LayoutDash();
    }

    // 대시보드 배치: 창 크기에 맞춰 직접 계산
    void LayoutDash() {
        if (cardStatus == null) return;
        Panel pg = pages["dashboard"];
        int pw = pg.ClientSize.Width, ph = pg.ClientSize.Height;
        if (pw < 100 || ph < 100) return;
        int m = Ui.S(16);
        cardStatus.SetBounds(m, m, pw - 2 * m, Ui.S(96));
        Pin.Apply(cardStatus);
        int y = cardStatus.Bottom + Ui.S(12);
        if (bannerOn) { banner.SetBounds(m, y, pw - 2 * m, Ui.S(72)); y += Ui.S(84); }
        cardHist.SetBounds(m, y, pw - 2 * m, Math.Max(Ui.S(200), ph - y - m));
        int hw = cardHist.Width, hh = cardHist.Height;
        int x = hw - Ui.S(20);
        foreach (RButton b in new RButton[] { tabRes, tabLog }) { x -= b.Width; b.Location = new Point(x, Ui.S(14)); x -= Ui.S(6); }
        x -= Ui.S(10);
        foreach (RButton b in new RButton[] { btnSave, btnClear }) { x -= b.Width; b.Location = new Point(x, Ui.S(14)); x -= Ui.S(6); }
        bar.SetBounds(Ui.S(20), Ui.S(48), hw - Ui.S(40), Ui.S(4));
        lblStat.Location = new Point(Ui.S(20), Ui.S(56));
        log.SetBounds(Ui.S(20), Ui.S(80), hw - Ui.S(40), hh - Ui.S(100));
        lv.SetBounds(Ui.S(20), Ui.S(80), hw - Ui.S(40), hh - Ui.S(100));
        Pin.Apply(banner);
        // 배너 내부
        int bw = Math.Max(Ui.S(180), banner.Width - Ui.S(68) - bnSet.Width - Ui.S(40));
        bnTitle.AutoSize = false; bnTitle.Size = new Size(bw, Ui.S(26));
        bnSet.Location = new Point(banner.Width - Ui.S(20) - bnSet.Width, (banner.Height - bnSet.Height) / 2);
        bnText.Width = bw;
    }

    void ShowTab(string t) {
        tab = t;
        lv.Visible = (t == "res"); log.Visible = (t != "res");
        tabRes.Primary = (t == "res"); tabLog.Primary = (t != "res");
        tabRes.Invalidate(); tabLog.Invalidate();
    }

    void SaveLog() {
        SaveFileDialog d = new SaveFileDialog();
        d.Filter = "Text File (*.txt)|*.txt";
        d.FileName = "obj2x_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        if (d.ShowDialog(this) == DialogResult.OK) {
            File.WriteAllText(d.FileName, log.Text, new System.Text.UTF8Encoding(true));
            Dialogs.Msg(this, L.T("m_log_saved"));
        }
    }

    // ------------------------------------------------------------ 배너
    void StyleBanner() {
        banner.BackColor = Theme.BnBg;
        bnTitle.ForeColor = Theme.BnText; bnText.ForeColor = Theme.BnText;
        bnSet.Invalidate(); banner.Invalidate(true);
    }

    void UpdateBanner() {
        List<string> missing = new List<string>();
        if (!BlenderOk()) missing.Add("Blender (blender.exe)");
        if (!ConvOk()) missing.Add("OmsiXConv (OmsiXConv.exe)");
        bool show = missing.Count > 0;
        bnText.Text = show ? L.T("m_path_banner", string.Join(", ", missing.ToArray())) : L.T("dash_blender_warning");
        bannerOn = show; banner.Visible = show; LayoutDash();
    }

    void PickBlender() {
        OpenFileDialog d = new OpenFileDialog();
        d.Title = L.T("lbl_blender_path");
        d.Filter = "blender.exe|blender.exe|Executable (*.exe)|*.exe";
        d.CheckFileExists = true;
        if (d.ShowDialog(this) == DialogResult.OK) {
            S.Blender = d.FileName;
            tBlender.Text = d.FileName;
            S.Save(); UpdateStatus();
        }
    }

    // ------------------------------------------------------------ 로그/결과
    void Say(string t) {
        if (t == null) return;
        log.AppendText(t + "\r\n");
        if (t.Length > 0) { dashLast.Key = null; dashLast.Text = t.Substring(0, Math.Min(90, t.Length)); }
    }

    void AddRow(ResRow r) {
        ListViewItem it = new ListViewItem(r.Name);
        it.SubItems.Add(r.V); it.SubItems.Add(r.T); it.SubItems.Add(r.M); it.SubItems.Add(r.Sec); it.SubItems.Add(r.Status);
        it.Tag = r.Kind;
        it.ForeColor = r.Kind == 2 ? Theme.Bad : (r.Kind == 1 ? Theme.Warn : Theme.Text);
        lv.Items.Add(it);
        it.EnsureVisible();
    }

    void RefreshDynamic() {
        if (cardStatus == null) return;
        ShowTab(tab);
                if (lv != null) {
            foreach (ListViewItem it in lv.Items) {
                int k = (it.Tag is int) ? (int)it.Tag : 0;
                it.ForeColor = k == 2 ? Theme.Bad : (k == 1 ? Theme.Warn : Theme.Text);
            }
        }
        WizRefreshTexts();
        SettingsRefreshTexts();
    }

    // ------------------------------------------------------------ 엔진 연결
    void UI(Action a) {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); } catch (Exception) { }
    }

    void WireEngine() {
        E.OnSay = delegate(string t) { UI(delegate { Say(t); }); };
        E.OnProgress = delegate(double f) { UI(delegate { bar.Value = f; }); };
        E.OnStat = delegate(string t) { UI(delegate { lblStat.Key = null; lblStat.Text = t; }); };
        E.OnRow = delegate(ResRow r) { UI(delegate { AddRow(r); }); };
        E.OnFailedChanged = delegate { UI(delegate { SetRunning(running); }); };
        E.OnFinish = delegate(bool stopped) {
            UI(delegate {
                stopping = false;
                SetRunning(false);
                if (stopped) { lblStat.Key = "status_stopped"; lblStat.Text = L.T("status_stopped"); }
            });
        };
    }

    void SetRunning(bool on) {
        running = on;
        btnStop.Visible = on;
        btnStop.Enabled = on && !stopping;
        btnWizard.Enabled = !on;
        btnRetry.Visible = !on && E.Failed.Count > 0;
        UpdateStatus();
        WizSetRunning(on);
    }

    void StopConversion() {
        if (!running) return;
        stopping = true;
        btnStop.Enabled = false;
        lblStat.Key = "m_stopping"; lblStat.Text = L.T("m_stopping");
        E.Stop();
    }

    void OnRetry() {
        if (E.Failed.Count == 0 || running) return;
        if (!ConvOk()) { Dialogs.Msg(this, L.T("msg_err_conv")); return; }
        SaveAll();
        RunOptions o = BuildOptions("std");
        SetRunning(true);
        bar.Value = 0;
        ShowTab("log");
        E.StartRetry(o);
    }

    RunOptions BuildOptions(string mode) {
        RunOptions o = new RunOptions();
        o.Mode = mode;
        o.In = S.Inp.Trim().TrimEnd('\\');
        o.Out = S.Out.Trim().TrimEnd('\\');
        if (S.SameDir || mode == "fast") o.Out = o.In;
        o.Blender = Clean(S.Blender); o.Conv = Clean(S.Conv); o.Py = PyPath;
        o.Par = S.Par; o.Sub = S.Sub; o.Inc = S.Inc; o.DelX = S.DelX;
        foreach (KeyValuePair<string, int> kv in S.Copt) o.Copt[kv.Key] = kv.Value;
        o.ExOpt = S.ExOpt;
        return o;
    }

    // 변환 시작 (마법사 마지막 단계에서 호출)
    public void StartConversion() {
        if (running) return;
        SyncSettingsFromUi();
        string mode = wizMode;
        RunOptions o = BuildOptions(mode);
        string title = L.T("app_title");
        if (!Directory.Exists(o.In)) { Dialogs.Msg(this, L.T("msg_err_in")); return; }
        if (o.Out.Length == 0) { Dialogs.Msg(this, L.T("msg_err_out")); return; }
        if (mode != "fast") {
            if (!BlenderOk()) { Dialogs.Msg(this, L.T("msg_err_bl")); return; }
            if (!File.Exists(PyPath)) { Dialogs.Msg(this, L.T("msg_err_py")); return; }
            if (mode == "std" && !ConvOk()) { Dialogs.Msg(this, L.T("msg_err_conv")); return; }
        }
        S.DoConv = (mode == "std"); S.Fast = (mode == "fast");
        S.Save();
        log.Text = "";
        try { if (!Directory.Exists(o.Out)) Directory.CreateDirectory(o.Out); } catch (Exception) { }
        List<ObjItem> objs = Engine.FindObjs(o.In, o.Sub);
        if (o.Inc) {
            List<ObjItem> sel = Engine.FilterChanged(objs, o.Out, mode == "std" ? ".o3d" : (mode == "fast" ? ".o3d" : ".x"));
            Say(L.T("m_changed", sel.Count, objs.Count));
            objs = sel;
        }
        if (objs.Count == 0) { Dialogs.Msg(this, L.T("m_none")); return; }
        S.AddRecent(o.In); S.Save();
        RecentRefresh();
        SwitchPage("dashboard");
        lv.Items.Clear();
        bar.Value = 0;
        ShowTab(mode == "fast" ? "res" : "log");
        SetRunning(true);
        E.Start(o, objs);
    }
}
}
