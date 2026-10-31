using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Obj2x.UI;

// 정보 화면 앞쪽 (큰 제목 + 새 버전 줄 + 반투명 항목). 뒤쪽 그라데이션은 HeroBackdrop이 따로 그린다.
// 등장 애니메이션이나 호버가 움직일 때만 다시 그리고, 가만히 있으면 아무 일도 하지 않는다.
public class AboutHero : Drawn {
    public static readonly StyledProperty<bool> IsNewProperty = AvaloniaProperty.Register<AboutHero, bool>(nameof(IsNew));
    public static readonly StyledProperty<string> NewVersionProperty = AvaloniaProperty.Register<AboutHero, string>(nameof(NewVersion), "");
    // 업데이트 줄 오른쪽 글자. 바뀔 때마다 아래에서 올라오며 나타난다 (HyperOS '휴대전화 정보'의 업데이트 표시처럼)
    public static readonly StyledProperty<string> UpdateValueProperty = AvaloniaProperty.Register<AboutHero, string>(nameof(UpdateValue), "");
    public static readonly StyledProperty<ICommand> VersionCommandProperty = AvaloniaProperty.Register<AboutHero, ICommand>(nameof(VersionCommand));
    public static readonly StyledProperty<bool> BusyProperty = AvaloniaProperty.Register<AboutHero, bool>(nameof(Busy));
    public static readonly StyledProperty<ICommand> UpdateCommandProperty = AvaloniaProperty.Register<AboutHero, ICommand>(nameof(UpdateCommand));
    public static readonly StyledProperty<ICommand> RepoCommandProperty = AvaloniaProperty.Register<AboutHero, ICommand>(nameof(RepoCommand));
    public static readonly StyledProperty<ICommand> DisclaimerCommandProperty = AvaloniaProperty.Register<AboutHero, ICommand>(nameof(DisclaimerCommand));
    public bool IsNew { get => GetValue(IsNewProperty); set => SetValue(IsNewProperty, value); }
    public string NewVersion { get => GetValue(NewVersionProperty); set => SetValue(NewVersionProperty, value); }
    public string UpdateValue { get => GetValue(UpdateValueProperty); set => SetValue(UpdateValueProperty, value); }
    public ICommand VersionCommand { get => GetValue(VersionCommandProperty); set => SetValue(VersionCommandProperty, value); }
    public bool Busy { get => GetValue(BusyProperty); set => SetValue(BusyProperty, value); }
    public ICommand UpdateCommand { get => GetValue(UpdateCommandProperty); set => SetValue(UpdateCommandProperty, value); }
    public ICommand RepoCommand { get => GetValue(RepoCommandProperty); set => SetValue(RepoCommandProperty, value); }
    public ICommand DisclaimerCommand { get => GetValue(DisclaimerCommandProperty); set => SetValue(DisclaimerCommandProperty, value); }
    static AboutHero() { AffectsRender<AboutHero>(NewVersionProperty, UpdateValueProperty, BusyProperty); }

    double intro = 10, last;                   // 등장 애니메이션 경과 시간(초)
    double valIn = 1;                          // 업데이트 줄 글자가 올라오는 정도 (0 → 1)
    double btnIn;                              // 가운데 [업데이트] 버튼이 나타난 정도 (0 → 1). 제목 묶음이 그만큼 위로 올라간다
    double BtnTarget => IsNew ? 1 : 0;
    readonly Stopwatch sw = Stopwatch.StartNew();
    bool running;
    int hot = -1;
    readonly double[] hov = new double[8];
    readonly List<KeyValuePair<Rect, int>> hits = new();
    readonly RippleFx fx;

    public AboutHero() { fx = new RippleFx(this); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == IsNewProperty) { if (!Live || Motion.Disabled) btnIn = BtnTarget; else Run(); InvalidateVisual(); }
        if (e.Property == UpdateValueProperty) { valIn = Motion.Disabled ? 1 : 0; Run(); }
    }

    public void StartIntro() { intro = Motion.Disabled ? 10 : 0; Run(); InvalidateVisual(); }
    void Run() {
        if (running || Motion.Disabled || !IsEffectivelyVisible) return;
        running = true; last = sw.Elapsed.TotalSeconds;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Frame);
    }
    void Halt() { running = false; }
    void Frame(TimeSpan ts) {
        if (!running) return;
        if (!IsEffectivelyVisible) { Halt(); fx.Stop(); return; }
        double now = sw.Elapsed.TotalSeconds, dt = Math.Min(0.05, Math.Max(0.001, now - last)); last = now;
        intro += dt / Math.Max(0.05, Motion.Scale);   // 개발자 옵션 애니메이션 배율
        valIn = Math.Min(1, valIn + dt / (0.45 * Math.Max(0.05, Motion.Scale)));
        double bt = BtnTarget, bstep = dt / (0.6 * Math.Max(0.05, Motion.Scale));
        btnIn = bt > btnIn ? Math.Min(bt, btnIn + bstep) : Math.Max(bt, btnIn - bstep);
        bool moving = intro < 1.4 || valIn < 1 || btnIn != bt;
        for (int i = 0; i < hov.Length; i++) {
            double tg = (i == hot) ? 1 : 0, d = tg - hov[i];
            if (Math.Abs(d) > 0.01) { hov[i] += d * (1 - Math.Exp(-dt * 14 / Math.Max(0.05, Motion.Scale))); moving = true; } else hov[i] = tg;
        }
        InvalidateVisual();
        if (!moving) { Halt(); return; }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Frame);
    }
    static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }

    double Appear(int i) { return Motion.EaseOut((intro - 0.08 * i) / 0.55); }

    public override void Render(DrawingContext dc) {
        if (W < 10 || H < 10) return;
        HitArea(dc);
        hits.Clear();

        Color txt = Look.Dark ? C(236, 238, 255) : C(28, 28, 36);
        Color sub = Look.Dark ? C(170, 176, 210) : C(110, 106, 128);
        double cw = Math.Min(760, W - 64), x0 = (W - cw) / 2;

        // 아래 항목 묶음
        double rh = 54, gap = 12;
        int[] groups = { 2, 1, 1 };
        double listH = 0; foreach (int n in groups) listH += n * rh + gap; listH -= gap;
        double listY = Math.Max(300, H - listH - 36), heroH = listY;

        double fT = Art.Pt(40), fV = Art.Pt(10), fR = Art.Pt(10.5), fRv = Art.Pt(9.5);
        double tH = Art.Ft("Ag", fT, true, txt).Height, vH = Art.Ft("Ag", fV, false, txt).Height;
        double blockH = tH + 6 + vH + 10 + vH;
        // 새 버전이 있으면 제목 묶음이 부드럽게 위로 올라가고 그 아래에 [업데이트] 버튼이 생긴다
        if (Motion.Disabled) btnIn = BtnTarget;
        double be = Motion.EaseInOut(btnIn), bH = 62, bR = 22, bw = Math.Min(440, cw * 0.6);
        double bTop = listY - 64 - bH;
        double y0 = Math.Max(28, (heroH - blockH) / 2 - 6), y1 = Math.Max(28, Math.Min(y0, bTop - 56 - blockH));
        double y = y0 + (y1 - y0) * be;
        double a, off;

        a = Appear(0); off = (1 - a) * 22;
        Art.Text(dc, Art.Ft(AppInfo.Name, fT, true, Look.A(txt, a)), new Rect(0, y + off, W, tH), TextAlignment.Center);
        y += tH + 6;
        a = Appear(1); off = (1 - a) * 18;
        Art.Text(dc, Art.Ft("v" + AppInfo.Version, fV, false, Look.A(sub, a)), new Rect(0, y + off, W, vH), TextAlignment.Center);
        y += vH + 10;
        Art.Text(dc, Art.Ft(L.T("about_desc"), fV, false, Look.A(sub, a)), new Rect(x0, y + off, cw, vH), TextAlignment.Center);
        y += vH;
        if (btnIn > 0.001) {
            double ba = a * Motion.EaseOut(btnIn), rise = (1 - be) * 18, h5 = hov[5];
            Rect br = new Rect((W - bw) / 2, bTop + rise, bw, bH);
            // 뒤 그라데이션이 비치는 반투명 흰 판 + 가는 흰 테두리, 큰 글자
            Color bf = Look.Dark ? Color.FromArgb((byte)(26 + 16 * h5), 255, 255, 255) : Color.FromArgb((byte)(150 + 40 * h5), 255, 255, 255);
            Color bb = Look.Dark ? Color.FromArgb(52, 255, 255, 255) : Color.FromArgb(235, 255, 255, 255);
            dc.DrawRectangle(Look.Brush(Look.A(bf, ba)), Art.Pen(Look.A(bb, ba), 1.2), new Rect(br.X + 0.6, br.Y + 0.6, br.Width - 1.2, br.Height - 1.2), bR, bR);
            if (fx.Active) using (dc.PushGeometryClip(Art.RoundRect(br, bR))) fx.Draw(dc, RippleFx.Tint(txt), 5);
            Art.Text(dc, Art.Ft(Busy ? L.T("about_upd_installing") : L.T("about_pill_update"), 17.5, true, Look.A(txt, ba)), br, TextAlignment.Center);
            if (btnIn > 0.5) hits.Add(new KeyValuePair<Rect, int>(br, 5));
        }

        // 항목들
        string[][] rows = {
            new[] { L.T("about_row_version"), "v" + AppInfo.Version, "4" },
            new[] { L.T("about_row_update"), UpdateValue ?? "", "1" },
            new[] { L.T("about_row_repo"), AppInfo.Repo, "2" },
            new[] { L.T("btn_disclaimer"), "", "3" } };
        double ry = listY; int ri = 0;
        for (int gi = 0; gi < groups.Length; gi++) {
            a = Appear(3 + gi); off = (1 - a) * 20;
            Rect gr = new Rect(x0, ry + off, cw, groups[gi] * rh);
            Glass(dc, gr, 16, a);
            for (int k = 0; k < groups[gi]; k++, ri++) {
                Rect rr = new Rect(x0, ry + off + k * rh, cw, rh);
                string[] row = rows[ri];
                bool click = row[2].Length > 0;
                int id = click ? int.Parse(row[2]) : -1;
                bool chevron = click && id != 4;       // 버전 줄은 누를 수 있지만(7번 → 개발자 옵션) 화살표는 없다
                if (click && hov[id] > 0.01)
                    using (dc.PushGeometryClip(Art.RoundRect(gr, 16)))
                        dc.DrawRectangle(Look.Brush(Look.A(Look.Dark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(18, 40, 30, 80), a * hov[id])), null, rr);
                if (click && fx.Active)                // 누른 줄에서 퍼지는 물결
                    using (dc.PushGeometryClip(Art.RoundRect(gr, 16))) using (dc.PushClip(rr)) fx.Draw(dc, RippleFx.Tint(txt), id);
                double pad = 22, chev = chevron ? 22 : 0;
                Art.Text(dc, Art.Ft(row[0], fR, false, Look.A(txt, a)), new Rect(rr.X + pad, rr.Y, rr.Width / 2 - pad, rr.Height), TextAlignment.Left);
                if (id == 1) DrawUpdateValue(dc, row[1], new Rect(rr.X + rr.Width / 3, rr.Y, rr.Width * 2 / 3 - pad - chev, rr.Height), sub, a, fRv);
                else Art.Text(dc, Art.Ft(row[1], fRv, false, Look.A(sub, a)), new Rect(rr.X + rr.Width / 3, rr.Y, rr.Width * 2 / 3 - pad - chev, rr.Height), TextAlignment.Right);
                if (chevron) {
                    double mx = rr.Right - pad - 6, my = rr.Y + rr.Height / 2, s = 5;
                    dc.DrawGeometry(null, Art.RoundPen(Look.A(sub, a), 1.6), Art.Poly(false, new Point(mx - s / 2, my - s), new Point(mx + s / 2, my), new Point(mx - s / 2, my + s)));
                }
                if (click) hits.Add(new KeyValuePair<Rect, int>(new Rect(rr.X, ry + k * rh, rr.Width, rh), id));
                if (k < groups[gi] - 1)
                    dc.DrawLine(Art.Pen(Look.A(Look.Dark ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(22, 0, 0, 40), a), 1), new Point(rr.X + pad, rr.Bottom), new Point(rr.Right - pad, rr.Bottom));
            }
            ry += groups[gi] * rh + gap;
        }
    }

    // 업데이트 줄 값: 새 버전이면 [업데이트] 알약, 아니면 글자. 둘 다 아래에서 올라오며 나타난다.
    void DrawUpdateValue(DrawingContext dc, string text, Rect r, Color sub, double a, double size) {
        double e = Motion.EaseOut(valIn), rise = (1 - e) * 14;
        if (IsNew && !Busy) {
            Color fg = Look.Dark ? Look.Mix(Look.Accent, Colors.White, 0.45) : Look.Accent;
            FormattedText t = Art.Ft(text, size, true, Look.A(fg, a * e));
            double bw = t.Width + 24, bh = 26;
            Rect br = new Rect(r.Right - bw, r.Y + (r.Height - bh) / 2, bw, bh);
            dc.DrawRectangle(Look.Brush(Look.A(Look.Accent, (Look.Dark ? 70 / 255.0 : 34 / 255.0) * a * e)), null, br, bh / 2, bh / 2);
            using (dc.PushGeometryClip(Art.RoundRect(br, bh / 2))) dc.DrawText(t, new Point(br.X + (bw - t.Width) / 2, br.Y + (bh - t.Height) / 2 + rise));
        } else {
            using (dc.PushClip(r)) Art.Text(dc, Art.Ft(text, size, false, Look.A(sub, a * e)), new Rect(r.X, r.Y + rise, r.Width, r.Height), TextAlignment.Right);
        }
    }

    // 반투명 유리 상자
    static void Glass(DrawingContext dc, Rect r, double rad, double a) {
        Color fill, bdr;
        if (Look.Dark) { fill = Color.FromArgb(30, 255, 255, 255); bdr = Color.FromArgb(26, 255, 255, 255); }
        else { fill = Color.FromArgb(222, 255, 255, 255); bdr = Color.FromArgb(140, 255, 255, 255); }
        dc.DrawRectangle(Look.Brush(Look.A(fill, a)), Art.Pen(Look.A(bdr, a), 1), new Rect(r.X + 0.5, r.Y + 0.5, r.Width - 1, r.Height - 1), rad, rad);
    }

    int HitAt(Point p) { foreach (KeyValuePair<Rect, int> kv in hits) if (kv.Key.Contains(p)) return kv.Value; return -1; }
    protected override void OnPointerMoved(PointerEventArgs e) {
        int h = HitAt(e.GetPosition(this));
        if (h != hot) {
            hot = h; Cursor = new Cursor(h >= 0 ? StandardCursorType.Hand : StandardCursorType.Arrow);
            if (Motion.Disabled) { for (int i = 0; i < hov.Length; i++) hov[i] = i == hot ? 1 : 0; InvalidateVisual(); } else Run();
        }
        base.OnPointerMoved(e);
    }
    protected override void OnPointerExited(PointerEventArgs e) { hot = -1; Run(); fx.Release(); base.OnPointerExited(e); }
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        Point p = e.GetPosition(this);
        foreach (KeyValuePair<Rect, int> kv in hits) if (kv.Key.Contains(p)) { fx.Press(p, kv.Key, kv.Value); break; }
        base.OnPointerPressed(e);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        fx.Release();
        int h = HitAt(e.GetPosition(this));
        ICommand c = null;
        if (h == 0 || h == 1 || h == 5) c = UpdateCommand; else if (h == 2) c = RepoCommand; else if (h == 3) c = DisclaimerCommand; else if (h == 4) c = VersionCommand;
        c?.Execute(null);
        base.OnPointerReleased(e);
    }
}

// 정보 화면 뒤쪽: 흐르는 그라데이션. 48x32 비트맵의 픽셀만 매 프레임 바꾸고, 늘려 그리는 것은 그래픽카드가 한다.
// 창이 최소화되었거나 다른 프로그램을 쓰는 중이면 멈춘다.
public class HeroBackdrop : Control {
    const int GW = 48, GH = 32;
    readonly WriteableBitmap grad;
    readonly int[] px = new int[GW * GH];
    double clock, last;
    readonly Stopwatch sw = Stopwatch.StartNew();
    bool running;
    Window win;

    public HeroBackdrop() {
        grad = new WriteableBitmap(new PixelSize(GW, GH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
        RenderGradient();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        Look.Changed += RenderGradient;
        win = TopLevel.GetTopLevel(this) as Window;
        if (win != null) { win.Activated += OnWin; win.Deactivated += OnWin; win.PropertyChanged += OnWinProp; }
        Wake();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnDetachedFromVisualTree(e);
        Look.Changed -= RenderGradient;
        if (win != null) { win.Activated -= OnWin; win.Deactivated -= OnWin; win.PropertyChanged -= OnWinProp; win = null; }
        running = false;
    }
    void OnWin(object s, EventArgs e) { Wake(); }
    void OnWinProp(object s, AvaloniaPropertyChangedEventArgs e) { if (e.Property == Window.WindowStateProperty) Wake(); }
    // 보이고, 창이 앞에 있고, 최소화되지 않았을 때만 돈다
    public void Wake() {
        bool run = !Motion.Disabled && IsEffectivelyVisible && win != null && win.IsActive && win.WindowState != WindowState.Minimized;
        if (run && !running) { running = true; last = sw.Elapsed.TotalSeconds; win.RequestAnimationFrame(Frame); }
        else if (!run) running = false;
    }
    // 개발용(--welcome-frames): 시간을 정해서 그 순간의 그라데이션을 그린다. 그다음부터는 스스로 움직이지 않는다.
    bool seeking;
    public void Seek(double t) { seeking = true; clock = t; RenderGradient(); }
    void Frame(TimeSpan ts) {
        if (!running) return;
        if (!IsEffectivelyVisible) { running = false; return; }
        if (!seeking) {
            double now = sw.Elapsed.TotalSeconds;
            clock += Math.Min(0.1, now - last) / Math.Max(0.05, Motion.Scale); last = now;
            RenderGradient();
        }
        win?.RequestAnimationFrame(Frame);
    }
    public override void Render(DrawingContext dc) {
        double W = Bounds.Width, H = Bounds.Height;
        // 가장자리가 어두워지지 않게 한 칸씩 바깥까지 늘려 그린다
        dc.DrawImage(grad, new Rect(0, 0, GW, GH), new Rect(-W / GW, -H / GH, W + 2 * W / GW, H + 2 * H / GH));
    }

    static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }
    static Color BaseColor() { return Look.Dark ? Color.FromRgb(14, 15, 38) : Color.FromRgb(250, 244, 252); }
    static Color[] BlobColors() {
        string a = Look.AccentName;
        if (Look.Dark) {
            if (a == "teal") return new[] { C(8, 92, 104), C(22, 54, 150), C(60, 44, 130), C(10, 70, 90) };
            if (a == "rose") return new[] { C(120, 32, 78), C(72, 34, 128), C(28, 30, 96), C(96, 30, 96) };
            return new[] { C(30, 52, 168), C(96, 40, 146), C(22, 26, 92), C(58, 36, 150) };
        }
        if (a == "teal") return new[] { C(178, 238, 222), C(186, 220, 255), C(214, 206, 255), C(200, 244, 236) };
        if (a == "rose") return new[] { C(255, 188, 206), C(255, 216, 192), C(226, 198, 252), C(255, 204, 226) };
        return new[] { C(255, 192, 218), C(210, 194, 255), C(188, 214, 255), C(240, 200, 248) };
    }
    // X, Y, AX, AY, WX, WY, phase, radius
    static readonly double[,] P = {
        { 0.25, 0.30, 0.22, 0.18, 0.21, 0.17, 0.0, 0.55 },
        { 0.80, 0.20, 0.20, 0.20, 0.16, 0.23, 1.7, 0.50 },
        { 0.70, 0.75, 0.25, 0.18, 0.19, 0.14, 3.1, 0.60 },
        { 0.20, 0.85, 0.18, 0.22, 0.13, 0.20, 4.4, 0.50 } };

    void RenderGradient() {
        Color bc = BaseColor(), bg = Look.Bg;
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
                double f = Math.Max(0, fy - 0.55) / 0.45 * (Look.Dark ? 0.55 : 0.35);
                r += (bg.R - r) * f; g += (bg.G - g) * f; b += (bg.B - b) * f;
                px[y * GW + x] = (255 << 24) | ((int)r << 16) | ((int)g << 8) | (int)b;
            }
        }
        using (ILockedFramebuffer fb = grad.Lock())
            for (int y = 0; y < GH; y++) System.Runtime.InteropServices.Marshal.Copy(px, y * GW, fb.Address + y * fb.RowBytes, GW);
        InvalidateVisual();
    }
}
