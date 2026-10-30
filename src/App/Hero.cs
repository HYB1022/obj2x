using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Obj2x.UI {

// 정보 화면 앞쪽 (큰 제목 + 새 버전 줄 + 반투명 항목). 뒤쪽 그라데이션은 HeroBackdrop이 따로 그린다.
// 등장 애니메이션이나 호버가 움직일 때만 다시 그리고, 가만히 있으면 아무 일도 하지 않는다.
public class AboutHero : Drawn {
    public static readonly DependencyProperty IsNewProperty = Dp("IsNew", typeof(bool), typeof(AboutHero), false);
    public static readonly DependencyProperty NewVersionProperty = Dp("NewVersion", typeof(string), typeof(AboutHero), "");
    public static readonly DependencyProperty UpdateValueProperty = Dp("UpdateValue", typeof(string), typeof(AboutHero), "");
    public static readonly DependencyProperty BusyProperty = Dp("Busy", typeof(bool), typeof(AboutHero), false);
    public static readonly DependencyProperty UpdateCommandProperty = DependencyProperty.Register("UpdateCommand", typeof(ICommand), typeof(AboutHero));
    public static readonly DependencyProperty RepoCommandProperty = DependencyProperty.Register("RepoCommand", typeof(ICommand), typeof(AboutHero));
    public static readonly DependencyProperty DisclaimerCommandProperty = DependencyProperty.Register("DisclaimerCommand", typeof(ICommand), typeof(AboutHero));
    public bool IsNew { get { return (bool)GetValue(IsNewProperty); } set { SetValue(IsNewProperty, value); } }
    public string NewVersion { get { return (string)GetValue(NewVersionProperty); } set { SetValue(NewVersionProperty, value); } }
    public string UpdateValue { get { return (string)GetValue(UpdateValueProperty); } set { SetValue(UpdateValueProperty, value); } }
    public bool Busy { get { return (bool)GetValue(BusyProperty); } set { SetValue(BusyProperty, value); } }
    public ICommand UpdateCommand { get { return (ICommand)GetValue(UpdateCommandProperty); } set { SetValue(UpdateCommandProperty, value); } }
    public ICommand RepoCommand { get { return (ICommand)GetValue(RepoCommandProperty); } set { SetValue(RepoCommandProperty, value); } }
    public ICommand DisclaimerCommand { get { return (ICommand)GetValue(DisclaimerCommandProperty); } set { SetValue(DisclaimerCommandProperty, value); } }

    double intro = 10, last;                   // 등장 애니메이션 경과 시간(초)
    readonly Stopwatch sw = Stopwatch.StartNew();
    bool running;
    int hot = -1;
    readonly double[] hov = new double[8];
    readonly List<KeyValuePair<Rect, int>> hits = new List<KeyValuePair<Rect, int>>();

    public AboutHero() { IsVisibleChanged += delegate { if (!IsVisible) Halt(); }; }

    public void StartIntro() { intro = Motion.Off ? 10 : 0; Run(); InvalidateVisual(); }
    void Run() {
        if (running || Motion.Off || !IsVisible) return;
        running = true; last = sw.Elapsed.TotalSeconds;
        CompositionTarget.Rendering += Frame;
    }
    void Halt() { if (!running) return; running = false; CompositionTarget.Rendering -= Frame; }
    void Frame(object s, EventArgs e) {
        double now = sw.Elapsed.TotalSeconds, dt = Math.Min(0.05, Math.Max(0.001, now - last)); last = now;
        intro += dt;
        bool moving = intro < 1.4;
        for (int i = 0; i < hov.Length; i++) {
            double tg = (i == hot) ? 1 : 0, d = tg - hov[i];
            if (Math.Abs(d) > 0.01) { hov[i] += d * (1 - Math.Exp(-dt * 14)); moving = true; } else hov[i] = tg;
        }
        InvalidateVisual();
        if (!moving) Halt();
    }
    static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }

    // ---- 배치
    double Appear(int i) { return Motion.EaseOut((intro - 0.08 * i) / 0.55); }

    protected override void OnRender(DrawingContext dc) {
        double W = ActualWidth, H = ActualHeight;
        if (W < 10 || H < 10) return;
        HitArea(dc);
        hits.Clear();

        Color txt = Theme.Dark ? C(236, 238, 255) : C(28, 28, 36);
        Color sub = Theme.Dark ? C(170, 176, 210) : C(110, 106, 128);
        double cw = Math.Min(760, W - 64), x0 = (W - cw) / 2;

        // 아래 항목 묶음
        double rh = 54, gap = 12;
        int[] groups = new int[] { 2, 1, 1 };
        double listH = 0; foreach (int n in groups) listH += n * rh + gap; listH -= gap;
        double listY = Math.Max(300, H - listH - 36), heroH = listY;

        double fT = Art.Pt(40), fV = Art.Pt(10), fB = Art.Pt(10.5), fR = Art.Pt(10.5), fRv = Art.Pt(9.5);
        double tH = Art.Ft("Ag", fT, true, txt).Height, vH = Art.Ft("Ag", fV, false, txt).Height;
        bool pill = IsNew;
        double bH = 68;
        double blockH = tH + 6 + vH + 10 + vH + (pill ? 32 + bH : 0);
        double y = Math.Max(28, (heroH - blockH) / 2 - 6);
        double a, off;

        a = Appear(0); off = (1 - a) * 22;
        Art.Text(dc, Art.Ft("obj2x", fT, true, Theme.A(txt, a)), new Rect(0, y + off, W, tH), TextAlignment.Center);
        y += tH + 6;
        a = Appear(1); off = (1 - a) * 18;
        Art.Text(dc, Art.Ft("v" + AppInfo.Version, fV, false, Theme.A(sub, a)), new Rect(0, y + off, W, vH), TextAlignment.Center);
        y += vH + 10;
        Art.Text(dc, Art.Ft(L.T("about_desc"), fV, false, Theme.A(sub, a)), new Rect(x0, y + off, cw, vH), TextAlignment.Center);
        y += vH + 32;

        // 새 버전 줄: [아이콘] 새 버전 vX / 현재 vY ...        [업데이트]
        if (pill) {
            a = Appear(2); off = (1 - a) * 16;
            Rect cr = new Rect(x0, y + off, cw, bH);
            Glass(dc, cr, 16, a, 0, false);
            double pad = 16, isz = 38;
            Art.Icon(dc, "update", cr.X + pad, cr.Y + (bH - isz) / 2, isz, a);
            string bt = L.T(Busy ? "about_upd_installing" : "about_pill_update");
            FormattedText btf = Art.Ft(bt, fB, true, Theme.A(Busy ? sub : (Theme.Dark ? Theme.Mix(Theme.Accent, Colors.White, 0.45) : Theme.Accent), a));
            double bw = btf.Width + 34, bh = 34;
            Rect br0 = new Rect(cr.Right - pad - bw, cr.Y + (bH - bh) / 2, bw, bh);
            double hv = Busy ? 0 : hov[0];
            Color soft = Color.FromArgb((byte)(Theme.Dark ? 56 + 40 * hv : 30 + 30 * hv), Theme.Accent.R, Theme.Accent.G, Theme.Accent.B);
            dc.DrawRoundedRectangle(Theme.Brush(Theme.A(soft, a)), null, br0, bh / 2, bh / 2);
            dc.DrawText(btf, new Point(br0.X + (bw - btf.Width) / 2, br0.Y + (bh - btf.Height) / 2));
            if (!Busy) hits.Add(new KeyValuePair<Rect, int>(new Rect(br0.X, br0.Y - off, br0.Width, br0.Height), 0));
            double tx = cr.X + pad + isz + 14, tw = br0.X - 12 - tx;
            FormattedText t1 = Art.Line(L.T("about_upd_row_title", NewVersion), fB, true, Theme.A(txt, a), tw);
            FormattedText t2 = Art.Line(L.T("about_upd_row_sub", AppInfo.Version), fRv, false, Theme.A(sub, a), tw);
            dc.DrawText(t1, new Point(tx, cr.Y + bH / 2 + 1 - t1.Height));
            dc.DrawText(t2, new Point(tx, cr.Y + bH / 2 + 1));
        }

        // 항목들
        string[][] rows = new string[][] {
            new string[] { L.T("about_row_version"), "v" + AppInfo.Version, "" },
            new string[] { L.T("about_row_update"), UpdateValue ?? "", "1" },
            new string[] { L.T("about_row_repo"), AppInfo.Repo, "2" },
            new string[] { L.T("btn_disclaimer"), "", "3" } };
        double ry = listY; int ri = 0;
        for (int gi = 0; gi < groups.Length; gi++) {
            a = Appear(3 + gi); off = (1 - a) * 20;
            Rect gr = new Rect(x0, ry + off, cw, groups[gi] * rh);
            Glass(dc, gr, 16, a, 0, false);
            for (int k = 0; k < groups[gi]; k++, ri++) {
                Rect rr = new Rect(x0, ry + off + k * rh, cw, rh);
                string[] row = rows[ri];
                bool click = row[2].Length > 0;
                int id = click ? int.Parse(row[2]) : -1;
                if (click && hov[id] > 0.01) {
                    dc.PushClip(new RectangleGeometry(gr, 16, 16));
                    dc.DrawRectangle(Theme.Brush(Theme.A(Theme.Dark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(18, 40, 30, 80), a * hov[id])), null, rr);
                    dc.Pop();
                }
                double pad = 22, chev = click ? 22 : 0;
                Art.Text(dc, Art.Ft(row[0], fR, false, Theme.A(txt, a)), new Rect(rr.X + pad, rr.Y, rr.Width / 2 - pad, rr.Height), TextAlignment.Left);
                Color vc = (id == 1 && IsNew) ? Theme.Accent : sub;
                Art.Text(dc, Art.Ft(row[1], fRv, false, Theme.A(vc, a)), new Rect(rr.X + rr.Width / 3, rr.Y, rr.Width * 2 / 3 - pad - chev, rr.Height), TextAlignment.Right);
                if (click) {
                    double mx = rr.Right - pad - 6, my = rr.Y + rr.Height / 2, s = 5;
                    dc.DrawGeometry(null, Art.RoundPen(Theme.A(sub, a), 1.6), Art.Poly(false, new Point(mx - s / 2, my - s), new Point(mx + s / 2, my), new Point(mx - s / 2, my + s)));
                    hits.Add(new KeyValuePair<Rect, int>(new Rect(rr.X, ry + k * rh, rr.Width, rh), id));
                }
                if (k < groups[gi] - 1)
                    dc.DrawLine(Art.Pen(Theme.A(Theme.Dark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(22, 0, 0, 40), a), 1), new Point(rr.X + pad, rr.Bottom), new Point(rr.Right - pad, rr.Bottom));
            }
            ry += groups[gi] * rh + gap;
        }
    }

    // 반투명 유리 상자
    void Glass(DrawingContext dc, Rect r, double rad, double a, double h, bool button) {
        Color fill, bdr;
        if (Theme.Dark) { fill = Color.FromArgb((byte)(button ? 40 + 22 * h : 30), 255, 255, 255); bdr = Color.FromArgb((byte)(button ? 60 : 26), 255, 255, 255); }
        else { fill = Color.FromArgb((byte)(button ? 175 + 50 * h : 222), 255, 255, 255); bdr = Color.FromArgb((byte)(button ? 230 : 140), 255, 255, 255); }
        dc.DrawRoundedRectangle(Theme.Brush(Theme.A(fill, a)), Art.Pen(Theme.A(bdr, a), 1), new Rect(r.X + 0.5, r.Y + 0.5, r.Width - 1, r.Height - 1), rad, rad);
    }

    int HitAt(Point p) { foreach (KeyValuePair<Rect, int> kv in hits) if (kv.Key.Contains(p)) return kv.Value; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) {
        int h = HitAt(e.GetPosition(this));
        if (h != hot) { hot = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Arrow; if (Motion.Off) { for (int i = 0; i < hov.Length; i++) hov[i] = i == hot ? 1 : 0; InvalidateVisual(); } else Run(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(MouseEventArgs e) { hot = -1; Run(); base.OnMouseLeave(e); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        int h = HitAt(e.GetPosition(this));
        ICommand c = null;
        if (h == 0 || h == 1) c = UpdateCommand; else if (h == 2) c = RepoCommand; else if (h == 3) c = DisclaimerCommand;
        if (c != null) c.Execute(null);
        base.OnMouseLeftButtonUp(e);
    }
}

// 정보 화면 뒤쪽: 흐르는 그라데이션. 48x32 비트맵의 픽셀만 초당 20번 바꾸고,
// 늘려 그리는 것은 WPF가 알아서 한다 (글자/상자는 다시 그리지 않는다).
// 아주 느리게 움직여서 20번이면 충분하고, 창이 최소화되었거나 다른 프로그램을 쓰는 중이면 멈춘다.
public class HeroBackdrop : FrameworkElement {
    readonly WriteableBitmap grad;
    const int GW = 48, GH = 32;
    readonly int[] px = new int[GW * GH];
    double clock, last;
    readonly Stopwatch sw = Stopwatch.StartNew();
    // 화면 프레임마다 깨우는 CompositionTarget.Rendering 대신 초당 20번 타이머 (구독만 해도 WPF가 매 프레임 그리게 된다)
    readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Render);
    Window win;

    public HeroBackdrop() {
        grad = new WriteableBitmap(GW, GH, 96, 96, PixelFormats.Bgr32, null);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        RenderGradient();
        timer.Interval = TimeSpan.FromMilliseconds(50);
        timer.Tick += Tick;
        IsVisibleChanged += delegate { Wake(); };
        Loaded += delegate {
            Theme.Changed += RenderGradient;
            win = Window.GetWindow(this);
            if (win != null) { win.Activated += OnWin; win.Deactivated += OnWin; win.StateChanged += OnWin; }
            Wake();
        };
        Unloaded += delegate {
            Theme.Changed -= RenderGradient;
            if (win != null) { win.Activated -= OnWin; win.Deactivated -= OnWin; win.StateChanged -= OnWin; win = null; }
            timer.Stop();
        };
    }
    void OnWin(object s, EventArgs e) { Wake(); }
    // 보이고, 창이 앞에 있고, 최소화되지 않았을 때만 돈다
    void Wake() {
        bool run = !Motion.Off && IsVisible && win != null && win.IsActive && win.WindowState != WindowState.Minimized;
        if (run && !timer.IsEnabled) { last = sw.Elapsed.TotalSeconds; timer.Start(); }
        else if (!run && timer.IsEnabled) timer.Stop();
    }
    void Tick(object s, EventArgs e) {
        double now = sw.Elapsed.TotalSeconds;
        clock += Math.Min(0.2, now - last); last = now;
        RenderGradient();
    }
    protected override void OnRender(DrawingContext dc) {
        double W = ActualWidth, H = ActualHeight;
        // 가장자리가 어두워지지 않게 한 칸씩 바깥까지 늘려 그린다
        dc.DrawImage(grad, new Rect(-W / GW, -H / GH, W + 2 * W / GW, H + 2 * H / GH));
    }

    static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }
    // ---- 색
    Color BaseColor() { return Theme.Dark ? Color.FromRgb(14, 15, 38) : Color.FromRgb(250, 244, 252); }
    Color[] BlobColors() {
        string a = Theme.AccentName;
        if (Theme.Dark) {
            if (a == "teal") return new Color[] { C(8, 92, 104), C(22, 54, 150), C(60, 44, 130), C(10, 70, 90) };
            if (a == "rose") return new Color[] { C(120, 32, 78), C(72, 34, 128), C(28, 30, 96), C(96, 30, 96) };
            return new Color[] { C(30, 52, 168), C(96, 40, 146), C(22, 26, 92), C(58, 36, 150) };
        }
        if (a == "teal") return new Color[] { C(178, 238, 222), C(186, 220, 255), C(214, 206, 255), C(200, 244, 236) };
        if (a == "rose") return new Color[] { C(255, 188, 206), C(255, 216, 192), C(226, 198, 252), C(255, 204, 226) };
        return new Color[] { C(255, 192, 218), C(210, 194, 255), C(188, 214, 255), C(240, 200, 248) };
    }
    // X, Y, AX, AY, WX, WY, phase, radius
    static readonly double[,] P = new double[,] {
        { 0.25, 0.30, 0.22, 0.18, 0.21, 0.17, 0.0, 0.55 },
        { 0.80, 0.20, 0.20, 0.20, 0.16, 0.23, 1.7, 0.50 },
        { 0.70, 0.75, 0.25, 0.18, 0.19, 0.14, 3.1, 0.60 },
        { 0.20, 0.85, 0.18, 0.22, 0.13, 0.20, 4.4, 0.50 } };

    void RenderGradient() {
        Color bc = BaseColor(), bg = Theme.Bg;
        Color[] col = BlobColors();
        double[] cx = new double[4], cy = new double[4];
        for (int i = 0; i < 4; i++) {
            cx[i] = P[i, 0] + P[i, 2] * Math.Sin(clock * P[i, 4] * 2.2 + P[i, 6]);
            cy[i] = P[i, 1] + P[i, 3] * Math.Cos(clock * P[i, 5] * 2.2 + P[i, 6] * 1.3);
        }
        const double aspect = 1.5;
        for (int y = 0; y < GH; y++) {
            double fy = y / (double)(GH - 1);
            for (int x = 0; x < GW; x++) {
                double fx = x / (double)(GW - 1);
                double r = bc.R, g = bc.G, b = bc.B;
                for (int i = 0; i < 4; i++) {
                    double dx = (fx - cx[i]) * aspect, dy = fy - cy[i];
                    double w = Math.Exp(-(dx * dx + dy * dy) / (P[i, 7] * P[i, 7] * 0.5)) * 0.9;
                    r += (col[i].R - r) * w; g += (col[i].G - g) * w; b += (col[i].B - b) * w;
                }
                // 아래쪽은 바탕색으로 조금 가라앉힌다 (글자가 잘 보이도록)
                double f = Math.Max(0, fy - 0.55) / 0.45 * (Theme.Dark ? 0.55 : 0.35);
                r += (bg.R - r) * f; g += (bg.G - g) * f; b += (bg.B - b) * f;
                px[y * GW + x] = (255 << 24) | ((int)r << 16) | ((int)g << 8) | (int)b;
            }
        }
        grad.WritePixels(new Int32Rect(0, 0, GW, GH), px, GW * 4, 0);
    }
}
}
