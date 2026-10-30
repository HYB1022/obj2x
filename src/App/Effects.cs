using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Obj2x.UI {

// ---------------------------------------------------------------- XAML 스타일용 애니메이션 (개발자 옵션의 애니메이션 배율을 따른다)
// XAML 스토리보드는 시간이 고정되므로, 트리거에서는 이 첨부 속성만 바꾸고 실제 움직임은 Motion.D로 돌린다.
//   <Setter TargetName="hov" Property="ui:Anim.Opacity" Value="1"/>   → Opacity를 1로 (값을 지우면 0으로)
//   ui:Anim.X → RenderTransform의 TranslateTransform.X,  ui:Anim.Angle → RenderTransform의 RotateTransform.Angle
// 화면에 붙기 전(처음 그릴 때)에는 움직이지 않고 바로 그 값이 된다.
public static class Anim {
    public const double Time = 0.18;

    public static readonly DependencyProperty OpacityProperty = Reg("Opacity", UIElement.OpacityProperty, null);
    public static readonly DependencyProperty XProperty = Reg("X", TranslateTransform.XProperty, typeof(TranslateTransform));
    public static readonly DependencyProperty AngleProperty = Reg("Angle", RotateTransform.AngleProperty, typeof(RotateTransform));
    public static double GetOpacity(DependencyObject d) { return (double)d.GetValue(OpacityProperty); }
    public static void SetOpacity(DependencyObject d, double v) { d.SetValue(OpacityProperty, v); }
    public static double GetX(DependencyObject d) { return (double)d.GetValue(XProperty); }
    public static void SetX(DependencyObject d, double v) { d.SetValue(XProperty, v); }
    public static double GetAngle(DependencyObject d) { return (double)d.GetValue(AngleProperty); }
    public static void SetAngle(DependencyObject d, double v) { d.SetValue(AngleProperty, v); }

    // 값이 없으면(NaN) 0으로 돌아간다
    static DependencyProperty Reg(string name, DependencyProperty target, Type transform) {
        return DependencyProperty.RegisterAttached(name, typeof(double), typeof(Anim), new PropertyMetadata(double.NaN, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            UIElement el = d as UIElement;
            if (el == null) return;
            double to = double.IsNaN((double)e.NewValue) ? 0 : (double)e.NewValue;
            DependencyObject obj = el;
            if (transform != null) {
                Transform t = el.RenderTransform;
                if (t == null || t.GetType() != transform || t.IsFrozen) { t = (Transform)Activator.CreateInstance(transform); el.RenderTransform = t; }
                obj = t;
            }
            FrameworkElement fe = el as FrameworkElement;
            bool live = fe == null || fe.IsLoaded;
            if (!live || Motion.Disabled) { ((IAnimatable)obj).BeginAnimation(target, null); obj.SetValue(target, to); return; }
            DoubleAnimation a = new DoubleAnimation(to, Motion.D(Time));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            ((IAnimatable)obj).BeginAnimation(target, a);
        }));
    }
}

// ---------------------------------------------------------------- 누름 효과: 누른 지점에서 원이 퍼진다 (휴대전화의 물결 효과)
// 누르고 있는 동안 원이 커지며 남아 있고, 떼면 빨리 마저 커지면서 사라진다.
// 움직이는 동안만 CompositionTarget.Rendering을 구독한다.
public class RippleFx {
    const double Grow = 0.45, Fade = 0.4;
    class Wave { public Point C; public double Max, T, F; public bool Held = true; public int Key; }
    readonly UIElement owner;
    readonly List<Wave> waves = new List<Wave>();
    readonly Stopwatch sw = new Stopwatch();
    double last;
    bool hooked;

    public RippleFx(UIElement owner) { this.owner = owner; }
    public bool Active { get { return waves.Count > 0; } }

    // area: 원이 덮어야 하는 영역 (반지름 = 누른 지점에서 가장 먼 모서리까지)
    public void Press(Point p, Rect area, int key) {
        if (Motion.Disabled) return;
        Release();
        double max = 0;
        foreach (Point c in new Point[] { area.TopLeft, area.TopRight, area.BottomLeft, area.BottomRight }) max = Math.Max(max, (c - p).Length);
        Wave w = new Wave(); w.C = p; w.Max = max; w.Key = key;
        waves.Add(w);
        if (!hooked) { hooked = true; sw.Restart(); last = 0; CompositionTarget.Rendering += Tick; }
        owner.InvalidateVisual();
    }
    public void Release() { foreach (Wave w in waves) w.Held = false; }
    public void Stop() { waves.Clear(); Unhook(); }

    void Unhook() { if (!hooked) return; hooked = false; CompositionTarget.Rendering -= Tick; sw.Stop(); }

    void Tick(object s, EventArgs e) {
        double now = sw.Elapsed.TotalSeconds, dt = Math.Min(0.05, now - last); last = now;
        double k = Math.Max(0.05, Motion.Scale);
        for (int i = waves.Count - 1; i >= 0; i--) {
            Wave w = waves[i];
            w.T += dt / k * (w.Held ? 1 : 2.5);
            if (!w.Held) { w.F += dt / k; if (w.F >= Fade) waves.RemoveAt(i); }
        }
        if (waves.Count == 0 || Motion.Disabled) { waves.Clear(); Unhook(); }
        owner.InvalidateVisual();
    }

    // 호출하는 쪽이 모양대로 잘라(PushClip) 둔 뒤 부른다. c의 알파가 가장 진할 때의 농도
    public void Draw(DrawingContext dc, Color c, int key) {
        foreach (Wave w in waves) {
            if (w.Key != key) continue;
            double g = 0.06 + 0.94 * Motion.EaseOut(Math.Min(1, w.T / Grow));
            double a = Math.Min(1, w.T / 0.05) * (w.Held ? 1 : Math.Max(0, 1 - w.F / Fade));
            if (a <= 0.004) continue;
            dc.DrawEllipse(Theme.Brush(Theme.A(c, a)), null, w.C, w.Max * g, w.Max * g);
        }
    }

    // 바탕 위에 겹칠 물결 색: 글자색을 옅게 (강조색 버튼의 흰 글자 → 흰 물결)
    public static Color Tint(Color fg) { return Color.FromArgb((byte)(Theme.Dark ? 46 : 34), fg.R, fg.G, fg.B); }
}

// XAML 템플릿에 넣는 물결 층. 템플릿의 주인(버튼 등)을 누르면 퍼진다. 모서리는 CornerRadius로 자른다.
public class Ripple : FrameworkElement {
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(Ripple),
        new FrameworkPropertyMetadata(new CornerRadius(0), FrameworkPropertyMetadataOptions.AffectsRender));
    public CornerRadius CornerRadius { get { return (CornerRadius)GetValue(CornerRadiusProperty); } set { SetValue(CornerRadiusProperty, value); } }

    readonly RippleFx fx;
    UIElement src;

    public Ripple() {
        IsHitTestVisible = false;
        fx = new RippleFx(this);
        Loaded += delegate { Hook(); };
        Unloaded += delegate { Unhook(); fx.Stop(); };
    }
    void Hook() {
        if (src != null) return;
        src = TemplatedParent as UIElement ?? VisualTreeHelper.GetParent(this) as UIElement;
        if (src == null) return;
        src.PreviewMouseLeftButtonDown += Down;
        src.PreviewMouseLeftButtonUp += Up;
        src.LostMouseCapture += Up;
        src.MouseLeave += Up;
    }
    void Unhook() {
        if (src == null) return;
        src.PreviewMouseLeftButtonDown -= Down;
        src.PreviewMouseLeftButtonUp -= Up;
        src.LostMouseCapture -= Up;
        src.MouseLeave -= Up;
        src = null;
    }
    void Down(object s, MouseButtonEventArgs e) {
        Point p = e.GetPosition(this);
        if (p.X >= 0 && p.Y >= 0 && p.X <= ActualWidth && p.Y <= ActualHeight) Press(p);   // 펼친 목록(팝업)을 누른 것은 무시
    }
    void Up(object s, MouseEventArgs e) { fx.Release(); }
    public void Press(Point p) { fx.Press(p, new Rect(0, 0, ActualWidth, ActualHeight), 0); }   // --shots에서도 부른다
    public void Release() { fx.Release(); }

    protected override void OnRender(DrawingContext dc) {
        if (!fx.Active) return;
        Control c = src as Control;
        SolidColorBrush fg = c != null ? c.Foreground as SolidColorBrush : null;
        double r = CornerRadius.TopLeft;
        Geometry card = CardClip();
        if (card != null) dc.PushClip(card);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), r, r));
        fx.Draw(dc, RippleFx.Tint(fg != null ? fg.Color : Theme.Text), 0);
        dc.Pop();
        if (card != null) dc.Pop();
    }

    // 줄이 둥근 카드의 맨 위/아래에 있으면 카드 모서리 밖으로 나가지 않게, 가까운 둥근 Border 모양으로도 자른다
    Geometry CardClip() {
        DependencyObject d = VisualTreeHelper.GetParent(this);
        for (int i = 0; i < 10 && d != null && !(d is ScrollViewer); i++, d = VisualTreeHelper.GetParent(d)) {
            Border b = d as Border;
            if (b == null || b.CornerRadius.TopLeft <= 0 || b.TemplatedParent == TemplatedParent) continue;
            try {
                Rect br = b.TransformToDescendant(this).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight));
                return new RectangleGeometry(br, b.CornerRadius.TopLeft, b.CornerRadius.TopLeft);
            } catch (InvalidOperationException) { return null; }
        }
        return null;
    }
}
}
