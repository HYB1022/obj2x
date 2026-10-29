using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Obj2x {

public partial class MainForm {
    Num numParA; Tx tBlender, tConv, tEx;
    Dictionary<string, Check> omsiCbs = new Dictionary<string, Check>();
    Combo cmbLang; Segmented segTheme, segAccent;
    Toggle tgFont, tgUpd, tgDelx;
    Lbl lblTmp, lblBackup;
    AboutHero hero;
    string[] copKeys = Settings.CoptKeys;
    static readonly string[] CopNames = new string[] { "X-File: z up", "Separate Files", "Show Details of X", "Remove Specular", "Opt. Materials", "Opt. Vertices", "All Normals Up", "Use Submesh Local Pos. as Global Pos." };

    Card NewCard(Panel pg, int y, int h) {
        Card c = new Card(); c.SetBounds(24, y, 760, h); c.Tag = PinInfo.S(24);
        pg.Controls.Add(c); return c;
    }
    void CardHead(Card c, string key) {
        LK(c, key, FR.Bold, LR.Text, 20, 14);
        Div d = new Div(); d.SetBounds(20, 38, 720, 1); d.Tag = PinInfo.S(20); c.Controls.Add(d);
    }

    // ------------------------------------------------------------ 고급
    void BuildAdvanced() {
        Panel pg = pages["advanced"];
        LK(pg, "nav_advanced", FR.Title, LR.Text, 24, 20);

        Card c1 = NewCard(pg, 56, 228);
        CardHead(c1, "sec_adv_omsi");
        LK(c1, "sec_adv_omsi_sub", FR.Sub, LR.Muted, 20, 42);
        for (int i = 0; i < 8; i++) {
            string k = copKeys[i];
            Check cb = new Check(); cb.Text = CopNames[i]; cb.Checked = S.Copt[k] == 1; cb.RefreshUi();
            int col = i % 2, row = i / 2;
            Put(c1, cb, 20 + col * 360, 72 + row * 28);
            string kk = k;
            cb.CheckedChanged += delegate { S.Copt[kk] = omsiCbs[kk].Checked ? 1 : 0; };
            omsiCbs[k] = cb;
        }
        LK(c1, "lbl_par", FR.Main, LR.Text, 20, 190);
        int nx = 20 + Ui.MaxW("lbl_par", Ui.F(FR.Main)) + 16;
        numParA = new Num(); numParA.Minimum = 1; numParA.Maximum = 20; numParA.Width = 64; numParA.RefreshUi();
        numParA.Value = Math.Max(1, Math.Min(20, S.Par));
        Put(c1, numParA, nx, 187);
        Lbl cores = Lbl.K("lbl_cores", FR.Sub, LR.Muted); cores.Args = new object[] { Environment.ProcessorCount }; cores.RefreshUi();
        Put(c1, cores, nx + 64 + 16, 191);
        numParA.ValueChanged += delegate {
            S.Par = (int)numParA.Value;
            if (numParW != null && numParW.Value != numParA.Value) { bool ol = wizLoading; wizLoading = true; numParW.Value = numParA.Value; wizLoading = ol; }
        };

        Card c2 = NewCard(pg, 296, 190);
        CardHead(c2, "sec_adv_rna");
        Lbl sub = Lbl.K("sec_adv_rna_sub", FR.Sub, LR.Muted); Put(c2, sub, 20, 44);
        tEx = new Tx(); tEx.Multiline = true; tEx.ScrollBars = ScrollBars.Vertical; tEx.AcceptsReturn = true;
        tEx.Tag = PinInfo.S(20); tEx.SetBounds(20, 92, 720, 80);
        tEx.Text = S.ExOpt; tEx.RefreshUi(); c2.Controls.Add(tEx);
        tEx.TextChanged += delegate { S.ExOpt = tEx.Text; };

        Card c3 = NewCard(pg, 498, 150);
        CardHead(c3, "sec_adv_paths");
        LK(c3, "lbl_blender_path", FR.Main, LR.Text, 20, 48);
        RButton bb = RButton.K("btn_browse", false, 28); bb.Radius = 6;
        tBlender = TB(c3, 20, 72, 20 + bb.Width + 10); tBlender.Text = S.Blender;
        PutR(c3, bb, 20, 71);
        LK(c3, "lbl_conv_path", FR.Main, LR.Text, 20, 104);
        // 두 번째 줄은 y 가 더 아래
        c3.Height = 176;
        RButton bc = RButton.K("btn_browse", false, 28); bc.Radius = 6;
        tConv = TB(c3, 20, 128, 20 + bc.Width + 10); tConv.Text = S.Conv;
        PutR(c3, bc, 20, 127);
        tBlender.TextChanged += delegate { S.Blender = tBlender.Text; UpdateStatus(); };
        tConv.TextChanged += delegate { S.Conv = tConv.Text; UpdateStatus(); };
        tBlender.Leave += delegate { S.Save(); };
        tConv.Leave += delegate { S.Save(); };
        bb.Click += delegate { PickBlender(); };
        bc.Click += delegate {
            OpenFileDialog d = new OpenFileDialog();
            d.Title = L.T("lbl_conv_path"); d.Filter = "OmsiXConv.exe|OmsiXConv.exe|Executable (*.exe)|*.exe"; d.CheckFileExists = true;
            if (d.ShowDialog(this) == DialogResult.OK) { tConv.Text = d.FileName; S.Save(); }
        };
    }

    // ------------------------------------------------------------ 설정
    void BuildSettings() {
        Panel pg = pages["settings"];
        LK(pg, "nav_settings", FR.Title, LR.Text, 24, 20);

        Card a = NewCard(pg, 56, 52 + 4 * 56 + 8);
        CardHead(a, "sec_appearance");
        LK(a, "lbl_language", FR.Main, LR.Text, 20, 62);
        cmbLang = new Combo(); cmbLang.Width = 170; cmbLang.Items.Add("한국어"); cmbLang.Items.Add("English"); cmbLang.SelectedIndex = (S.Lang == "en") ? 1 : 0; cmbLang.RefreshUi();
        PutR(a, cmbLang, 20, 58);
        LK(a, "lbl_theme", FR.Main, LR.Text, 20, 118);
        segTheme = new Segmented(); segTheme.Add("system", "theme_system", false); segTheme.Add("light", "theme_light", false); segTheme.Add("dark", "theme_dark", false); segTheme.Value = S.Theme;
        PutR(a, segTheme, 20, 110);
        LK(a, "lbl_accent", FR.Main, LR.Text, 20, 168);
        LK(a, "lbl_accent_sub", FR.Sub, LR.Muted, 20, 190);
        segAccent = new Segmented(); segAccent.Add("indigo", "accent_indigo", false); segAccent.Add("teal", "accent_teal", false); segAccent.Add("rose", "accent_rose", false); segAccent.Value = S.Accent;
        PutR(a, segAccent, 20, 172);
        tgFont = ToggleRow(a, 224 + 0, "lbl_sysfont", "lbl_sysfont_sub", null);
        tgFont.Checked = S.SysFont;
        cmbLang.SelectedIndexChanged += delegate { string nl = cmbLang.SelectedIndex == 1 ? "en" : "ko"; if (nl == S.Lang) return; S.Lang = nl; ApplyLanguage(); S.Save(); };
        segTheme.Changed += delegate { S.Theme = segTheme.Value; ApplyTheme(); S.Save(); };
        segAccent.Changed += delegate { S.Accent = segAccent.Value; ApplyTheme(); S.Save(); };
        tgFont.CheckedChanged += delegate { if (building) return; S.SysFont = tgFont.Checked; ApplyFont(); S.Save(); };

        Card b = NewCard(pg, a.Bottom + 12, 52 + 2 * 60 + 4);
        CardHead(b, "sec_behavior");
        tgUpd = ToggleRow(b, 48, "lbl_updcheck", "lbl_updcheck_sub", null); tgUpd.Checked = S.UpdCheck;
        Div bd = new Div(); bd.SetBounds(20, 108, 720, 1); bd.Tag = PinInfo.S(20); b.Controls.Add(bd);
        tgDelx = ToggleRow(b, 112, "lbl_delx", "lbl_delx_sub", null); tgDelx.Checked = S.DelX;
        tgUpd.CheckedChanged += delegate { S.UpdCheck = tgUpd.Checked; };
        tgDelx.CheckedChanged += delegate { S.DelX = tgDelx.Checked; };

        Card f = NewCard(pg, b.Bottom + 12, 52 + 2 * 60 + 4);
        CardHead(f, "sec_files");
        LK(f, "lbl_backup", FR.Main, LR.Text, 20, 50);
        lblBackup = new Lbl(Path.Combine(Here, "obj2x_backup"), FR.Sub, LR.Muted); Put(f, lblBackup, 20, 72);
        RButton bo = RButton.K("btn_open_folder", false, 30); bo.Radius = 6;
        PutR(f, bo, 20, 54);
        bo.Click += delegate {
            string d = Path.Combine(Here, "obj2x_backup");
            try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); Process.Start(d); } catch (Exception) { }
        };
        Div fd = new Div(); fd.SetBounds(20, 108, 720, 1); fd.Tag = PinInfo.S(20); f.Controls.Add(fd);
        LK(f, "lbl_temp", FR.Main, LR.Text, 20, 116);
        lblTmp = new Lbl("", FR.Sub, LR.Muted); Put(f, lblTmp, 20, 138);
        RButton bt = RButton.K("btn_clean", false, 30); bt.Radius = 6;
        PutR(f, bt, 20, 120);
        bt.Click += delegate {
            foreach (string p in TempFiles()) { try { File.Delete(p); } catch (Exception) { } }
            UpdateTempLabel();
            Dialogs.Msg(this, L.T("msg_temp_clean"));
        };
        UpdateTempLabel();
    }

    List<string> TempFiles() {
        List<string> l = new List<string>();
        l.Add(Path.Combine(Path.GetTempPath(), "obj2x_job.txt"));
        l.Add(Path.Combine(Path.GetTempPath(), "obj2x_log.txt"));
        return l;
    }
    void UpdateTempLabel() {
        long sz = 0;
        foreach (string p in TempFiles()) { try { if (File.Exists(p)) sz += new FileInfo(p).Length; } catch (Exception) { } }
        lblTmp.Text = L.T("lbl_temp_sub", Math.Round(sz / 1048576.0, 2).ToString("0.00") + " MB");
    }

    void SettingsRefreshTexts() {
        if (lblTmp == null) return;
        UpdateTempLabel();
        UpdateLinkText();
    }

    // ------------------------------------------------------------ 정보
    void BuildAbout() {
        Panel pg = pages["about"];
        pg.AutoScroll = false;
        hero = new AboutHero(); hero.Dock = DockStyle.Fill; pg.Controls.Add(hero);
        hero.UpdateValue = delegate {
            if (updText == "upd_link_new") return L.T("about_upd_new", updLatest);
            if (updText == "upd_link_latest") return L.T("upd_link_latest", Program.AppVersion);
            if (updText != null) return L.T(updText);
            return L.T("about_upd_idle");
        };
        hero.UpdateIsNew = delegate { return updLatest.Length > 0; };
        hero.UpdateRowClicked += delegate {
            if (updLatest.Length > 0) PromptUpdate();
            else if (!updBusy) { updText = "upd_link_checking"; UpdateLinkText(); StartUpdateCheck(true); }
        };
        hero.RepoClicked += delegate { try { Process.Start(RepoUrl()); } catch (Exception) { } };
        hero.DisclaimerClicked += delegate { ShowDisclaimer(true); };
        UpdateLinkText();
    }

    public void ShowDisclaimer(bool review) {
        Dialogs.Disclaimer(this, review, delegate(bool ok) {
            if (review) return;
            if (ok) { S.Agreed = true; S.AgreedVer = Program.AppVersion; S.Save(); }
            else { Close(); }
        });
    }

    // 폼 값을 설정 객체로 모은다
    void SyncSettingsFromUi() {
        if (building || tIn == null) return;
        if (tBlender != null) S.Blender = tBlender.Text.Trim();
        if (tConv != null) S.Conv = tConv.Text.Trim();
        if (tEx != null) S.ExOpt = tEx.Text;
    }
}
}
