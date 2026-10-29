using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Obj2x {

// ---------------------------------------------------------------- 애니메이션 시계
// 타이머 하나로 화면의 모든 움직임을 돌린다. 할 일이 없으면 멈춘다.
public static class Anim {
    public static bool Off;          // 셀프 테스트/화면 캡처용: 모든 움직임을 즉시 끝낸다
    static Timer tm;
    static Stopwatch sw = Stopwatch.StartNew();
    static double last;
    static List<Func<float, bool>> jobs = new List<Func<float, bool>>();
    public static bool Mono = Type.GetType("Mono.Runtime") != null;

    // step(dt초)가 false를 돌려주면 목록에서 빠진다
    public static void Add(Func<float, bool> step) {
        if (jobs.Contains(step)) return;
        jobs.Add(step);
        if (tm == null) { tm = new Timer(); tm.Interval = 15; tm.Tick += delegate { Tick(); }; }
        if (!tm.Enabled) { last = sw.Elapsed.TotalSeconds; tm.Start(); }
    }
    static void Tick() {
        double now = sw.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(0.05, Math.Max(0.001, now - last)); last = now;
        Func<float, bool>[] cur = jobs.ToArray();
        foreach (Func<float, bool> j in cur) {
            bool keep;
            try { keep = j(dt); } catch (Exception) { keep = false; }
            if (!keep) jobs.Remove(j);
        }
        if (jobs.Count == 0) tm.Stop();
    }
    public static float EaseOut(float t) { if (t <= 0) return 0; if (t >= 1) return 1; float u = 1 - t; return 1 - u * u * u; }
}

// 목표값으로 부드럽게 따라가는 숫자 (호버 색, 토글 손잡이, 선택 막대 위치 등)
public class Fader {
    public float V, Target;
    public float K = 16f;            // 클수록 빠르다 (약 3/K 초에 거의 도착)
    Control owner; bool children;
    Func<float, bool> job;
    public Fader(Control owner, float v) : this(owner, v, false) { }
    public Fader(Control owner, float v, bool invalidateChildren) {
        this.owner = owner; V = Target = v; children = invalidateChildren;
        job = Step;
    }
    public void Set(float v) { V = Target = v; Inv(); }
    public void To(float t) {
        Target = t;
        if (Anim.Off || owner == null || !owner.IsHandleCreated || !owner.Visible) { V = t; Inv(); return; }
        Anim.Add(job);
    }
    bool Step(float dt) {
        if (owner == null || owner.IsDisposed) return false;
        float d = Target - V;
        float range = Math.Max(1f, Math.Abs(Target) * 0.002f);
        if (Math.Abs(d) < 0.002f * range) { V = Target; Inv(); return false; }
        V += d * (float)(1 - Math.Exp(-dt * K));
        Inv();
        return true;
    }
    void Inv() { if (owner != null && !owner.IsDisposed) owner.Invalidate(children); }
}

// ---------------------------------------------------------------- 페이지 전환 (이전 화면 위로 새 화면이 살짝 떠오르며 나타남)
public class SwapOverlay : Control {
    Bitmap from, to;
    float t; bool done;
    Action finished;
    const float Dur = 0.24f;
    public SwapOverlay(Bitmap from) {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        this.from = from;
    }
    public void Start(Bitmap to, Action finished) {
        this.to = to; this.finished = finished;
        Anim.Add(Step);
    }
    bool Step(float dt) {
        if (IsDisposed) return false;
        t += dt / Dur;
        if (t >= 1) { Finish(); return false; }
        Invalidate();
        return true;
    }
    public void Finish() {
        if (done) return; done = true;
        Action f = finished; finished = null;
        if (Parent != null) Parent.Controls.Remove(this);
        if (from != null) from.Dispose(); if (to != null) to.Dispose();
        from = null; to = null;
        Dispose();
        if (f != null) f();
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        if (from != null) g.DrawImageUnscaled(from, 0, 0); else g.Clear(Theme.Bg);
        if (to == null) return;
        float e2 = Anim.EaseOut(t);
        int dy = (int)Math.Round((1 - e2) * Ui.S(14));
        using (ImageAttributes ia = new ImageAttributes()) {
            ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = Math.Min(1f, e2 * 1.15f);
            ia.SetColorMatrix(cm);
            g.DrawImage(to, new Rectangle(0, dy, to.Width, to.Height), 0, 0, to.Width, to.Height, GraphicsUnit.Pixel, ia);
        }
    }
}

// ---------------------------------------------------------------- 정보 화면 (흐르는 그라데이션 + 큰 제목 + 반투명 항목)
public class AboutHero : Control, IRefresh {
    public Func<string> UpdateValue;          // 업데이트 줄 오른쪽 글자
    public Func<bool> UpdateIsNew;
    public event EventHandler UpdateRowClicked, RepoClicked, DisclaimerClicked;

    Bitmap grad;                               // 작은 그라데이션 (늘려서 그린다)
    const int GW = 48, GH = 32;
    float clock;                               // 흐르는 시간
    float intro = 10f;                         // 등장 애니메이션 경과 시간 (초)
    bool running;
    int hot = -1;
    float[] hov = new float[8];
    List<KeyValuePair<Rectangle, int>> hits = new List<KeyValuePair<Rectangle, int>>();

    public AboutHero() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        grad = new Bitmap(GW, GH, PixelFormat.Format32bppRgb);
    }
    public void RefreshUi() { Invalidate(); }

    public void StartIntro() {
        intro = Anim.Off ? 10f : 0f;
        Run();
        Invalidate();
    }
    void Run() {
        if (running || Anim.Off) return;
        running = true;
        Anim.Add(Step);
    }
    float acc;
    bool Step(float dt) {
        if (IsDisposed || !Visible || FindForm() == null || FindForm().WindowState == FormWindowState.Minimized) { running = false; return false; }
        clock += dt; intro += dt;
        bool hovMoving = false;
        for (int i = 0; i < hov.Length; i++) {
            float tg = (i == hot) ? 1f : 0f;
            float d = tg - hov[i];
            if (Math.Abs(d) > 0.01f) { hov[i] += d * (float)(1 - Math.Exp(-dt * 14)); hovMoving = true; } else hov[i] = tg;
        }
        // 그라데이션은 초당 약 30번, 등장/호버 중에는 매 틱
        acc += dt;
        if (acc >= 0.033f || intro < 1.4f || hovMoving) { acc = 0; Invalidate(); }
        return true;
    }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) Run(); }

    // ---- 색
    struct Blob { public Color C; public float X, Y, AX, AY, WX, WY, P, R; }
    Color BaseColor() { return Theme.Dark ? Color.FromArgb(14, 15, 38) : Color.FromArgb(250, 244, 252); }
    Blob[] Blobs() {
        Color[] c;
        string a = Theme.AccentName;
        if (Theme.Dark) {
            if (a == "teal") c = new Color[] { Color.FromArgb(8, 92, 104), Color.FromArgb(22, 54, 150), Color.FromArgb(60, 44, 130), Color.FromArgb(10, 70, 90) };
            else if (a == "rose") c = new Color[] { Color.FromArgb(120, 32, 78), Color.FromArgb(72, 34, 128), Color.FromArgb(28, 30, 96), Color.FromArgb(96, 30, 96) };
            else c = new Color[] { Color.FromArgb(30, 52, 168), Color.FromArgb(96, 40, 146), Color.FromArgb(22, 26, 92), Color.FromArgb(58, 36, 150) };
        } else {
            if (a == "teal") c = new Color[] { Color.FromArgb(178, 238, 222), Color.FromArgb(186, 220, 255), Color.FromArgb(214, 206, 255), Color.FromArgb(200, 244, 236) };
            else if (a == "rose") c = new Color[] { Color.FromArgb(255, 188, 206), Color.FromArgb(255, 216, 192), Color.FromArgb(226, 198, 252), Color.FromArgb(255, 204, 226) };
            else c = new Color[] { Color.FromArgb(255, 192, 218), Color.FromArgb(210, 194, 255), Color.FromArgb(188, 214, 255), Color.FromArgb(240, 200, 248) };
        }
        Blob[] b = new Blob[4];
        float[,] p = new float[,] {
            // X, Y, AX, AY, WX, WY, phase, radius
            { 0.25f, 0.30f, 0.22f, 0.18f, 0.21f, 0.17f, 0.0f, 0.55f },
            { 0.80f, 0.20f, 0.20f, 0.20f, 0.16f, 0.23f, 1.7f, 0.50f },
            { 0.70f, 0.75f, 0.25f, 0.18f, 0.19f, 0.14f, 3.1f, 0.60f },
            { 0.20f, 0.85f, 0.18f, 0.22f, 0.13f, 0.20f, 4.4f, 0.50f } };
        for (int i = 0; i < 4; i++) {
            b[i].C = c[i]; b[i].X = p[i, 0]; b[i].Y = p[i, 1]; b[i].AX = p[i, 2]; b[i].AY = p[i, 3];
            b[i].WX = p[i, 4]; b[i].WY = p[i, 5]; b[i].P = p[i, 6]; b[i].R = p[i, 7];
        }
        return b;
    }

    void RenderGradient() {
        Color bc = BaseColor();
        Blob[] bl = Blobs();
        float t = clock;
        float[] cx = new float[4], cy = new float[4];
        for (int i = 0; i < 4; i++) {
            cx[i] = bl[i].X + bl[i].AX * (float)Math.Sin(t * bl[i].WX * 2.2 + bl[i].P);
            cy[i] = bl[i].Y + bl[i].AY * (float)Math.Cos(t * bl[i].WY * 2.2 + bl[i].P * 1.3);
        }
        Color bg = Theme.Bg;
        BitmapData d = grad.LockBits(new Rectangle(0, 0, GW, GH), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        int[] px = new int[GW * GH];
        float aspect = 1.5f;
        for (int y = 0; y < GH; y++) {
            float fy = y / (float)(GH - 1);
            for (int x = 0; x < GW; x++) {
                float fx = x / (float)(GW - 1);
                float r = bc.R, gg = bc.G, b = bc.B;
                for (int i = 0; i < 4; i++) {
                    float dx = (fx - cx[i]) * aspect, dy = fy - cy[i];
                    float w = (float)Math.Exp(-(dx * dx + dy * dy) / (bl[i].R * bl[i].R * 0.5f)) * 0.9f;
                    r += (bl[i].C.R - r) * w; gg += (bl[i].C.G - gg) * w; b += (bl[i].C.B - b) * w;
                }
                // 아래쪽은 바탕색으로 조금 가라앉힌다 (글자가 잘 보이도록)
                float f = Math.Max(0f, fy - 0.55f) / 0.45f * (Theme.Dark ? 0.55f : 0.35f);
                r += (bg.R - r) * f; gg += (bg.G - gg) * f; b += (bg.B - b) * f;
                px[y * GW + x] = (255 << 24) | ((int)r << 16) | ((int)gg << 8) | (int)b;
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(px, 0, d.Scan0, px.Length);
        grad.UnlockBits(d);
    }

    // ---- 배치
    float Appear(int i) {                       // i번째 요소의 등장 정도 (0~1)
        float t = (intro - 0.08f * i) / 0.55f;
        return Anim.EaseOut(t);
    }
    Color A(Color c, float a) { return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(c.A * a))), c.R, c.G, c.B); }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        RenderGradient();
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using (ImageAttributes ia = new ImageAttributes()) {
            ia.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(grad, new Rectangle(-Width / GW, -Height / GH, Width + 2 * Width / GW, Height + 2 * Height / GH), 0, 0, GW, GH, GraphicsUnit.Pixel, ia);
        }
        g.PixelOffsetMode = PixelOffsetMode.Default;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        hits.Clear();

        Color txt = Theme.Dark ? Color.FromArgb(236, 238, 255) : Color.FromArgb(28, 28, 36);
        Color sub = Theme.Dark ? Color.FromArgb(170, 176, 210) : Color.FromArgb(110, 106, 128);
        int W = Width, H = Height;
        int cw = Math.Min(Ui.S(760), W - Ui.S(64));
        int x0 = (W - cw) / 2;

        // 아래 항목 묶음
        int rh = Ui.S(54), gap = Ui.S(12);
        int[] groups = new int[] { 2, 1, 1 };
        int listH = 0; foreach (int n in groups) listH += n * rh + gap; listH -= gap;
        int listY = Math.Max(Ui.S(300), H - listH - Ui.S(36));
        int heroH = listY;

        using (Font fT = new Font(Ui.Family, 40f * Ui.FZ, FontStyle.Bold))
        using (Font fV = new Font(Ui.Family, 10f * Ui.FZ))
        using (Font fD = new Font(Ui.Family, 10f * Ui.FZ))
        using (Font fB = new Font(Ui.Family, 10.5f * Ui.FZ, FontStyle.Bold))
        using (Font fR = new Font(Ui.Family, 10.5f * Ui.FZ))
        using (Font fRv = new Font(Ui.Family, 9.5f * Ui.FZ))
        using (StringFormat c = new StringFormat()) {
            c.Alignment = StringAlignment.Center; c.LineAlignment = StringAlignment.Center;
            c.FormatFlags = StringFormatFlags.NoWrap; c.Trimming = StringTrimming.EllipsisCharacter;
            int tH = (int)fT.GetHeight(g), vH = (int)fV.GetHeight(g), dH = (int)fD.GetHeight(g);
            bool pill = UpdateIsNew != null && UpdateIsNew();
            string pillText = L.T("about_pill_update");
            int bH = Ui.S(46), bW = Math.Max(Ui.S(220), (int)g.MeasureString(pillText, fB).Width + Ui.S(64));
            int blockH = tH + Ui.S(6) + vH + Ui.S(10) + dH + (pill ? Ui.S(40) + bH : 0);
            int y = Math.Max(Ui.S(28), (heroH - blockH) / 2 - Ui.S(6));
            float a; int off;

            a = Appear(0); off = (int)((1 - a) * Ui.S(22));
            using (SolidBrush br = new SolidBrush(A(txt, a))) g.DrawString("obj2x", fT, br, new RectangleF(0, y + off, W, tH), c);
            y += tH + Ui.S(6);
            a = Appear(1); off = (int)((1 - a) * Ui.S(18));
            using (SolidBrush br = new SolidBrush(A(sub, a))) g.DrawString("v" + Program.AppVersion, fV, br, new RectangleF(0, y + off, W, vH), c);
            y += vH + Ui.S(10);
            using (SolidBrush br = new SolidBrush(A(sub, a))) g.DrawString(L.T("about_desc"), fD, br, new RectangleF(x0, y + off, cw, dH), c);
            y += dH + Ui.S(40);

            if (pill) {
                a = Appear(2); off = (int)((1 - a) * Ui.S(16));
                Rectangle br0 = new Rectangle((W - bW) / 2, y + off, bW, bH);
                Glass(g, br0, Ui.S(14), a, hov[0], true);
                using (SolidBrush b = new SolidBrush(A(txt, a))) g.DrawString(pillText, fB, b, br0, c);
                hits.Add(new KeyValuePair<Rectangle, int>(br0, 0));
            }

            // 항목들
            string[][] rows = new string[][] {
                new string[] { L.T("about_row_version"), "v" + Program.AppVersion, "" },
                new string[] { L.T("about_row_update"), UpdateValue != null ? UpdateValue() : "", "1" },
                new string[] { L.T("about_row_repo"), Program.Repo, "2" },
                new string[] { L.T("btn_disclaimer"), "", "3" } };
            int ry = listY, ri = 0;
            using (StringFormat l = new StringFormat())
            using (StringFormat r = new StringFormat()) {
                l.LineAlignment = StringAlignment.Center; l.FormatFlags = StringFormatFlags.NoWrap; l.Trimming = StringTrimming.EllipsisCharacter;
                r.LineAlignment = StringAlignment.Center; r.Alignment = StringAlignment.Far; r.FormatFlags = StringFormatFlags.NoWrap; r.Trimming = StringTrimming.EllipsisCharacter;
                for (int gi = 0; gi < groups.Length; gi++) {
                    a = Appear(3 + gi); off = (int)((1 - a) * Ui.S(20));
                    Rectangle gr = new Rectangle(x0, ry + off, cw, groups[gi] * rh);
                    Glass(g, gr, Ui.S(16), a, 0, false);
                    for (int k = 0; k < groups[gi]; k++, ri++) {
                        Rectangle rr = new Rectangle(x0, ry + off + k * rh, cw, rh);
                        string[] row = rows[ri];
                        bool click = row[2].Length > 0;
                        int id = click ? int.Parse(row[2]) : -1;
                        if (click && id >= 0 && hov[id] > 0.01f) {
                            using (GraphicsPath hp = RowPath(rr, gr, Ui.S(16)))
                            using (SolidBrush hb = new SolidBrush(A(Theme.Dark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(18, 40, 30, 80), a * hov[id]))) g.FillPath(hb, hp);
                        }
                        int pad = Ui.S(22);
                        using (SolidBrush b = new SolidBrush(A(txt, a)))
                            g.DrawString(row[0], fR, b, new RectangleF(rr.X + pad, rr.Y, rr.Width / 2f - pad, rr.Height), l);
                        int chev = click ? Ui.S(22) : 0;
                        Color vc = sub;
                        if (id == 1 && UpdateIsNew != null && UpdateIsNew()) vc = Theme.Accent;
                        using (SolidBrush b = new SolidBrush(A(vc, a)))
                            g.DrawString(row[1], fRv, b, new RectangleF(rr.X + rr.Width / 3f, rr.Y, rr.Width * 2 / 3f - pad - chev, rr.Height), r);
                        if (click) {
                            int mx = rr.Right - pad - Ui.S(6), my = rr.Y + rr.Height / 2, s = Ui.S(5);
                            using (Pen p = new Pen(A(sub, a), Math.Max(1.4f, 1.6f * Ui.Z))) {
                                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                                g.DrawLines(p, new Point[] { new Point(mx - s / 2, my - s), new Point(mx + s / 2, my), new Point(mx - s / 2, my + s) });
                            }
                            hits.Add(new KeyValuePair<Rectangle, int>(new Rectangle(rr.X, ry + k * rh, rr.Width, rh), id));
                        }
                        if (k < groups[gi] - 1) using (Pen dp = new Pen(A(Theme.Dark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(22, 0, 0, 40), a)))
                            g.DrawLine(dp, rr.X + pad, rr.Bottom, rr.Right - pad, rr.Bottom);
                    }
                    ry += groups[gi] * rh + gap;
                }
            }
        }
    }

    static GraphicsPath RowPath(Rectangle rr, Rectangle group, int rad) {
        // 묶음의 위/아래 끝 줄이면 그쪽 모서리만 둥글게
        GraphicsPath gp = new GraphicsPath();
        bool top = rr.Y <= group.Y, bot = rr.Bottom >= group.Bottom;
        int d = rad * 2;
        if (top) { gp.AddArc(rr.X, rr.Y, d, d, 180, 90); gp.AddArc(rr.Right - d - 1, rr.Y, d, d, 270, 90); }
        else { gp.AddLine(rr.X, rr.Y, rr.Right - 1, rr.Y); }
        if (bot) { gp.AddArc(rr.Right - d - 1, rr.Bottom - d - 1, d, d, 0, 90); gp.AddArc(rr.X, rr.Bottom - d - 1, d, d, 90, 90); }
        else { gp.AddLine(rr.Right - 1, rr.Bottom, rr.X, rr.Bottom); }
        gp.CloseFigure();
        return gp;
    }

    // 반투명 유리 상자
    void Glass(Graphics g, Rectangle r, int rad, float a, float h, bool button) {
        Color fill, bdr;
        if (Theme.Dark) { fill = Color.FromArgb(button ? 40 + (int)(22 * h) : 30, 255, 255, 255); bdr = Color.FromArgb(button ? 60 : 26, 255, 255, 255); }
        else { fill = Color.FromArgb(button ? 175 + (int)(50 * h) : 222, 255, 255, 255); bdr = Color.FromArgb(button ? 230 : 140, 255, 255, 255); }
        using (GraphicsPath gp = Ui.Round(r.X, r.Y, r.Width - 1, r.Height - 1, rad))
        using (SolidBrush b = new SolidBrush(A(fill, a)))
        using (Pen p = new Pen(A(bdr, a), 1f)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
    }

    int HitAt(Point p) { foreach (KeyValuePair<Rectangle, int> kv in hits) if (kv.Key.Contains(p)) return kv.Value; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) {
        int h = HitAt(e.Location);
        if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Run(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hot = -1; Run(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) {
        int h = HitAt(e.Location);
        EventHandler ev = null;
        if (h == 0 || h == 1) ev = UpdateRowClicked; else if (h == 2) ev = RepoClicked; else if (h == 3) ev = DisclaimerClicked;
        if (ev != null) ev(this, EventArgs.Empty);
        base.OnMouseClick(e);
    }
    protected override void Dispose(bool disposing) { if (disposing && grad != null) { grad.Dispose(); grad = null; } base.Dispose(disposing); }
}
}
