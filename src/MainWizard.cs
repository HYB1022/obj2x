using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Obj2x {

public partial class MainForm {
    int wizStep = 1;
    string wizMode = "std";
    Lbl wizTitle; Stepper stepper; Panel wizContent;
    Panel s1, s2, s3, s4;
    RadioCard rcStd, rcFast, rcX; Lbl availLbl;
    Tx tIn, tOut; RButton bIn, bOut; Toggle tgSame; Lbl lblSame;
    Card cardRecent; RecentList recent; Lbl lblRecentTitle;
    Toggle tgInc, tgSub; Num numParW; Lbl coresW;
    Lbl[] sumLbl = new Lbl[4], sumVal = new Lbl[4];
    RButton wizBack, wizNext, wizCancel, wizAdv;
    bool wizLoading;

    // 제목/설명 + 오른쪽 토글이 있는 행 (카드 안)
    public Toggle ToggleRow(Card c, int y, string titleKey, string subKey, object[] subArgs) {
        Lbl t = Lbl.K(titleKey, FR.Main, LR.Text); Put(c, t, 20, subKey == null ? y + 14 : y);
        if (subKey != null) { Lbl s = Lbl.K(subKey, FR.Sub, LR.Muted); if (subArgs != null) { s.Args = subArgs; s.RefreshUi(); } Put(c, s, 20, y + 22); }
        Toggle tg = new Toggle(); PutR(c, tg, 20, y + 8);
        return tg;
    }

    void BuildWizard() {
        Panel pg = pages["wizard"];
        wizTitle = Lbl.K("wiz_title", FR.Title, LR.Text); Put(pg, wizTitle, 24, 20);
        stepper = new Stepper(); stepper.SetBounds(24, 52, 760, 58); pg.Controls.Add(stepper);
        wizContent = new Page(); wizContent.SetBounds(24, 120, 760, 440);
        wizContent.AutoScroll = true;
        wizContent.Resize += delegate { Pin.Apply(wizContent); };
        pg.Controls.Add(wizContent);
        s1 = NewStep(); s2 = NewStep(); s3 = NewStep(); s4 = NewStep();
        BuildStep1(); BuildStep2(); BuildStep3(); BuildStep4();

        wizCancel = RButton.K("btn_cancel", false, 38);
        wizBack = RButton.K("btn_back", false, 38);
        wizNext = RButton.K("btn_next", true, 38);
        int w = Math.Max(wizNext.Width, Math.Max(Ui.MaxW("btn_run", Ui.F(FR.Main)) + 36, 96));
        wizNext.Width = w; wizBack.Width = Math.Max(wizBack.Width, 96); wizCancel.Width = Math.Max(wizCancel.Width, 96);
        pg.Controls.Add(wizCancel); pg.Controls.Add(wizBack); pg.Controls.Add(wizNext);
        wizCancel.Click += delegate { SwitchPage("dashboard"); };
        wizBack.Click += delegate { if (wizStep > 1) { wizStep--; UpdateWizStep(); } };
        wizNext.Click += delegate { WizNext(); };
        pg.Resize += delegate { LayoutWizard(); };
        UpdateWizStep();
    }

    Panel NewStep() {
        Panel p = new Page(); p.Tag = PinInfo.S(0); p.SetBounds(0, 0, 760, 300); p.Visible = false;
        wizContent.Controls.Add(p); return p;
    }
    static readonly int[] StepH = new int[] { 330, 472, 290, 290 };

    void LayoutWizard() {
        if (wizNext == null) return;
        Panel pg = pages["wizard"];
        int pw = pg.ClientSize.Width, ph = pg.ClientSize.Height;
        if (pw < 100 || ph < 100) return;
        int m = Ui.S(24);
        stepper.SetBounds(m, Ui.S(52), pw - 2 * m, Ui.S(58));
        wizContent.SetBounds(m, Ui.S(112), pw - 2 * m, Math.Max(Ui.S(200), ph - Ui.S(112 + 64)));
        wizContent.AutoScrollMinSize = new Size(1, Ui.S(StepH[Math.Max(0, Math.Min(3, wizStep - 1))]));
        int y = ph - Ui.S(54);
        wizNext.Location = new Point(pw - m - wizNext.Width, y);
        wizBack.Location = new Point(wizNext.Left - Ui.S(10) - wizBack.Width, y);
        wizCancel.Location = new Point(m, y);
        Panel[] st = new Panel[] { s1, s2, s3, s4 };
        for (int i = 0; i < 4; i++) st[i].Height = Ui.S(StepH[i]);
        Pin.Apply(pg);
        if (availLbl != null) availLbl.WrapW = Math.Max(Ui.S(280), wizContent.ClientSize.Width - Ui.S(8));
    }

    // ---- 1단계: 종류 선택
    void BuildStep1() {
        LK(s1, "wiz_sub", FR.Bold, LR.Text, 0, 4);
        rcStd = NewCard(s1, "std", "wiz_std_title", "wiz_std_sub", "wiz_std_badge", 34);
        rcX = NewCard(s1, "x", "wiz_xonly_title", "wiz_xonly_sub", null, 114);
        rcFast = NewCard(s1, "fast", "wiz_fast_title", "wiz_fast_sub", "wiz_fast_badge", 194);
        availLbl = new Lbl(); availLbl.FontRole = FR.Sub; availLbl.Role = LR.Warn;
        Put(s1, availLbl, 0, 276);
    }

    RadioCard NewCard(Panel parent, string mode, string tk, string sk, string bk, int y) {
        RadioCard c = new RadioCard(); c.Mode = mode; c.TitleKey = tk; c.SubKey = sk; c.BadgeKey = bk;
        c.SetBounds(0, y, 760, 72); c.Tag = PinInfo.S(0);
        parent.Controls.Add(c);
        c.Picked += delegate { SetMode(mode); };
        return c;
    }

    void SetMode(string m) { SetMode(m, true); }
    // persist=false: 도구가 없어 임시로 바꾸는 경우라 저장된 선택은 건드리지 않는다
    void SetMode(string m, bool persist) {
        wizMode = m;
        if (persist) { S.Fast = (m == "fast"); S.DoConv = (m == "std"); }
        rcStd.Selected = (m == "std"); rcX.Selected = (m == "x"); rcFast.Selected = (m == "fast");
        rcStd.Invalidate(); rcX.Invalidate(); rcFast.Invalidate();
        WizModeEffects();
    }

    void WizModeEffects() {
        if (tOut == null) return;
        bool lockOut = tgSame.Checked || wizMode == "fast";
        tOut.Enabled = !lockOut; bOut.Enabled = !lockOut;
        if (lockOut) tOut.Text = tIn.Text;
        numParW.Enabled = (wizMode == "std");
    }

    // 도구가 없으면 해당 방식을 잠근다
    void WizAvailability() {
        if (rcStd == null) return;
        bool hb = BlenderOk(), hc = ConvOk();
        rcFast.Enabled = true; rcStd.Enabled = hb && hc; rcX.Enabled = hb;
        if (!rcStd.Enabled && wizMode == "std") SetMode("fast", false);
        if (!rcX.Enabled && wizMode == "x") SetMode("fast", false);
        List<string> missing = new List<string>();
        if (!hb) missing.Add("Blender (blender.exe)");
        if (!hc) missing.Add("OmsiXConv (OmsiXConv.exe)");
        if (missing.Count > 0) {
            string r = L.T("wiz_avail_head", string.Join(", ", missing.ToArray()));
            if (!hb) r += L.T("wiz_avail_noblender");
            if (!hc) r += L.T("wiz_avail_noconv");
            r += L.T("wiz_avail_tail");
            availLbl.Text = r;
        } else availLbl.Text = "";
        rcStd.Invalidate(); rcX.Invalidate(); rcFast.Invalidate();
    }

    // ---- 2단계: 경로
    void BuildStep2() {
        LK(s2, "wiz_step2_title", FR.H2, LR.Text, 0, 0);
        LK(s2, "wiz_step2_sub", FR.Sub, LR.Muted, 0, 24);
        LK(s2, "lbl_in_folder", FR.Bold, LR.Text, 0, 52);
        bIn = RButton.K("btn_browse", false, 28); bIn.Radius = 6;
        tIn = TB(s2, 0, 74, bIn.Width + 10);
        PutR(s2, bIn, 0, 73);
        LK(s2, "lbl_in_hint", FR.Sub, LR.Muted, 0, 104);
        LK(s2, "lbl_out_folder", FR.Bold, LR.Text, 0, 130);
        bOut = RButton.K("btn_browse", false, 28); bOut.Radius = 6;
        tOut = TB(s2, 0, 152, bOut.Width + 10);
        PutR(s2, bOut, 0, 151);
        tgSame = new Toggle(); Put(s2, tgSame, 0, 186);
        lblSame = LK(s2, "chk_samedir", FR.Main, LR.Text, 56, 188);
        lblRecentTitle = LK(s2, "recent_title", FR.Bold, LR.Text, 0, 218);
        cardRecent = new Card(); cardRecent.SetBounds(0, 240, 760, 8 + 5 * RecentList.RowH); cardRecent.Tag = PinInfo.S(0);
        s2.Controls.Add(cardRecent);
        recent = new RecentList(); recent.SetBounds(8, 4, 744, 5 * RecentList.RowH); recent.Tag = PinInfo.S(8);
        cardRecent.Controls.Add(recent);
        recent.PathPicked += delegate(object o, PathEventArgs e) {
            if (RecentList.Exists(e.Path)) { tIn.Text = e.Path; }
        };

        bIn.Click += delegate { string p = BrowseFolder(tIn.Text); if (p != null) tIn.Text = p; };
        bOut.Click += delegate { string p = BrowseFolder(tOut.Text); if (p != null) tOut.Text = p; };
        tIn.TextChanged += delegate { if (wizLoading) return; S.Inp = tIn.Text; if (tgSame.Checked || wizMode == "fast") tOut.Text = tIn.Text; };
        tOut.TextChanged += delegate { if (wizLoading) return; S.Out = tOut.Text; };
        tgSame.CheckedChanged += delegate { if (wizLoading) return; S.SameDir = tgSame.Checked; WizModeEffects(); };
        tIn.AllowDrop = true;
        tIn.DragEnter += delegate(object o, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        tIn.DragDrop += delegate(object o, DragEventArgs e) {
            string[] f = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (f != null && f.Length > 0) tIn.Text = Directory.Exists(f[0]) ? f[0] : Path.GetDirectoryName(f[0]);
        };
    }

    string BrowseFolder(string start) {
        FolderBrowserDialog d = new FolderBrowserDialog();
        try { if (Directory.Exists(start)) d.SelectedPath = start; } catch (Exception) { }
        if (d.ShowDialog(this) == DialogResult.OK) return d.SelectedPath;
        return null;
    }

    public void RecentRefresh() {
        if (recent == null) return;
        recent.SetPaths(S.Recent);
        cardRecent.Height = recent.Height + Ui.S(8);
    }

    // ---- 3단계: 옵션
    void BuildStep3() {
        LK(s3, "wiz_step3_title", FR.H2, LR.Text, 0, 0);
        LK(s3, "wiz_step3_sub", FR.Sub, LR.Muted, 0, 26);
        Card c = new Card(); c.SetBounds(0, 56, 760, 3 * 64 + 8); c.Tag = PinInfo.S(0);
        s3.Controls.Add(c);
        tgInc = ToggleRow(c, 12, "chk_inc", null, null);
        Div d1 = new Div(); d1.SetBounds(20, 64, 720, 1); d1.Tag = PinInfo.S(20); c.Controls.Add(d1);
        tgSub = ToggleRow(c, 76, "chk_sub", null, null);
        Div d2 = new Div(); d2.SetBounds(20, 128, 720, 1); d2.Tag = PinInfo.S(20); c.Controls.Add(d2);
        LK(c, "lbl_par", FR.Main, LR.Text, 20, 140);
        coresW = Lbl.K("lbl_cores", FR.Sub, LR.Muted); coresW.Args = new object[] { Environment.ProcessorCount }; coresW.RefreshUi(); Put(c, coresW, 20, 162);
        numParW = new Num(); numParW.Minimum = 1; numParW.Maximum = 20; numParW.Width = 70; numParW.RefreshUi();
        PutR(c, numParW, 20, 146);
        tgInc.CheckedChanged += delegate { if (!wizLoading) S.Inc = tgInc.Checked; };
        tgSub.CheckedChanged += delegate { if (!wizLoading) S.Sub = tgSub.Checked; };
        numParW.ValueChanged += delegate { if (wizLoading) return; S.Par = (int)numParW.Value; if (numParA != null && numParA.Value != numParW.Value) numParA.Value = numParW.Value; };
    }

    // ---- 4단계: 확인
    void BuildStep4() {
        LK(s4, "wiz_step4_title", FR.H2, LR.Text, 0, 0);
        LK(s4, "wiz_step4_sub", FR.Sub, LR.Muted, 0, 26);
        Card c = new Card(); c.SetBounds(0, 56, 760, 160); c.Tag = PinInfo.S(0);
        s4.Controls.Add(c);
        string[] keys = new string[] { "wiz_confirm_mode", "wiz_confirm_input", "wiz_confirm_output", "wiz_confirm_opts" };
        for (int i = 0; i < 4; i++) {
            sumLbl[i] = Lbl.K(keys[i], FR.Main, LR.Muted); Put(c, sumLbl[i], 20, 18 + i * 32);
            Lbl v = new Lbl(); v.FontRole = FR.Bold; v.AutoSize = false; v.AutoEllipsis = true; v.Size = new Size(540, 22);
            v.Tag = PinInfo.S(20); v.RefreshUi();
            sumVal[i] = Put(c, v, 190, 18 + i * 32);
        }
        wizAdv = RButton.K("btn_open_adv", false, 36);
        Put(s4, wizAdv, 0, 232);
        wizAdv.Click += delegate { SwitchPage("advanced"); };
    }

    string ModeName() {
        return L.T(wizMode == "fast" ? "sum_mode_fast" : (wizMode == "x" ? "sum_mode_x" : "sum_mode_std"));
    }

    void SummaryRefresh() {
        if (sumVal[0] == null) return;
        sumVal[0].Text = ModeName();
        sumVal[1].Text = tIn.Text;
        sumVal[2].Text = (tgSame.Checked || wizMode == "fast") ? tIn.Text : tOut.Text;
        List<string> o = new List<string>();
        if (tgInc.Checked) o.Add(L.T("sum_opt_inc"));
        if (tgSub.Checked) o.Add(L.T("sum_opt_sub"));
        if (wizMode == "std") o.Add(L.T("sum_opt_par", (int)numParW.Value));
        sumVal[3].Text = o.Count > 0 ? string.Join(", ", o.ToArray()) : L.T("sum_opt_none");
    }

    void UpdateWizStep() {
        s1.Visible = (wizStep == 1); s2.Visible = (wizStep == 2); s3.Visible = (wizStep == 3); s4.Visible = (wizStep == 4);
        stepper.Step = wizStep; stepper.Invalidate();
        wizBack.Visible = (wizStep > 1);
        wizNext.Key = (wizStep == 4) ? "btn_run" : "btn_next";
        wizNext.RefreshUi();
        LayoutWizard();
        if (wizStep == 2) RecentRefresh();
        if (wizStep == 4) SummaryRefresh();
        wizNext.Enabled = !running;
    }

    void WizNext() {
        if (wizStep == 2) {
            string title = L.T("app_title");
            if (!Directory.Exists(tIn.Text.Trim())) { Dialogs.Msg(this, L.T("msg_err_in")); return; }
            if (!(tgSame.Checked || wizMode == "fast") && tOut.Text.Trim().Length == 0) { Dialogs.Msg(this, L.T("msg_err_out")); return; }
        }
        if (wizStep < 4) { wizStep++; UpdateWizStep(); }
        else StartConversion();
    }

    // 마법사에 들어올 때 저장된 설정을 컨트롤에 채운다
    void WizSync() {
        if (tIn == null) return;
        wizLoading = true;
        tIn.Text = S.Inp; tOut.Text = S.Out;
        tgSame.Checked = S.SameDir;
        tgInc.Checked = S.Inc; tgSub.Checked = S.Sub;
        numParW.Value = Math.Max(1, Math.Min(20, S.Par));
        wizLoading = false;
        string m = S.Fast ? "fast" : (S.DoConv ? "std" : "x");
        SetMode(m, false);
        WizAvailability();
        UpdateWizStep();
        RecentRefresh();
    }

    void WizRefreshTexts() {
        if (wizNext == null) return;
        wizNext.RefreshUi();
        SummaryRefresh();
        WizAvailability();
        stepper.Invalidate();
    }

    public void WizGo(int step) { wizStep = step; UpdateWizStep(); }

    void WizSetRunning(bool on) { if (wizNext != null) wizNext.Enabled = !on; }
}
}
