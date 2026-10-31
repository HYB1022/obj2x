using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Obj2x.UI;

// ---------------------------------------------------------------- 값을 시간에 따라 바꾸는 작은 엔진 (모든 움직임이 Motion.Scale을 따르게)
// 같은 (주인, 이름)으로 다시 부르면 지금 값에서 새 목표로 이어서 움직인다. 움직이는 것이 있을 때만 타이머가 돈다.
public static class Tween {
    sealed class Item { public object Owner; public string Key; public double From, To, Dur, T; public Action<double> Set; public Func<double, double> Ease; public Action Done; }
    static readonly List<Item> items = new();
    static DispatcherTimer timer;
    static readonly Stopwatch sw = new();
    static double last;

    public static void Run(object owner, string key, double from, double to, double sec, Action<double> set, Func<double, double> ease = null, Action done = null) {
        Stop(owner, key);
        if (sec <= 0 || Motion.Disabled || double.IsNaN(from) || Math.Abs(from - to) < 1e-9) { set(to); done?.Invoke(); return; }
        items.Add(new Item { Owner = owner, Key = key, From = from, To = to, Dur = sec, Set = set, Ease = ease ?? Motion.EaseOut, Done = done });
        set(from);
        // 콜백을 받는 생성자는 바로 시작해 버리므로(시계를 못 맞춤) 따로 만든다
        if (timer == null) { timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / 120) }; timer.Tick += Tick; }
        if (!timer.IsEnabled) { sw.Restart(); last = 0; timer.Start(); }
    }
    public static void Stop(object owner, string key) {
        for (int i = items.Count - 1; i >= 0; i--) if (items[i].Owner == owner && items[i].Key == key) items.RemoveAt(i);
    }
    public static bool Running(object owner, string key) {
        foreach (Item it in items) if (it.Owner == owner && it.Key == key) return true;
        return false;
    }
    static void Tick(object s, EventArgs e) {
        double now = sw.Elapsed.TotalSeconds, dt = Math.Min(0.05, now - last); last = now;
        Item[] snap = items.ToArray();
        foreach (Item it in snap) {
            it.T += dt;
            double t = Math.Min(1, it.T / it.Dur);
            it.Set(it.From + (it.To - it.From) * it.Ease(t));
            if (t >= 1) { items.Remove(it); it.Done?.Invoke(); }
        }
        if (items.Count == 0) timer.Stop();
    }
}

// ---------------------------------------------------------------- XAML 스타일용 애니메이션 (개발자 옵션의 애니메이션 배율을 따른다)
// 스타일 선택자에서 이 첨부 속성만 바꾸고 실제 움직임은 Tween으로 돌린다.
//   <Setter Property="ui:Anim.Opacity" Value="1"/>   → Opacity를 1로 (스타일이 풀리면 0으로)
//   ui:Anim.X → RenderTransform의 TranslateTransform.X,  ui:Anim.Angle → RenderTransform의 RotateTransform.Angle
// 화면에 붙기 전(처음 그릴 때)에는 움직이지 않고 바로 그 값이 된다.
public class Anim : AvaloniaObject {
    public const double Time = 0.18;
    public static readonly AttachedProperty<double> OpacityProperty = AvaloniaProperty.RegisterAttached<Anim, Visual, double>("Opacity", double.NaN);
    public static readonly AttachedProperty<double> XProperty = AvaloniaProperty.RegisterAttached<Anim, Visual, double>("X", double.NaN);
    public static readonly AttachedProperty<double> AngleProperty = AvaloniaProperty.RegisterAttached<Anim, Visual, double>("Angle", double.NaN);
    public static readonly AttachedProperty<bool> PopInProperty = AvaloniaProperty.RegisterAttached<Anim, Popup, bool>("PopIn");
    public static double GetOpacity(Visual d) => d.GetValue(OpacityProperty);
    public static void SetOpacity(Visual d, double v) => d.SetValue(OpacityProperty, v);
    public static double GetX(Visual d) => d.GetValue(XProperty);
    public static void SetX(Visual d, double v) => d.SetValue(XProperty, v);
    public static double GetAngle(Visual d) => d.GetValue(AngleProperty);
    public static void SetAngle(Visual d, double v) => d.SetValue(AngleProperty, v);
    public static bool GetPopIn(Popup d) => d.GetValue(PopInProperty);
    public static void SetPopIn(Popup d, bool v) => d.SetValue(PopInProperty, v);

    static Anim() {
        OpacityProperty.Changed.AddClassHandler<Visual>((v, e) => Go(v, "op", e.NewValue));
        XProperty.Changed.AddClassHandler<Visual>((v, e) => Go(v, "x", e.NewValue));
        AngleProperty.Changed.AddClassHandler<Visual>((v, e) => Go(v, "ang", e.NewValue));
        PopInProperty.Changed.AddClassHandler<Popup>((p, e) => { if (e.NewValue is true) p.Opened += OnPopOpened; else p.Opened -= OnPopOpened; });
    }

    static void Go(Visual v, string kind, object nv) {
        double to = nv is double d && !double.IsNaN(d) ? d : 0;
        Func<double> get; Action<double> set;
        if (kind == "op") { get = () => v.Opacity; set = x => v.Opacity = x; }
        else if (kind == "x") {
            TranslateTransform t = v.RenderTransform as TranslateTransform;
            if (t == null) { t = new TranslateTransform(); v.RenderTransform = t; }
            get = () => t.X; set = x => t.X = x;
        } else {
            RotateTransform t = v.RenderTransform as RotateTransform;
            if (t == null) { t = new RotateTransform(); v.RenderTransform = t; }
            get = () => t.Angle; set = x => t.Angle = x;
        }
        if (!v.IsAttachedToVisualTree() || Motion.Disabled) { Tween.Stop(v, kind); set(to); return; }
        Tween.Run(v, kind, get(), to, Motion.Sec(Time), set);
    }

    // 팝업(드롭다운 목록)이 열릴 때 살짝 내려오며 나타난다
    static void OnPopOpened(object sender, EventArgs e) {
        Control c = ((Popup)sender).Child;
        if (c == null) return;
        TranslateTransform t = c.RenderTransform as TranslateTransform;
        if (t == null) { t = new TranslateTransform(); c.RenderTransform = t; }
        if (Motion.Disabled) { c.Opacity = 1; t.Y = 0; return; }
        Tween.Run(c, "popop", 0, 1, Motion.Sec(0.16), x => c.Opacity = x, t2 => t2);
        Tween.Run(c, "popy", -8, 0, Motion.Sec(0.22), x => t.Y = x);
    }
}

// 둥근 Border 안의 내용을 모서리 반지름대로 잘라 낸다 (카드의 첫/마지막 줄 호버가 네모로 보이지 않게)
public class RoundClip : AvaloniaObject {
    public static readonly AttachedProperty<bool> OnProperty = AvaloniaProperty.RegisterAttached<RoundClip, Border, bool>("On");
    public static bool GetOn(Border d) => d.GetValue(OnProperty);
    public static void SetOn(Border d, bool v) => d.SetValue(OnProperty, v);
    static RoundClip() {
        OnProperty.Changed.AddClassHandler<Border>((b, e) => {
            if (e.NewValue is true) { b.SizeChanged += Update; Apply(b); } else { b.SizeChanged -= Update; b.Clip = null; }
        });
    }
    static void Update(object s, SizeChangedEventArgs e) { Apply((Border)s); }
    static void Apply(Border b) { b.Clip = Art.RoundRect(new Rect(b.Bounds.Size), b.CornerRadius.TopLeft); }
}

// ---------------------------------------------------------------- 누름 효과: 누른 지점에서 원이 퍼진다 (휴대전화의 물결 효과)
// 누르고 있는 동안 원이 커지며 남아 있고, 떼면 빨리 마저 커지면서 사라진다. 움직이는 동안만 타이머가 돈다.
public class RippleFx {
    const double Grow = 0.45, Fade = 0.4;
    sealed class Wave { public Point C; public double Max, T, F; public bool Held = true; public int Key; }
    readonly Visual owner;
    readonly List<Wave> waves = new();
    readonly Stopwatch sw = new();
    DispatcherTimer timer;
    double last;

    public RippleFx(Visual owner) { this.owner = owner; }
    public bool Active => waves.Count > 0;

    // area: 원이 덮어야 하는 영역 (반지름 = 누른 지점에서 가장 먼 모서리까지)
    public void Press(Point p, Rect area, int key) {
        if (Motion.Disabled) return;
        Release();
        double max = 0;
        foreach (Point c in new[] { area.TopLeft, area.TopRight, area.BottomLeft, area.BottomRight }) { Vector d = c - p; max = Math.Max(max, Math.Sqrt(d.X * d.X + d.Y * d.Y)); }
        waves.Add(new Wave { C = p, Max = max, Key = key });
        if (timer == null) { timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / 120) }; timer.Tick += Tick; }
        if (!timer.IsEnabled) { sw.Restart(); last = 0; timer.Start(); }
        owner.InvalidateVisual();
    }
    public void Release() { foreach (Wave w in waves) w.Held = false; }
    public void Stop() { waves.Clear(); timer?.Stop(); }

    void Tick(object s, EventArgs e) {
        double now = sw.Elapsed.TotalSeconds, dt = Math.Min(0.05, now - last); last = now;
        double k = Math.Max(0.05, Motion.Scale);
        for (int i = waves.Count - 1; i >= 0; i--) {
            Wave w = waves[i];
            w.T += dt / k * (w.Held ? 1 : 2.5);
            if (!w.Held) { w.F += dt / k; if (w.F >= Fade) waves.RemoveAt(i); }
        }
        if (waves.Count == 0 || Motion.Disabled) { waves.Clear(); timer.Stop(); }
        owner.InvalidateVisual();
    }

    // 호출하는 쪽이 모양대로 잘라 둔 뒤 부른다. c의 알파가 가장 진할 때의 농도
    public void Draw(DrawingContext dc, Color c, int key) {
        foreach (Wave w in waves) {
            if (w.Key != key) continue;
            double g = 0.06 + 0.94 * Motion.EaseOut(Math.Min(1, w.T / Grow));
            double a = Math.Min(1, w.T / 0.05) * (w.Held ? 1 : Math.Max(0, 1 - w.F / Fade));
            if (a <= 0.004) continue;
            dc.DrawEllipse(Look.Brush(Look.A(c, a)), null, w.C, w.Max * g, w.Max * g);
        }
    }

    // 바탕 위에 겹칠 물결 색: 글자색을 옅게 (강조색 버튼의 흰 글자 → 흰 물결)
    public static Color Tint(Color fg) { return Color.FromArgb((byte)(Look.Dark ? 46 : 34), fg.R, fg.G, fg.B); }
}

// XAML 템플릿에 넣는 물결 층. 템플릿의 주인(버튼 등)을 누르면 퍼진다. 모서리는 CornerRadius로 자른다.
public class Ripple : Control {
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = AvaloniaProperty.Register<Ripple, CornerRadius>(nameof(CornerRadius));
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    static Ripple() { AffectsRender<Ripple>(CornerRadiusProperty); }

    readonly RippleFx fx;
    InputElement src;

    public Ripple() { IsHitTestVisible = false; fx = new RippleFx(this); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); Hook(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); Unhook(); fx.Stop(); }
    void Hook() {
        if (src != null) return;
        src = TemplatedParent as InputElement ?? this.GetVisualParent() as InputElement;
        if (src == null) return;
        src.AddHandler(PointerPressedEvent, Down, RoutingStrategies.Tunnel, true);
        src.AddHandler(PointerReleasedEvent, Up, RoutingStrategies.Tunnel, true);
        src.AddHandler(PointerCaptureLostEvent, Up2, RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
        src.PointerExited += Up3;
    }
    void Unhook() {
        if (src == null) return;
        src.RemoveHandler(PointerPressedEvent, Down);
        src.RemoveHandler(PointerReleasedEvent, Up);
        src.RemoveHandler(PointerCaptureLostEvent, Up2);
        src.PointerExited -= Up3;
        src = null;
    }
    void Down(object s, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Point p = e.GetPosition(this);
        if (p.X < 0 || p.Y < 0 || p.X > Bounds.Width || p.Y > Bounds.Height) return;   // 펼친 목록(팝업)을 누른 것은 무시
        if (InnerControl(e.Source as Visual)) return;                                       // 줄 안의 버튼/드롭다운/입력란을 누른 것은 그쪽 몫
        Press(p);
    }
    void Up(object s, PointerReleasedEventArgs e) { fx.Release(); }
    void Up2(object s, PointerCaptureLostEventArgs e) { fx.Release(); }
    void Up3(object s, PointerEventArgs e) { fx.Release(); }
    // 누른 곳과 주인(src) 사이에 다른 컨트롤(버튼, 입력란, 드롭다운, 직접 그리는 컨트롤 등)이 있는지
    bool InnerControl(Visual d) {
        for (int i = 0; d != null && d != src && i < 64; i++) {
            if ((d is TemplatedControl || d is Drawn) && (d as StyledElement)?.TemplatedParent != src) return true;
            d = d.GetVisualParent();
        }
        return false;
    }
    public void Press(Point p) { fx.Press(p, new Rect(Bounds.Size), 0); }   // --shots에서도 부른다
    public void Release() { fx.Release(); }

    public override void Render(DrawingContext dc) {
        if (!fx.Active) return;
        IBrush fgb = (src as TemplatedControl)?.Foreground;
        Color fg = fgb is ISolidColorBrush sb ? sb.Color : Look.Text;
        Geometry card = CardClip();
        using (card != null ? dc.PushGeometryClip(card) : default(DrawingContext.PushedState?)) {
            using (dc.PushGeometryClip(Art.RoundRect(new Rect(Bounds.Size), CornerRadius.TopLeft))) fx.Draw(dc, RippleFx.Tint(fg), 0);
        }
    }

    // 줄이 둥근 카드의 맨 위/아래에 있으면 카드 모서리 밖으로 나가지 않게, 가까운 둥근 Border 모양으로도 자른다
    Geometry CardClip() {
        Visual d = this.GetVisualParent();
        for (int i = 0; i < 10 && d != null && d is not ScrollViewer; i++, d = d.GetVisualParent()) {
            if (d is not Border b || b.CornerRadius.TopLeft <= 0 || b.TemplatedParent == TemplatedParent) continue;
            Point? o = b.TranslatePoint(new Point(0, 0), this);
            if (o == null) return null;
            return Art.RoundRect(new Rect(o.Value, b.Bounds.Size), b.CornerRadius.TopLeft);
        }
        return null;
    }
}
