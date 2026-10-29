using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Obj2x {

// 같은 창 위에 어둡게 깔리는 대화 상자 (메시지, 면책 조항, 선택 목록 모두 이것을 쓴다)
public class DialogOverlay : Panel {
    Form host; Action<int> done;
    Bitmap shot;
    Lbl title, body;
    Control extra;
    public RButton[] Btn;
    Rectangle cardRect = Rectangle.Empty;
    int cw;
    float t = 1f;          // 0 = 안 보임, 1 = 다 나타남
    bool closing, shown;

    public static DialogOverlay Show(Form host, string title, string body, Control extra, string[] btns, int primary, int width, Action<int> done) {
        DialogOverlay ov = new DialogOverlay(host, title, body, extra, btns, primary, width, done);
        host.Controls.Add(ov);
        ov.Bounds = host.ClientRectangle;
        ov.BringToFront();
        ov.DoLayout();
        ov.Focus();
        ov.Intro();
        return ov;
    }

    DialogOverlay(Form host, string titleText, string bodyText, Control extra, string[] btns, int primary, int width, Action<int> done) {
        this.host = host; this.done = done; this.extra = extra; cw = width;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Bounds = host.ClientRectangle;
        shot = Snapshot(host);
        cw = Ui.S(width);
        title = new Lbl(titleText, FR.Big, LR.Text); title.Font = new Font(Ui.Family, 17f * Ui.FZ, FontStyle.Bold);
        Controls.Add(title);
        if (bodyText != null) { body = new Lbl(bodyText, FR.Main, LR.Text); body.WrapW = cw - Ui.S(64); Controls.Add(body); }
        if (extra != null) Controls.Add(extra);
        int bw = Ui.S(96);
        foreach (string s in btns) bw = Math.Max(bw, Ui.TextW(s, Ui.F(FR.Main)) + Ui.S(36));
        Btn = new RButton[btns.Length];
        for (int i = 0; i < btns.Length; i++) {
            RButton b = new RButton(); b.Text = btns[i]; b.Primary = (i == primary); b.Radius = 10; b.Size = new Size(bw, Ui.S(40));
            int idx = i;
            b.Click += delegate { Close(idx); };
            Btn[i] = b; Controls.Add(b);
        }
        Resize += delegate { DoLayout(); };
    }

    void Kids(bool v) { foreach (Control c in Controls) c.Visible = v; shown = v; }
    void Intro() {
        if (Anim.Off) { t = 1; Kids(true); return; }
        t = 0; Kids(false);
        Anim.Add(delegate(float dt) {
            if (IsDisposed || closing) return false;
            t = Math.Min(1f, t + dt / 0.22f);
            if (!shown && t >= 0.55f) Kids(true);
            Invalidate();
            return t < 1f;
        });
    }

    static Bitmap Snapshot(Form host) {
        Size sz = host.ClientSize;
        if (sz.Width < 1 || sz.Height < 1) return null;
        try {
            if (host.Visible && host.WindowState != FormWindowState.Minimized) {
                host.Refresh(); Application.DoEvents();
                Bitmap shot = new Bitmap(sz.Width, sz.Height);
                using (Graphics g = Graphics.FromImage(shot)) {
                    g.CopyFromScreen(host.PointToScreen(Point.Empty), Point.Empty, sz);
                    }
                return shot;
            }
        } catch (Exception) { }
        try {
            Bitmap bmp = new Bitmap(sz.Width, sz.Height);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.Clear(host.BackColor);
                foreach (Control c in host.Controls) {
                    if (!c.Visible || c.Width < 1 || c.Height < 1) continue;
                    using (Bitmap cb = new Bitmap(c.Width, c.Height)) {
                        c.DrawToBitmap(cb, new Rectangle(0, 0, c.Width, c.Height));
                        g.DrawImage(cb, c.Left, c.Top);
                    }
                }
            }
            return bmp;
        } catch (Exception) { return null; }
    }

    void Close(int idx) {
        if (closing) return;
        closing = true;
        if (Anim.Off) { Remove(idx); return; }
        Kids(false);
        Anim.Add(delegate(float dt) {
            if (IsDisposed) return false;
            t = Math.Max(0f, t - dt / 0.15f);
            Invalidate();
            if (t <= 0f) { Remove(idx); return false; }
            return true;
        });
    }

    void Remove(int idx) {
        Form h = host;
        if (h != null && !h.IsDisposed) h.Controls.Remove(this);
        if (shot != null) { shot.Dispose(); shot = null; }
        Action<int> cb = done; done = null;
        Dispose();
        if (cb != null) cb(idx);
    }

    void DoLayout() {
        int p = Ui.S(32);
        int ch = p + title.Height + Ui.S(16) + (body != null ? body.Height + Ui.S(20) : 0) + (extra != null ? extra.Height + Ui.S(22) : 0) + Btn[0].Height + Ui.S(28);
        int cx = Math.Max(0, (Width - cw) / 2), cy = Math.Max(0, (Height - ch) / 2);
        cardRect = new Rectangle(cx, cy, cw, ch);
        int y = cy + p;
        title.Location = new Point(cx + p, y); y += title.Height + Ui.S(16);
        if (body != null) { body.Location = new Point(cx + p, y); y += body.Height + Ui.S(20); }
        if (extra != null) { extra.Location = new Point(cx + p, y); extra.Width = cw - 2 * p; y += extra.Height + Ui.S(22); }
        int x = cx + cw - p;
        for (int i = Btn.Length - 1; i >= 0; i--) { x -= Btn[i].Width; Btn[i].Location = new Point(x, y); x -= Ui.S(12); }
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
        Graphics g = e.Graphics;
        float e2 = Anim.EaseOut(t);
        if (shot != null) g.DrawImage(shot, 0, 0, Width, Height);
        else g.Clear(Theme.Bg);
        using (SolidBrush shade = new SolidBrush(Color.FromArgb((int)(150 * e2), 0, 0, 0))) g.FillRectangle(shade, 0, 0, Width, Height);
        if (cardRect.Width <= 0 || e2 <= 0.01f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float sc = 0.92f + 0.08f * e2;
        float w = cardRect.Width * sc, hh = cardRect.Height * sc;
        float x = cardRect.X + (cardRect.Width - w) / 2, y = cardRect.Y + (cardRect.Height - hh) / 2 + (1 - e2) * Ui.S(10);
        int a = (int)(255 * Math.Min(1f, e2 * 1.3f));
        using (GraphicsPath gp = Ui.Round(x, y, w - 1, hh - 1, Ui.S(28f) * sc))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, Theme.Card)))
        using (Pen p = new Pen(Color.FromArgb(a, Theme.CardBdr), 1)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
    }
}

// 라디오 목록 (선택 대화 상자용)
public class RadioList : Control {
    public string[][] Items; public string Value;
    public RadioList(string[][] items, string cur) {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Items = items; Value = cur; Height = Ui.S(46) * items.Length; Cursor = Cursors.Hand;
    }
    protected override void OnMouseClick(MouseEventArgs e) {
        int i = e.Y / Ui.S(46);
        if (i >= 0 && i < Items.Length) { Value = Items[i][0]; Invalidate(); }
        base.OnMouseClick(e);
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < Items.Length; i++) {
            float k = Ui.Z; int rh = Ui.S(46);
            int y = i * rh; bool sel = Items[i][0] == Value;
            using (Pen p = new Pen(sel ? Theme.Accent : Theme.Muted, 2)) g.DrawEllipse(p, 2 * k, y + 13 * k, 18 * k, 18 * k);
            if (sel) using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 7 * k, y + 18 * k, 8 * k, 8 * k);
            TextRenderer.DrawText(g, Items[i][1], Ui.F(FR.Main), new Rectangle(Ui.S(34), y, Width - Ui.S(34), rh), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

public static class Dialogs {
    // 확인 하나만 있는 안내
    public static void Msg(Form host, string text) { Msg(host, "obj2x", text); }
    public static void Msg(Form host, string title, string text) {
        DialogOverlay.Show(host, title, text, null, new string[] { L.T("wel_ok") }, 0, 480, null);
    }
    // 면책 조항. review=true면 닫기만, false면 체크 후 계속/종료. 계속이면 true.
    public static DialogOverlay Disclaimer(Form host, bool review, Action<bool> done) {
        Check ck = null;
        if (!review) { ck = new Check(); ck.Text = L.T("disclaimer_agree_chk"); ck.RefreshUi(); }
        string[] btns = review ? new string[] { L.T("btn_close") } : new string[] { L.T("btn_exit"), L.T("btn_continue") };
        DialogOverlay ov = DialogOverlay.Show(host, L.T("disclaimer_title"), L.T("disclaimer_text"), ck, btns, btns.Length - 1, 520,
            delegate(int i) { if (done != null) done(review || i == 1); });
        if (!review) { ov.Btn[1].Enabled = false; RButton ok = ov.Btn[1]; ck.CheckedChanged += delegate { ok.Enabled = ck.Checked; }; }
        return ov;
    }
    // 라디오 선택. 확인하면 고른 값, 취소하면 null
    public static void Choose(Form host, string title, string[][] items, string cur, Action<string> done) {
        RadioList rl = new RadioList(items, cur);
        DialogOverlay.Show(host, title, null, rl, new string[] { L.T("btn_cancel"), L.T("wel_ok") }, 1, 420,
            delegate(int i) { done(i == 1 ? rl.Value : null); });
    }
}

// 첫 실행 환영 화면: 인사말이 번갈아 나오고, 언어/보기 설정은 팝업으로 고른 뒤 시작을 누르면 면책 조항이 뜬다.
public class WelcomeForm : Form {
    class Tile : Control {
        public Tile() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; Size = new Size(200, 200); }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            LogoArt.Draw(e.Graphics, 14, 14, 150);
        }
    }
    Settings S;
    Tile tile; Lbl title, lblLang, lblView, gLang, gView; RButton start;
    System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
    int tick;
    static readonly string[] Greetings = new string[] { "안녕하세요", "Hello" };
    public bool Agreed;

    public WelcomeForm(Settings s) {
        S = s;
        Size = new Size(1000, 700); MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        Icon = AppIcon.Get();
        tile = new Tile();
        title = new Lbl(); title.Font = new Font(Ui.Family, 30f, FontStyle.Bold);
        Font gf = new Font("Segoe MDL2 Assets", 20f);
        gLang = new Lbl(); gLang.Text = ""; gLang.Font = gf; gLang.AutoSize = false; gLang.Size = new Size(44, 44); gLang.TextAlign = ContentAlignment.MiddleCenter; gLang.Cursor = Cursors.Hand;
        gView = new Lbl(); gView.Text = ""; gView.Font = gf; gView.AutoSize = false; gView.Size = new Size(44, 44); gView.TextAlign = ContentAlignment.MiddleCenter; gView.Cursor = Cursors.Hand;
        Font rf = new Font(Ui.Family, 17f, FontStyle.Bold);
        lblLang = new Lbl(); lblLang.Font = rf; lblLang.AutoSize = false; lblLang.Size = new Size(420, 44); lblLang.TextAlign = ContentAlignment.MiddleLeft; lblLang.Cursor = Cursors.Hand;
        lblView = new Lbl(); lblView.Font = rf; lblView.AutoSize = false; lblView.Size = new Size(420, 44); lblView.TextAlign = ContentAlignment.MiddleLeft; lblView.Cursor = Cursors.Hand;
        start = new RButton(); start.Primary = true; start.Radius = 8; start.Height = 42; start.Key = "wel_start";
        start.FontRole = FR.Bold; start.Width = Math.Max(110, Ui.MaxW("wel_start", Ui.F(FR.Bold)) + 48);
        Controls.Add(tile); Controls.Add(title); Controls.Add(gLang); Controls.Add(lblLang); Controls.Add(gView); Controls.Add(lblView); Controls.Add(start);
        EventHandler pickLang = delegate { PickLang(); };
        EventHandler pickView = delegate { PickView(); };
        gLang.Click += pickLang; lblLang.Click += pickLang;
        gView.Click += pickView; lblView.Click += pickView;
        start.Click += delegate { OnStart(); };
        Resize += delegate { DoLayout(); };
        tm.Interval = 40;
        tm.Tick += delegate { tick++; TickTitle(); };
        Shown += delegate { tm.Start(); };
        FormClosed += delegate { tm.Stop(); };
        HandleCreated += delegate { TitleBar(); };
        WindowState = FormWindowState.Maximized;
        ApplyAll();
    }

    void TitleBar() {
        try { if (Theme.Dark) W.DarkTitle(Handle); else W.LightTitle(Handle); } catch (Exception) { }
    }

    void ApplyAll() {
        Theme.Apply(S.Theme, S.Accent);
        L.Lang = S.Lang;
        Text = L.T("app_title");
        BackColor = Theme.Bg; ForeColor = Theme.Text;
        lblLang.Text = (L.Lang == "en") ? "English" : "한국어";
        lblView.Text = L.T("wel_view");
        start.RefreshUi();
        foreach (Lbl l in new Lbl[] { lblLang, lblView, gLang, gView }) l.ForeColor = Theme.Accent;
        TickTitle();
        if (IsHandleCreated) TitleBar();
        DoLayout();
        Invalidate(true);
    }

    void TickTitle() {
        int cyc = tick % 60;
        double a = Math.Min(1.0, Math.Min(cyc / 8.0, (60 - cyc) / 8.0));
        if (a < 0) a = 0;
        int idx = (tick / 60) % Greetings.Length;
        if (title.Text != Greetings[idx]) title.Text = Greetings[idx];
        title.ForeColor = Ui.Mix(Theme.Bg, Theme.Text, a);
    }

    void DoLayout() {
        int fw = ClientSize.Width, fh = ClientSize.Height;
        int lx = (int)(fw * 0.14), ty = (int)(fh * 0.24);
        tile.Location = new Point(lx, ty);
        title.Location = new Point(lx + 4, ty + 224);
        int rx = (int)(fw * 0.56), ry = (int)(fh * 0.56);
        gLang.Location = new Point(rx, ry); lblLang.Location = new Point(rx + 52, ry);
        gView.Location = new Point(rx, ry + 72); lblView.Location = new Point(rx + 52, ry + 72);
        start.Location = new Point(fw - start.Width - (int)(fw * 0.06), (int)(fh * 0.80));
    }

    void PickLang() {
        Dialogs.Choose(this, L.T("wel_lang"), new string[][] { new string[] { "ko", "한국어" }, new string[] { "en", "English" } }, S.Lang,
            delegate(string r) { if (r != null && r != S.Lang) { S.Lang = r; ApplyAll(); } });
    }

    void PickView() {
        Dialogs.Choose(this, L.T("wel_view"), new string[][] {
            new string[] { "system", L.T("theme_system") }, new string[] { "light", L.T("theme_light") }, new string[] { "dark", L.T("theme_dark") } }, S.Theme,
            delegate(string r) { if (r != null && r != S.Theme) { S.Theme = r; ApplyAll(); } });
    }

    void OnStart() {
        start.Enabled = false;
        Dialogs.Disclaimer(this, false, delegate(bool ok) {
            Agreed = ok;
            if (ok) { S.Agreed = true; S.AgreedVer = Program.AppVersion; }
            DialogResult = ok ? DialogResult.OK : DialogResult.Cancel;
            Close();
        });
    }
}
}
