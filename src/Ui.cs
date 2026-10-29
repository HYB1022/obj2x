using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Obj2x {

// 언어/테마가 바뀔 때 스스로 다시 그리는 컨트롤
public interface IRefresh { void RefreshUi(); }

public enum FR { Main, Bold, Title, Sub, Mono, H2, Big, Tiny }

public static class Ui {
    public static string Family = "Malgun Gothic";
    // 화면 배율. 창이 크면 1보다 커지고, 글꼴과 여백이 함께 커진다.
    public static float Z = 1f;
    // 윈도우 배율(125%, 150% 등). 글꼴(pt)은 이미 이만큼 커지므로 글꼴 크기에는 Z / D 만 곱한다.
    static float d = 0f;
    public static float D {
        get {
            if (d <= 0f) {
                d = 1f;
                try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) d = Math.Max(1f, g.DpiX / 96f); } catch (Exception) { }
            }
            return d;
        }
    }
    public static float FZ { get { return Z / D; } }
    public static int S(int v) { return (int)Math.Round(v * Z); }
    public static float S(float v) { return v * Z; }
    static Dictionary<FR, Font> fonts = new Dictionary<FR, Font>();

    public static void BuildFonts(bool systemFont) {
        string fam = "Malgun Gothic";
        if (systemFont) { try { fam = SystemFonts.DefaultFont.FontFamily.Name; } catch (Exception) { } }
        Family = fam;
        fonts.Clear();
        float z = FZ;
        fonts[FR.Main] = new Font(fam, 9f * z);
        fonts[FR.Bold] = new Font(fam, 9f * z, FontStyle.Bold);
        fonts[FR.Title] = new Font(fam, 13f * z, FontStyle.Bold);
        fonts[FR.Sub] = new Font(fam, 8.5f * z);
        fonts[FR.Tiny] = new Font(fam, 8f * z, FontStyle.Bold);
        fonts[FR.Mono] = new Font("Consolas", 9f * z);
        fonts[FR.H2] = new Font(fam, 11f * z, FontStyle.Bold);
        fonts[FR.Big] = new Font(fam, 18f * z, FontStyle.Bold);
    }

    public static Font F(FR r) {
        if (fonts.Count == 0) BuildFonts(false);
        return fonts[r];
    }

    public static GraphicsPath Round(float x, float y, float w, float h, float r) {
        GraphicsPath gp = new GraphicsPath();
        float d = r * 2;
        if (d > w) d = w; if (d > h) d = h;
        if (d <= 0) { gp.AddRectangle(new RectangleF(x, y, w, h)); return gp; }
        gp.AddArc(x, y, d, d, 180, 90);
        gp.AddArc(x + w - d, y, d, d, 270, 90);
        gp.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        gp.AddArc(x, y + h - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }

    // 모든 하위 컨트롤을 다시 그리게 한다 (테마/언어 변경 시)
    public static void RefreshAll(Control root) {
        IRefresh r = root as IRefresh;
        if (r != null) r.RefreshUi();
        foreach (Control c in root.Controls) RefreshAll(c);
        root.Invalidate();
    }

    public static int TextW(string s, Font f) {
        return TextRenderer.MeasureText(s, f, new Size(int.MaxValue / 2, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
    }

    // 한국어/영어 중 더 긴 쪽 기준으로 폭을 잡는다 (언어를 바꿔도 글자가 잘리지 않게)
    public static int MaxW(string key, Font f) {
        string save = L.Lang;
        int w = 0;
        try {
            L.Lang = "ko"; w = Math.Max(w, TextW(L.T(key), f));
            L.Lang = "en"; w = Math.Max(w, TextW(L.T(key), f));
        } finally { L.Lang = save; }
        return w;
    }

    public static Color Mix(Color a, Color b, double t) {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}

// ---------------------------------------------------------------- 라벨
public enum LR { Text, Muted, Accent, Ok, Warn, Bad, Banner }

public class Lbl : Label, IRefresh {
    int wrapW;
    // 폭을 정해 두면 그 폭에 맞춰 줄바꿈하고, 글자 높이에 맞게 상자 크기를 다시 잡는다.
    public int WrapW { get { return wrapW; } set { wrapW = value; Rewrap(); } }
    void Rewrap() {
        if (wrapW <= 0) return;
        AutoSize = false;
        Size s = TextRenderer.MeasureText(Text, Font, new Size(wrapW, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        Size = new Size(wrapW, s.Height + 2);
    }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Rewrap(); }
    protected override void ScaleControl(SizeF f, BoundsSpecified bs) { if (wrapW > 0) wrapW = (int)Math.Round(wrapW * f.Width); base.ScaleControl(f, bs); }
    public LR Role = LR.Text;
    public FR FontRole = FR.Main;
    public string Key;
    public object[] Args;
    public Lbl() { AutoSize = true; BackColor = Color.Transparent; UseMnemonic = false; }
    public Lbl(string text, FR f, LR r) : this() { Text = text; FontRole = f; Role = r; RefreshUi(); }
    public static Lbl K(string key, FR f, LR r) { Lbl l = new Lbl(); l.Key = key; l.FontRole = f; l.Role = r; l.RefreshUi(); return l; }
    public void RefreshUi() {
        if (Key != null) Text = (Args != null) ? L.T(Key, Args) : L.T(Key);
        Font = Ui.F(FontRole);
        Rewrap();
        switch (Role) {
            case LR.Muted: ForeColor = Theme.Muted; break;
            case LR.Accent: ForeColor = Theme.Accent; break;
            case LR.Ok: ForeColor = Theme.Ok; break;
            case LR.Warn: ForeColor = Theme.Warn; break;
            case LR.Bad: ForeColor = Theme.Bad; break;
            case LR.Banner: ForeColor = Theme.BnText; break;
            default: ForeColor = Theme.Text; break;
        }
    }
}

// ---------------------------------------------------------------- 카드
public class Card : Panel, IRefresh {
    public Card() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }
    public void RefreshUi() { BackColor = Theme.Card; }
    protected override void OnPaintBackground(PaintEventArgs e) {
        Color outer = (Parent != null) ? Parent.BackColor : Theme.Bg;
        if (outer.A < 255) outer = Theme.Bg;
        e.Graphics.Clear(outer);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath gp = Ui.Round(0.5f, 0.5f, Width - 2, Height - 2, Ui.S(10f)))
        using (SolidBrush b = new SolidBrush(Theme.Card))
        using (Pen p = new Pen(Theme.CardBdr, 1)) {
            e.Graphics.FillPath(b, gp);
            e.Graphics.DrawPath(p, gp);
        }
    }
}

// 일반 패널 (페이지 배경). 테마를 따라 배경색이 바뀐다.
public class Page : Panel, IRefresh {
    public Page() { BackColor = Theme.Bg; DoubleBuffered = true; }
    public void RefreshUi() { BackColor = Theme.Bg; }
}

public class Div : Control, IRefresh {
    public Div() { Height = 1; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true); }
    public void RefreshUi() { Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) { using (SolidBrush b = new SolidBrush(Theme.Divider)) e.Graphics.FillRectangle(b, 0, 0, Width, Height); }
}

// ---------------------------------------------------------------- 버튼
public class RButton : Control, IRefresh {
    public bool Primary;
    public int Radius = 8;
    public string Key;
    public bool Banner;
    public FR FontRole = FR.Main;
    bool hover, down;
    Fader hv;
    public RButton() {
        hv = new Fader(this, 0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        Size = new Size(Ui.S(100), Ui.S(36));
    }
    public static RButton K(string key, bool primary, int height) {
        RButton b = new RButton();
        b.Key = key; b.Primary = primary; b.Height = Ui.S(height);
        b.RefreshUi();
        b.Width = Ui.MaxW(key, b.Font) + Ui.S(36);
        return b;
    }
    public void RefreshUi() {
        if (Key != null) Text = L.T(Key);
        Font = Ui.F(FontRole);
        Invalidate();
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; hv.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; hv.To(0); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; hv.To(1.6f); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; hv.To(hover ? 1 : 0); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnClick(EventArgs e) { if (Enabled) base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg, fg;
        if (!Enabled) { bg = Theme.DisBg; fg = Theme.DisText; }
        else {
            float h = Math.Max(0f, Math.Min(1.6f, hv.V));
            Color b0, b1;
            if (Banner) { b0 = Theme.BnBtnBg; b1 = Theme.BnBtnHov; fg = Theme.BnBtnFg; }
            else if (Primary) { b0 = Theme.Accent; b1 = Theme.AccentHov; fg = Color.White; }
            else { b0 = Theme.Btn; b1 = Theme.BtnHov; fg = Theme.Text; }
            bg = h <= 1 ? Ui.Mix(b0, b1, h) : Ui.Mix(b1, Ui.Mix(b1, fg, 0.12), h - 1);
        }
        using (GraphicsPath gp = Ui.Round(0, 0, Width - 1, Height - 1, Ui.S((float)Radius)))
        using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, gp);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

// ---------------------------------------------------------------- 토글
public class Toggle : Control, IRefresh {
    bool chk;
    Fader kn;
    public event EventHandler CheckedChanged;
    public Toggle() {
        kn = new Fader(this, 0); kn.K = 18;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Size = new Size(Ui.S(46), Ui.S(24)); Cursor = Cursors.Hand; TabStop = false;
    }
    public bool Checked {
        get { return chk; }
        set { if (chk != value) { chk = value; kn.To(chk ? 1 : 0); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
    }
    public void RefreshUi() { Invalidate(); }
    protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        int d = Height - 4;
        using (GraphicsPath gp = Ui.Round(2, 2, Width - 5, d, d / 2f))
        using (SolidBrush b = new SolidBrush(Ui.Mix(Theme.ToggleOff, Theme.Accent, Math.Max(0f, Math.Min(1f, kn.V))))) g.FillPath(b, gp);
        float m = Height * 5 / 24f;
        float kd = Height - 2 * m;
        float x0 = m + 1, x1 = Width - kd - m - 1;
        float kx = x0 + (x1 - x0) * kn.V;
        using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, kx, m, kd, kd);
    }
}

// 체크 표시 + 글자 (고급 페이지의 OmsiXConv 옵션용)
public class Check : Control, IRefresh {
    bool chk;
    Fader fv;
    public event EventHandler CheckedChanged;
    public Check() {
        fv = new Fader(this, 0); fv.K = 22;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Cursor = Cursors.Hand; Height = Ui.S(24); TabStop = false;
    }
    public bool Checked { get { return chk; } set { if (chk != value) { chk = value; fv.To(chk ? 1 : 0); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } } }
    public void RefreshUi() { Font = Ui.F(FR.Main); Width = Ui.TextW(Text, Font) + Ui.S(34); Invalidate(); }
    protected override void OnClick(EventArgs e) { if (Enabled) Checked = !Checked; base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Height / 24f;
        int y = (int)((Height - 18 * k) / 2);
        using (GraphicsPath gp = Ui.Round(1, y, 17 * k, 17 * k, 4 * k)) {
            float v = Math.Max(0f, Math.Min(1f, fv.V));
            using (SolidBrush b = new SolidBrush(Ui.Mix(Theme.Card, Theme.Accent, v))) g.FillPath(b, gp);
            using (Pen p = new Pen(Ui.Mix(Theme.Muted, Theme.Accent, v), 1.5f)) g.DrawPath(p, gp);
        }
        if (fv.V > 0.05f) {
            using (Pen p = new Pen(Color.FromArgb((int)(255 * Math.Min(1f, fv.V)), Color.White), 2f)) { g.DrawLine(p, 5 * k, y + 9 * k, 8 * k, y + 12 * k); g.DrawLine(p, 8 * k, y + 12 * k, 14 * k, y + 5 * k); }
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(26 * k), 0, Width - (int)(26 * k), Height), Enabled ? Theme.Text : Theme.DisText, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

// ---------------------------------------------------------------- 세그먼트 버튼
public class Segmented : Control, IRefresh {
    public class Item { public string Value; public string Key; public bool Raw; public int W; }
    List<Item> items = new List<Item>();
    string sel = "";
    Fader px, pw;
    bool placed;
    public event EventHandler Changed;
    public Segmented() {
        px = new Fader(this, 0); pw = new Fader(this, 0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Height = Ui.S(32); Cursor = Cursors.Hand; TabStop = false;
    }
    public string Value { get { return sel; } set { if (sel != value) { sel = value; MovePill(); } } }
    void MovePill() {
        int x = 2;
        foreach (Item it in items) { if (it.Value == sel) { if (!placed) { px.Set(x); pw.Set(it.W); placed = true; } else { px.To(x); pw.To(it.W); } return; } x += it.W; }
        Invalidate();
    }
    public void Add(string value, string key, bool raw) {
        Item it = new Item(); it.Value = value; it.Key = key; it.Raw = raw;
        Font f = Ui.F(FR.Main);
        it.W = (raw ? Ui.TextW(key, f) : Ui.MaxW(key, f)) + Ui.S(28);
        items.Add(it);
        int w = 0; foreach (Item i in items) w += i.W;
        Width = w + 4;
    }
    protected override void ScaleControl(SizeF f, BoundsSpecified bs) {
        foreach (Item it in items) it.W = (int)Math.Round(it.W * f.Width);
        placed = false;
        base.ScaleControl(f, bs);
        int w = 0; foreach (Item i in items) w += i.W; Width = w + 4;
    }
    public void RefreshUi() { Invalidate(); }
    string Label(Item it) { return it.Raw ? it.Key : L.T(it.Key); }
    protected override void OnMouseClick(MouseEventArgs e) {
        int x = 2;
        foreach (Item it in items) {
            if (e.X >= x && e.X < x + it.W) {
                if (sel != it.Value) { sel = it.Value; MovePill(); if (Changed != null) Changed(this, EventArgs.Empty); }
                return;
            }
            x += it.W;
        }
        base.OnMouseClick(e);
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath gp = Ui.Round(0.5f, 0.5f, Width - 2, Height - 2, Ui.S(8f)))
        using (SolidBrush b = new SolidBrush(Theme.Btn)) g.FillPath(b, gp);
        if (!placed) { placed = true; MovePill(); placed = true; int x0 = 2; foreach (Item it in items) { if (it.Value == sel) { px.Set(x0); pw.Set(it.W); } x0 += it.W; } }
        if (pw.V > 1)
            using (GraphicsPath gp = Ui.Round(px.V, 2, pw.V, Height - 5, Ui.S(6f)))
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillPath(b, gp);
        int x = 2;
        foreach (Item it in items) {
            bool on = it.Value == sel && Math.Abs(px.V - x) < it.W / 2f;
            TextRenderer.DrawText(g, Label(it), Ui.F(on ? FR.Bold : FR.Main), new Rectangle(x, 0, it.W, Height), on ? Color.White : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            x += it.W;
        }
    }
}

// ---------------------------------------------------------------- 입력 상자
public class Tx : TextBox, IRefresh {
    public Tx() { BorderStyle = BorderStyle.FixedSingle; }
    public void RefreshUi() { BackColor = ReadOnly && Multiline ? Theme.Input : Theme.Input; ForeColor = Theme.Text; Font = Multiline && ReadOnly ? Ui.F(FR.Mono) : Ui.F(FR.Main); }
}

public class Combo : ComboBox, IRefresh {
    public Combo() { DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; }
    public void RefreshUi() { BackColor = Theme.Input; ForeColor = Theme.Text; Font = Ui.F(FR.Main); }
}

public class Num : NumericUpDown, IRefresh {
    public Num() { BorderStyle = BorderStyle.FixedSingle; }
    public void RefreshUi() { BackColor = Theme.Input; ForeColor = Theme.Text; Font = Ui.F(FR.Main); }
}

// ---------------------------------------------------------------- 상태 점 (옆 글자의 세로 가운데에 맞춰 그린다)
public enum DotState { Ok, Warn, Busy }
public class Dot : Control, IRefresh {
    DotState st = DotState.Warn;
    public Lbl Beside;                       // 이 라벨의 높이 가운데에 맞춘다
    public Dot() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; TabStop = false;
    }
    public DotState State { get { return st; } set { if (st != value) { st = value; Invalidate(); } } }
    public void RefreshUi() { Align(); Invalidate(); }
    public void Align() {
        if (Beside == null) return;
        int d = Ui.S(10);
        SetBounds(Beside.Left - d - Ui.S(8), Beside.Top, d + 2, Math.Max(d + 2, Beside.Height));
    }
    protected override void OnPaint(PaintEventArgs e) {
        Color c = st == DotState.Ok ? Theme.Ok : (st == DotState.Busy ? Color.FromArgb(255, 152, 0) : Theme.Warn);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float d = Ui.S(10);
        using (SolidBrush b = new SolidBrush(c)) e.Graphics.FillEllipse(b, 1, (Height - d) / 2f, d, d);
    }
}

// ---------------------------------------------------------------- 얇은 진행 막대
public class SlimBar : Control, IRefresh {
    double v;
    public SlimBar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Height = 4; }
    public double Value { get { return v; } set { if (value < 0) value = 0; if (value > 1) value = 1; v = value; Invalidate(); } }
    public void RefreshUi() { Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        using (SolidBrush b = new SolidBrush(Theme.Divider)) e.Graphics.FillRectangle(b, 0, 0, Width, Height);
        using (SolidBrush b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 0, 0, (int)(Width * v), Height);
    }
}

// ---------------------------------------------------------------- 사이드바 메뉴 버튼
public class NavButton : Control, IRefresh {
    public string Page; public string Key; public bool Active;
    public string BadgeKey;                  // 오른쪽 알약 표시 문구 키 (새 버전 알림 등). null이면 없음
    bool hover;
    Fader hv;
    public NavButton() {
        hv = new Fader(this, 0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Size = new Size(Ui.S(190), Ui.S(42)); Cursor = Cursors.Hand; TabStop = false;
    }
    public void RefreshUi() { Text = L.T(Key); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; hv.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; hv.To(0); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(2, 2, Width - 4, Height - 4);
        SidePanel sp = Parent as SidePanel;
        bool pillHere = sp != null && sp.PillOver(this);
        if (!pillHere && hv.V > 0.01f) {
            using (GraphicsPath gp = Ui.Round(r.X, r.Y, r.Width, r.Height, Ui.S(12f)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1f, hv.V)), Theme.Btn))) g.FillPath(b, gp);
        }
        int isz = Ui.S(24), ix = r.X + Ui.S(10);
        Icons.Draw(g, Page, ix, r.Y + (r.Height - isz) / 2f, isz, 1f);
        int tx = ix + isz + Ui.S(12);
        int right = r.Right - Ui.S(8);
        if (BadgeKey != null) {
            // [업데이트] 처럼 연한 강조색 바탕 + 강조색 글자
            string bt = L.T(BadgeKey);
            Font bf = Ui.F(FR.Sub);
            int bw = Ui.TextW(bt, bf) + Ui.S(20), bh = Ui.S(24);
            Rectangle br = new Rectangle(right - bw, r.Y + (r.Height - bh) / 2, bw, bh);
            Color soft = Theme.Dark ? Color.FromArgb(70, Theme.Accent) : Color.FromArgb(34, Theme.Accent);
            Color fg = Theme.Dark ? Ui.Mix(Theme.Accent, Color.White, 0.45) : Theme.Accent;
            using (GraphicsPath gp = Ui.Round(br.X, br.Y, br.Width, br.Height, bh / 2f))
            using (SolidBrush b = new SolidBrush(soft)) g.FillPath(b, gp);
            TextRenderer.DrawText(g, bt, bf, br, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            right = br.X - Ui.S(6);
        }
        TextRenderer.DrawText(g, Text, Active ? Ui.F(FR.Bold) : Ui.F(FR.Main), new Rectangle(tx, r.Y, Math.Max(0, right - tx), r.Height), Active ? Theme.NavPillText : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// ---------------------------------------------------------------- 마법사 단계 표시줄
public class Stepper : Control, IRefresh {
    public int Step = 1;
    static string[] keys = new string[] { "step_1", "step_2", "step_3", "step_4" };
    public Stepper() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true); BackColor = Color.Transparent; Height = Ui.S(58); }
    public void RefreshUi() { Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        int s12 = Ui.S(12), s24 = 2 * s12, cy = Ui.S(18);
        using (Pen pl = new Pen(Theme.Divider, 2)) g.DrawLine(pl, Ui.S(40), cy, Width - Ui.S(40), cy);
        int cnt = 4;
        float usable = Math.Max(Ui.S(240), Width - Ui.S(96));
        float span = usable / (cnt - 1);
        for (int i = 0; i < cnt; i++) {
            int cx = (int)(Ui.S(48) + i * span);
            bool done = (i + 1 < Step), active = (i + 1 == Step);
            using (SolidBrush b = new SolidBrush((active || done) ? Theme.Accent : Theme.Card)) g.FillEllipse(b, cx - s12, cy - s12, s24, s24);
            using (Pen p = new Pen((active || done) ? Theme.Accent : Theme.Divider, 2)) g.DrawEllipse(p, cx - s12, cy - s12, s24, s24);
            string num = done ? "✓" : (i + 1).ToString();
            TextRenderer.DrawText(g, num, Ui.F(FR.Bold), new Rectangle(cx - s12, cy - s12, s24, s24), (active || done) ? Color.White : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            string t = L.T(keys[i]);
            int tw = Ui.TextW(t, Ui.F(FR.Sub)) + Ui.S(12);
            int lx = Math.Max(0, Math.Min(Width - tw, cx - tw / 2));
            TextRenderer.DrawText(g, t, Ui.F(FR.Sub), new Rectangle(lx, cy + Ui.S(13), tw, Ui.S(22)), active ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

// ---------------------------------------------------------------- 마법사 종류 선택 카드
public class RadioCard : Control, IRefresh {
    public string Mode; public string TitleKey, SubKey, BadgeKey;
    bool selected;
    public bool Selected { get { return selected; } set { if (selected != value) { selected = value; sv.To(value ? 1 : 0); } } }
    public event EventHandler Picked;
    bool hover;
    Fader hv, sv;
    public RadioCard() {
        hv = new Fader(this, 0); sv = new Fader(this, 0);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent; Height = Ui.S(72); Cursor = Cursors.Hand; TabStop = false;
    }
    public void RefreshUi() { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { RefreshUi(); base.OnEnabledChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; hv.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; hv.To(0); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { if (Enabled && Picked != null) Picked(this, EventArgs.Empty); base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = Math.Max(0f, Math.Min(1f, sv.V)), h = Enabled ? Math.Max(0f, Math.Min(1f, hv.V)) : 0f;
        Color bdr = Ui.Mix(Ui.Mix(Theme.CardBdr, Theme.Muted, h), Theme.Accent, s);
        float k = Height / 72f;
        using (GraphicsPath gp = Ui.Round(1, 1, Width - 3, Height - 3, 10 * k)) {
            using (SolidBrush b = new SolidBrush(Ui.Mix(Theme.Card, Theme.NavPill, s))) g.FillPath(b, gp);
            using (Pen p = new Pen(bdr, 1f + s)) g.DrawPath(p, gp);
        }
        Color tc = Enabled ? Theme.Text : Theme.DisText;
        Color sc = Enabled ? Theme.Muted : Theme.DisText;
        using (Pen p = new Pen(Selected ? Theme.Accent : (Enabled ? Theme.Muted : Theme.DisText), 2)) g.DrawEllipse(p, 18 * k, Height / 2 - 9 * k, 18 * k, 18 * k);
        if (s > 0.02f) { float dd = 8 * k * s; using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 27 * k - dd / 2, Height / 2f - dd / 2, dd, dd); }
        int tx = (int)(54 * k);
        string title = L.T(TitleKey);
        TextRenderer.DrawText(g, title, Ui.F(FR.Bold), new Point(tx, (int)(12 * k)), tc, TextFormatFlags.NoPrefix);
        int tw = Ui.TextW(title, Ui.F(FR.Bold));
        if (BadgeKey != null) {
            string bt = L.T(BadgeKey);
            int bw = Ui.TextW(bt, Ui.F(FR.Tiny)) + (int)(16 * k);
            int bx = tx + tw + (int)(10 * k), by = (int)(11 * k), bh = (int)(20 * k);
            Color bc = (Mode == "fast") ? Theme.Warn : Theme.Accent;
            using (GraphicsPath gp = Ui.Round(bx, by, bw, bh, bh / 2f))
            using (SolidBrush b = new SolidBrush(Enabled ? bc : Theme.DisBg)) g.FillPath(b, gp);
            TextRenderer.DrawText(g, bt, Ui.F(FR.Tiny), new Rectangle(bx, by, bw, bh), Enabled ? Color.White : Theme.DisText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
        TextRenderer.DrawText(g, L.T(SubKey), Ui.F(FR.Sub), new Rectangle(tx, (int)(38 * k), Width - tx - (int)(12 * k), (int)(28 * k)), sc, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
    }
}

// ---------------------------------------------------------------- 최근 폴더 목록 (LTBox 스타일)
public class RecentList : Control, IRefresh {
    public List<string> Paths = new List<string>();
    public event EventHandler<PathEventArgs> PathPicked;
    int hot = -1;
    public static int RowH { get { return Ui.S(44); } }
    public RecentList() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent; TabStop = false;
    }
    public void SetPaths(List<string> p) {
        Paths = new List<string>(p);
        Height = Math.Max(1, Paths.Count) * RowH;
        Invalidate();
    }
    public static bool Exists(string p) { try { return System.IO.Directory.Exists(p); } catch (Exception) { return false; } }
    public void RefreshUi() { Invalidate(); }
    protected override void OnMouseMove(MouseEventArgs e) {
        int i = e.Y / Math.Max(1, Height / Math.Max(1, Paths.Count)); if (i >= Paths.Count) i = -1;
        if (i != hot) { hot = i; Cursor = (i >= 0) ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) {
        int i = e.Y / Math.Max(1, Height / Math.Max(1, Paths.Count));
        if (i >= 0 && i < Paths.Count && PathPicked != null) PathPicked(this, new PathEventArgs(Paths[i]));
        base.OnMouseClick(e);
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Paths.Count == 0) {
            TextRenderer.DrawText(g, L.T("lbl_recent_empty"), Ui.F(FR.Sub), new Rectangle(0, 0, Width, Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }
        int RowH = Height / Math.Max(1, Paths.Count);
        float k = RowH / 44f;
        for (int i = 0; i < Paths.Count; i++) {
            int y = i * RowH;
            bool ok = Exists(Paths[i]);
            if (i == hot) using (GraphicsPath gp = Ui.Round(0, y + 1, Width - 1, RowH - 2, 8 * k)) using (SolidBrush b = new SolidBrush(Theme.Btn)) g.FillPath(b, gp);
            // 폴더 아이콘
            Color ic = ok ? Theme.Accent : Theme.Bad;
            using (SolidBrush b = new SolidBrush(ic)) {
                g.FillRectangle(b, 14 * k, y + RowH / 2 - 8 * k, 9 * k, 4 * k);
                g.FillRectangle(b, 14 * k, y + RowH / 2 - 5 * k, 22 * k, 14 * k);
            }
            string path = Paths[i];
            if (ok) {
                TextRenderer.DrawText(g, path, Ui.F(FR.Main), new Rectangle((int)(50 * k), y, Width - (int)(60 * k), RowH), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            } else {
                TextRenderer.DrawText(g, path, Ui.F(FR.Main), new Rectangle((int)(50 * k), y + (int)(4 * k), Width - (int)(60 * k), (int)(20 * k)), Theme.Text, TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(g, L.T("recent_missing"), Ui.F(FR.Sub), new Rectangle((int)(50 * k), y + (int)(23 * k), Width - (int)(60 * k), (int)(18 * k)), Theme.Bad, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
    }
}

public class PathEventArgs : EventArgs {
    public string Path;
    public PathEventArgs(string p) { Path = p; }
}
}
