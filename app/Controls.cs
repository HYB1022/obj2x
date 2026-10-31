using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Obj2x.UI;

// 직접 그리는 컨트롤의 공통 부분: 테마/언어가 바뀌면 다시 그리고, 값을 부드럽게 움직인다
public class Drawn : Control {
    bool hooked;
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        if (hooked) return; hooked = true; Look.Changed += OnLook; Loc.Changed += OnLook;
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnDetachedFromVisualTree(e);
        if (!hooked) return; hooked = false; Look.Changed -= OnLook; Loc.Changed -= OnLook;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == IsEffectivelyEnabledProperty) InvalidateVisual();
    }
    void OnLook() { InvalidateMeasure(); InvalidateVisual(); }
    // 값을 부드럽게 바꾼다 (key마다 하나). set이 끝나면 다시 그린다
    protected void Animate(string key, double from, double to, double sec, Action<double> set) {
        Tween.Run(this, key, from, to, Motion.Sec(sec), v => { set(v); InvalidateVisual(); });
    }
    // 빈 곳도 마우스가 닿게
    protected void HitArea(DrawingContext dc) { dc.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size)); }
    protected double W => Bounds.Width;
    protected double H => Bounds.Height;
    protected bool Live => this.IsAttachedToVisualTree();
}

// ---------------------------------------------------------------- 왼쪽 메뉴 줄 (샤오미 태블릿 설정 앱처럼 카드 안의 한 줄)
// 아이콘 타일 + 이름 + 오른쪽 [업데이트] 알약. 선택되면 줄이 강조색으로 옅게 칠해지며 나타난다.
public class NavItem : Drawn {
    public static readonly StyledProperty<string> PageProperty = AvaloniaProperty.Register<NavItem, string>(nameof(Page), "");
    public static readonly StyledProperty<string> KeyProperty = AvaloniaProperty.Register<NavItem, string>(nameof(Key), "");
    public static readonly StyledProperty<string> CurrentProperty = AvaloniaProperty.Register<NavItem, string>(nameof(Current), "");
    public static readonly StyledProperty<string> BadgeProperty = AvaloniaProperty.Register<NavItem, string>(nameof(Badge));
    public static readonly StyledProperty<ICommand> CommandProperty = AvaloniaProperty.Register<NavItem, ICommand>(nameof(Command));
    public string Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    public string Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }
    public string Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public ICommand Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public bool Active => Current == Page;
    static NavItem() { AffectsRender<NavItem>(KeyProperty, BadgeProperty); }

    double hover, sel, badgeIn = 1;
    readonly RippleFx fx;
    public NavItem() { Height = 50; Cursor = new Cursor(StandardCursorType.Hand); fx = new RippleFx(this); }
    Rect Box => new Rect(Bounds.Size);
    const double Rad = 14;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == CurrentProperty || e.Property == PageProperty) {
            if (Live) Animate("sel", sel, Active ? 1 : 0, 0.2, v => sel = v);
            else { Tween.Stop(this, "sel"); sel = Active ? 1 : 0; }
        }
        // 알약이 새로 생기면 글자가 아래에서 올라오며 나타난다
        if (e.Property == BadgeProperty && string.IsNullOrEmpty(e.OldValue as string) && !string.IsNullOrEmpty(e.NewValue as string)) Animate("badge", 0, 1, 0.45, v => badgeIn = v);
    }
    protected override void OnPointerEntered(PointerEventArgs e) { Animate("hov", hover, 1, 0.18, v => hover = v); base.OnPointerEntered(e); }
    protected override void OnPointerExited(PointerEventArgs e) { Animate("hov", hover, 0, 0.18, v => hover = v); fx.Release(); base.OnPointerExited(e); }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) fx.Press(e.GetPosition(this), Box, 0); base.OnPointerPressed(e); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { fx.Release(); if (e.InitialPressMouseButton == MouseButton.Left && Box.Contains(e.GetPosition(this))) Command?.Execute(Page); base.OnPointerReleased(e); }

    public override void Render(DrawingContext dc) {
        HitArea(dc);
        Rect r = Box;
        double s = Math.Max(0, Math.Min(1, sel));
        if (hover * (1 - s) > 0.01) dc.DrawRectangle(Look.Brush(Look.A(Look.Btn, hover * (1 - s))), null, r, Rad, Rad);
        if (s > 0.01) dc.DrawRectangle(Look.Brush(Look.A(Look.NavPill, s)), null, r, Rad, Rad);
        if (fx.Active) using (dc.PushGeometryClip(Art.RoundRect(r, Rad))) fx.Draw(dc, RippleFx.Tint(Look.Text), 0);
        double isz = 30, ix = r.X + 12;
        Art.Icon(dc, Page, ix, r.Y + (r.Height - isz) / 2, isz, 1);
        double tx = ix + isz + 14, right = r.Right - 12;
        if (!string.IsNullOrEmpty(Badge)) {
            double e = Motion.EaseOut(badgeIn);
            Color fg = Look.Dark ? Look.Mix(Look.Accent, Colors.White, 0.45) : Look.Accent;
            FormattedText bf = Art.Ft(L.T(Badge), Art.Pt(8.5), false, Look.A(fg, e));
            double bw = bf.Width + 20, bh = 24;
            Rect br = new Rect(right - bw, r.Y + (r.Height - bh) / 2, bw, bh);
            dc.DrawRectangle(Look.Brush(Look.A(Look.Accent, (Look.Dark ? 70 / 255.0 : 34 / 255.0) * e)), null, br, bh / 2, bh / 2);
            using (dc.PushGeometryClip(Art.RoundRect(br, bh / 2))) dc.DrawText(bf, new Point(br.X + (bw - bf.Width) / 2, br.Y + (bh - bf.Height) / 2 + (1 - e) * 14));
            right = br.X - 6;
        }
        FormattedText t = Art.Line(L.T(Key), 14, false, Look.Mix(Look.Text, Look.NavPillText, s), right - tx);
        dc.DrawText(t, new Point(tx, r.Y + (r.Height - t.Height) / 2));
    }
}

// ---------------------------------------------------------------- 마법사 단계 표시줄
public class Stepper : Drawn {
    public static readonly StyledProperty<int> StepProperty = AvaloniaProperty.Register<Stepper, int>(nameof(Step), 1);
    public int Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    static Stepper() { AffectsRender<Stepper>(StepProperty); }
    static readonly string[] keys = { "step_1", "step_2", "step_3", "step_4" };
    public Stepper() { Height = 58; }
    public override void Render(DrawingContext dc) {
        double cy = 18, r = 12;
        double usable = Math.Max(240, W - 96), span = usable / 3;
        dc.DrawLine(Art.Pen(Look.Divider, 2), new Point(48, cy), new Point(48 + 3 * span, cy));
        if (Step > 1) dc.DrawLine(Art.Pen(Look.Accent, 2), new Point(48, cy), new Point(48 + Math.Min(3, Step - 1) * span, cy));   // 지나온 구간은 강조색
        for (int i = 0; i < 4; i++) {
            double cx = 48 + i * span;
            bool done = (i + 1 < Step), active = (i + 1 == Step);
            Color fill = (active || done) ? Look.Accent : Look.Card;
            dc.DrawEllipse(Look.Brush(fill), Art.Pen((active || done) ? Look.Accent : Look.Divider, 2), new Point(cx, cy), r, r);
            if (done) {                            // 글꼴의 ✓ 대신 둥근 선으로 그린 체크
                dc.DrawGeometry(null, Art.RoundPen(Colors.White, 2.2), Art.Poly(false,
                    new Point(cx - 4.6, cy + 0.3), new Point(cx - 1.4, cy + 3.5), new Point(cx + 4.8, cy - 3.4)));
            } else {
                FormattedText num = Art.Ft((i + 1).ToString(), 12, true, active ? Colors.White : Look.Muted);
                dc.DrawText(num, new Point(cx - num.Width / 2, cy - num.Height / 2));
            }
            FormattedText t = Art.Ft(L.T(keys[i]), Art.Pt(8.5), false, active ? Look.Text : Look.Muted);
            double tw = t.Width + 12;
            double lx = Math.Max(0, Math.Min(W - tw, cx - tw / 2));
            dc.DrawText(t, new Point(lx + 6, cy + 13 + (22 - t.Height) / 2));
        }
    }
}

// ---------------------------------------------------------------- 마법사 변환 방식 카드
public class RadioCard : Drawn {
    public static readonly StyledProperty<string> ModeProperty = AvaloniaProperty.Register<RadioCard, string>(nameof(Mode), "");
    public static readonly StyledProperty<string> TitleKeyProperty = AvaloniaProperty.Register<RadioCard, string>(nameof(TitleKey), "");
    public static readonly StyledProperty<string> SubKeyProperty = AvaloniaProperty.Register<RadioCard, string>(nameof(SubKey), "");
    public static readonly StyledProperty<string> BadgeKeyProperty = AvaloniaProperty.Register<RadioCard, string>(nameof(BadgeKey));
    public static readonly StyledProperty<bool> SelectedProperty = AvaloniaProperty.Register<RadioCard, bool>(nameof(Selected));
    public static readonly StyledProperty<ICommand> CommandProperty = AvaloniaProperty.Register<RadioCard, ICommand>(nameof(Command));
    public string Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public string TitleKey { get => GetValue(TitleKeyProperty); set => SetValue(TitleKeyProperty, value); }
    public string SubKey { get => GetValue(SubKeyProperty); set => SetValue(SubKeyProperty, value); }
    public string BadgeKey { get => GetValue(BadgeKeyProperty); set => SetValue(BadgeKeyProperty, value); }
    public bool Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public ICommand Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    static RadioCard() { AffectsRender<RadioCard>(ModeProperty, TitleKeyProperty, SubKeyProperty, BadgeKeyProperty); }

    double sel, hover;
    readonly RippleFx fx;
    public RadioCard() { Height = 72; Cursor = new Cursor(StandardCursorType.Hand); fx = new RippleFx(this); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == SelectedProperty) Animate("sel", sel, Selected ? 1 : 0, 0.2, v => sel = v);
        if (e.Property == IsEffectivelyEnabledProperty) Cursor = new Cursor(IsEffectivelyEnabled ? StandardCursorType.Hand : StandardCursorType.Arrow);
    }
    protected override void OnPointerEntered(PointerEventArgs e) { Animate("hov", hover, 1, 0.18, v => hover = v); base.OnPointerEntered(e); }
    protected override void OnPointerExited(PointerEventArgs e) { Animate("hov", hover, 0, 0.18, v => hover = v); fx.Release(); base.OnPointerExited(e); }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { if (IsEffectivelyEnabled) fx.Press(e.GetPosition(this), new Rect(Bounds.Size), 0); base.OnPointerPressed(e); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { fx.Release(); if (IsEffectivelyEnabled && Command != null && new Rect(Bounds.Size).Contains(e.GetPosition(this))) Command.Execute(Mode); base.OnPointerReleased(e); }

    public override void Render(DrawingContext dc) {
        double k = H / 72.0;
        bool en = IsEffectivelyEnabled;
        double s = Math.Max(0, Math.Min(1, sel));
        double h = en ? Math.Max(0, Math.Min(1, hover)) : 0;
        Color bdr = Look.Mix(Look.Mix(Look.CardBdr, Look.Muted, h), Look.Accent, s);
        Rect box = new Rect(1, 1, W - 2, H - 2);
        dc.DrawRectangle(Look.Brush(Look.Mix(Look.Card, Look.NavPill, s)), Art.Pen(bdr, 1 + s), box, 10 * k, 10 * k);
        if (fx.Active) using (dc.PushGeometryClip(Art.RoundRect(box, 10 * k))) fx.Draw(dc, RippleFx.Tint(Look.Text), 0);
        Color tc = en ? Look.Text : Look.DisText, sc = en ? Look.Muted : Look.DisText;
        Color rc = Selected ? Look.Accent : (en ? Look.Muted : Look.DisText);
        dc.DrawEllipse(null, Art.Pen(rc, 2), new Point(27 * k, H / 2), 9 * k, 9 * k);
        if (s > 0.02) dc.DrawEllipse(Look.Brush(Look.Accent), null, new Point(27 * k, H / 2), 4 * k * s, 4 * k * s);
        double tx = 54 * k, gap = 8 * k, bh = 20 * k;
        FormattedText title = Art.Ft(L.T(TitleKey), 12, true, tc);
        FormattedText sub = Art.Ft(L.T(SubKey), Art.Pt(8.5), false, sc);
        sub.MaxTextWidth = Math.Max(1, W - tx - 12 * k); sub.MaxLineCount = 2; sub.Trimming = TextTrimming.CharacterEllipsis;
        // 제목 줄 높이는 배지와 제목 중 큰 쪽, 묶음 전체를 카드 가운데에
        double line = Math.Max(title.Height, string.IsNullOrEmpty(BadgeKey) ? 0 : bh);
        double top = (H - (line + gap + sub.Height)) / 2, mid = top + line / 2;
        dc.DrawText(title, new Point(tx, mid - title.Height / 2));
        if (!string.IsNullOrEmpty(BadgeKey)) {
            FormattedText bt = Art.Ft(L.T(BadgeKey), Art.Pt(8), true, en ? Colors.White : Look.DisText);
            double bw = bt.Width + 16 * k, bx = tx + title.Width + 10 * k, by = mid - bh / 2 + k;
            Color bc = (Mode == "fast") ? Look.Warn : Look.Accent;
            dc.DrawRectangle(Look.Brush(en ? bc : Look.DisBg), null, new Rect(bx, by, bw, bh), bh / 2, bh / 2);
            dc.DrawText(bt, new Point(bx + (bw - bt.Width) / 2, by + (bh - bt.Height) / 2));
        }
        dc.DrawText(sub, new Point(tx, top + line + gap));
    }
}

// ---------------------------------------------------------------- 세그먼트 버튼 (선택 막대가 미끄러져 움직인다)
// Items: "값=문구키;값=문구키" (예: light=theme_light;dark=theme_dark)
public class Segmented : Drawn {
    public static readonly StyledProperty<string> ItemsProperty = AvaloniaProperty.Register<Segmented, string>(nameof(Items), "");
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<Segmented, string>(nameof(Value), "", defaultBindingMode: BindingMode.TwoWay);
    public string Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    static Segmented() { AffectsMeasure<Segmented>(ItemsProperty); AffectsRender<Segmented>(ValueProperty); }

    sealed class Item { public string Value, Key; public double W; }
    readonly List<Item> list = new();
    bool placed;
    double pillX, pillW;

    public Segmented() { Height = 32; Cursor = new Cursor(StandardCursorType.Hand); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); MovePill(false); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == ValueProperty) MovePill(true);
    }

    void Parse() {
        list.Clear();
        foreach (string part in (Items ?? "").Split(';')) {
            int i = part.IndexOf('='); if (i <= 0) continue;
            Item it = new Item { Value = part.Substring(0, i), Key = part.Substring(i + 1) };
            it.W = Art.MaxW(it.Key, 12, true) + 28;
            list.Add(it);
        }
    }
    protected override Size MeasureOverride(Size avail) {
        Parse();
        // 언어/글꼴이 바뀌면 칸 폭이 달라진다. 알약도 새 폭에 맞춰 바로 옮긴다 (측정 중에는 값을 바꾸지 않고 다음 순간에)
        if (placed) Dispatcher.UIThread.Post(() => MovePill(false), DispatcherPriority.Render);
        double w = 4; foreach (Item it in list) w += it.W;
        return new Size(w, 32);
    }
    void MovePill(bool anim) {
        if (list.Count == 0) Parse();
        double x = 2;
        foreach (Item it in list) {
            if (it.Value == Value) {
                if (!anim || !placed || Motion.Disabled || !Live) { Tween.Stop(this, "px"); Tween.Stop(this, "pw"); pillX = x; pillW = it.W; placed = true; InvalidateVisual(); }
                else { Animate("px", pillX, x, 0.22, v => pillX = v); Animate("pw", pillW, it.W, 0.22, v => pillW = v); }
                return;
            }
            x += it.W;
        }
    }
    // 셀프 테스트용: 알약이 지금 값의 칸에 정확히 있는지
    public bool PillAligned {
        get {
            double x = 2;
            foreach (Item it in list) {
                if (it.Value == Value) return Math.Abs(pillX - x) < 0.5 && Math.Abs(pillW - it.W) < 0.5;
                x += it.W;
            }
            return false;
        }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        double x = 2, mx = e.GetPosition(this).X;
        foreach (Item it in list) { if (mx >= x && mx < x + it.W) { Value = it.Value; break; } x += it.W; }
        base.OnPointerReleased(e);
    }
    public override void Render(DrawingContext dc) {
        if (!placed) MovePill(false);
        dc.DrawRectangle(Look.Brush(Look.Btn), null, new Rect(0.5, 0.5, W - 1, H - 1), 8, 8);
        if (pillW > 1) dc.DrawRectangle(Look.Brush(Look.Accent), null, new Rect(pillX, 2, pillW, H - 4), 6, 6);
        double x = 2;
        foreach (Item it in list) {
            bool on = it.Value == Value && Math.Abs(pillX - x) < it.W / 2;
            FormattedText t = Art.Ft(L.T(it.Key), 12, on, on ? Colors.White : Look.Text);
            dc.DrawText(t, new Point(x + (it.W - t.Width) / 2, (H - t.Height) / 2));
            x += it.W;
        }
    }
}

// ---------------------------------------------------------------- 숫자 상자 [ − | 20 | + ]
// 버튼(누르고 있으면 반복), 직접 입력(Enter/포커스 이동 시 적용), 마우스 휠로 바꾼다.
public class NumBox : Border {
    public static readonly StyledProperty<int> ValueProperty = AvaloniaProperty.Register<NumBox, int>(nameof(Value), 1, defaultBindingMode: BindingMode.TwoWay);
    public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get; set; } = 1;
    public int Maximum { get; set; } = 20;
    readonly TextBox tb = new TextBox();

    public NumBox() {
        Height = 30; Width = 116; CornerRadius = new CornerRadius(8); BorderThickness = new Thickness(1);
        this[!BackgroundProperty] = new DynamicResourceExtension("Input");
        this[!BorderBrushProperty] = new DynamicResourceExtension("InputBdr");
        Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,30") };
        g.Children.Add(MakeBtn("M 0,5 L 10,5", -1, 0));
        g.Children.Add(MakeBtn("M 0,5 L 10,5 M 5,0 L 5,10", 1, 2));
        if (Application.Current.TryFindResource("NumText", out object th) && th is ControlTheme ct) tb.Theme = ct;
        tb.HorizontalContentAlignment = HorizontalAlignment.Center; tb.FontWeight = FontWeight.Bold;
        Grid.SetColumn(tb, 1); g.Children.Add(tb);
        Child = g;
        tb.AddHandler(TextInputEvent, (s, e) => { if (e.Text != null && e.Text.Any(c => !char.IsDigit(c))) e.Handled = true; }, RoutingStrategies.Tunnel);
        tb.KeyDown += (s, e) => {
            if (e.Key == Key.Enter) { Commit(); tb.SelectAll(); }
            else if (e.Key == Key.Up) { Step(1); e.Handled = true; }
            else if (e.Key == Key.Down) { Step(-1); e.Handled = true; }
        };
        tb.LostFocus += (s, e) => { Commit(); this[!BorderBrushProperty] = new DynamicResourceExtension("InputBdr"); };
        tb.GotFocus += (s, e) => { this[!BorderBrushProperty] = new DynamicResourceExtension("Accent"); };
        PointerWheelChanged += (s, e) => { if (IsEffectivelyEnabled) { Step(e.Delta.Y > 0 ? 1 : -1); e.Handled = true; } };
        ShowValue();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == ValueProperty) ShowValue();
        if (e.Property == IsEffectivelyEnabledProperty) Opacity = IsEffectivelyEnabled ? 1 : 0.5;
    }

    RepeatButton MakeBtn(string path, int delta, int col) {
        Avalonia.Controls.Shapes.Path p = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(path), StrokeThickness = 1.6, Width = 10, Height = 10, StrokeLineCap = PenLineCap.Round };
        p[!Shape.StrokeProperty] = new DynamicResourceExtension("Text");
        RepeatButton b = new RepeatButton { Content = p, Focusable = false, Cursor = new Cursor(StandardCursorType.Hand), Delay = 350, Interval = 70 };
        if (Application.Current.TryFindResource("NumBtn", out object th) && th is ControlTheme ct) b.Theme = ct;
        b.Click += (s, e) => Step(delta);
        Grid.SetColumn(b, col);
        return b;
    }

    void Step(int d) { Commit(); Value = Clamp(Value + d); }
    int Clamp(int v) { return Math.Max(Minimum, Math.Min(Maximum, v)); }
    void Commit() {
        if (int.TryParse(tb.Text, out int v)) Value = Clamp(v);
        ShowValue();
    }
    void ShowValue() { string s = Value.ToString(); if (tb.Text != s) tb.Text = s; }
}

// ---------------------------------------------------------------- 레이아웃 경계 표시 (개발자 옵션)
// Target 아래의 보이는 요소마다 경계선(파랑)과 모서리 표시(분홍)를 그린다. 레이아웃이 바뀌면 다시 그린다.
public class BoundsLayer : Control {
    public Control Target;
    bool queued;
    public BoundsLayer() { IsHitTestVisible = false; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property != IsVisibleProperty || Target == null) return;
        if (IsVisible) { Target.LayoutUpdated += OnLayout; InvalidateVisual(); } else Target.LayoutUpdated -= OnLayout;
    }
    void OnLayout(object s, EventArgs e) {
        if (queued) return;
        queued = true;
        Dispatcher.UIThread.Post(() => { queued = false; InvalidateVisual(); }, DispatcherPriority.Background);
    }
    public override void Render(DrawingContext dc) {
        if (Target == null || !IsVisible) return;
        IPen line = Art.Pen(Color.FromArgb(150, 40, 120, 255), 1), mark = Art.Pen(Color.FromRgb(255, 40, 160), 1.5);
        Walk(dc, Target, line, mark, 0);
    }
    void Walk(DrawingContext dc, Visual d, IPen line, IPen mark, int depth) {
        if (depth > 60) return;
        foreach (Visual c in d.GetVisualChildren()) {
            if (!c.IsVisible) continue;
            if (c.Bounds.Width >= 2 && c.Bounds.Height >= 2) {
                Point? o = c.TranslatePoint(new Point(0, 0), this);
                if (o != null) {
                    Rect r = new Rect(Math.Round(o.Value.X) + 0.5, Math.Round(o.Value.Y) + 0.5, Math.Max(0, Math.Round(c.Bounds.Width) - 1), Math.Max(0, Math.Round(c.Bounds.Height) - 1));
                    dc.DrawRectangle(null, line, r);
                    double k = Math.Min(6, Math.Min(r.Width, r.Height) / 2);
                    dc.DrawLine(mark, r.TopLeft, new Point(r.Left + k, r.Top)); dc.DrawLine(mark, r.TopLeft, new Point(r.Left, r.Top + k));
                    dc.DrawLine(mark, r.BottomRight, new Point(r.Right - k, r.Bottom)); dc.DrawLine(mark, r.BottomRight, new Point(r.Right, r.Bottom - k));
                }
            }
            Walk(dc, c, line, mark, depth + 1);
        }
    }
}

// ---------------------------------------------------------------- 값이 매개변수와 같으면 true (마법사 단계: ConverterParameter=2 → IsVisible)
public class EqVis : IValueConverter {
    public static readonly EqVis I = new EqVis();
    public object Convert(object v, Type t, object p, CultureInfo c) { return v != null && p != null && v.ToString() == p.ToString(); }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) { throw new NotSupportedException(); }
}
// bool 뒤집기 (IsVisible="{Binding X, Converter={x:Static ui:Not.I}}")
public class Not : IValueConverter {
    public static readonly Not I = new Not();
    public object Convert(object v, Type t, object p, CultureInfo c) { return !(v is bool b && b); }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) { return !(v is bool b && b); }
}

// ---------------------------------------------------------------- 버튼 모양 (Styles.axaml) + 폴더 끌어다 놓기 (TextBox에 ui:Behave.FolderDrop="True")
public class Behave : AvaloniaObject {
    public static readonly AttachedProperty<CornerRadius> RadiusProperty = AvaloniaProperty.RegisterAttached<Behave, Control, CornerRadius>("Radius", new CornerRadius(8));
    public static CornerRadius GetRadius(Control d) => d.GetValue(RadiusProperty);
    public static void SetRadius(Control d, CornerRadius v) => d.SetValue(RadiusProperty, v);
    public static readonly AttachedProperty<IBrush> HoverProperty = AvaloniaProperty.RegisterAttached<Behave, Control, IBrush>("Hover");
    public static IBrush GetHover(Control d) => d.GetValue(HoverProperty);
    public static void SetHover(Control d, IBrush v) => d.SetValue(HoverProperty, v);

    public static readonly AttachedProperty<bool> FolderDropProperty = AvaloniaProperty.RegisterAttached<Behave, TextBox, bool>("FolderDrop");
    public static bool GetFolderDrop(TextBox d) => d.GetValue(FolderDropProperty);
    public static void SetFolderDrop(TextBox d, bool v) => d.SetValue(FolderDropProperty, v);
    static Behave() {
        ValueProperty.Changed.AddClassHandler<ComboBox>((cb, e) => { HookValue(cb); Reselect(cb); });
        FolderDropProperty.Changed.AddClassHandler<TextBox>((t, e) => {
            if (e.NewValue is not true) return;
            DragDrop.SetAllowDrop(t, true);
            t.AddHandler(DragDrop.DragOverEvent, (s, a) => { a.DragEffects = a.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None; a.Handled = true; }, RoutingStrategies.Tunnel);
            t.AddHandler(DragDrop.DropEvent, (s, a) => {
                string f = DropPath(a);
                if (f != null) t.Text = Directory.Exists(f) ? f : System.IO.Path.GetDirectoryName(f);
                a.Handled = true;
            }, RoutingStrategies.Tunnel);
        });
    }
    // 드롭다운의 고른 값 (항목의 Code와 같은 것을 고른다). 목록을 다시 채워도 같은 값이면 다시 고른다 (WPF SelectedValue처럼)
    public static readonly AttachedProperty<string> ValueProperty = AvaloniaProperty.RegisterAttached<Behave, ComboBox, string>("Value", defaultBindingMode: BindingMode.TwoWay);
    public static string GetValue(ComboBox d) => d.GetValue(ValueProperty);
    public static void SetValue(ComboBox d, string v) => d.SetValue(ValueProperty, v);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, object> hooked = new();
    static string CodeOf(object item) => item?.GetType().GetProperty("Code")?.GetValue(item) as string;
    static void Reselect(ComboBox cb) {
        string v = cb.GetValue(ValueProperty) ?? "";
        object pick = null;
        foreach (object it in cb.Items) if ((CodeOf(it) ?? "") == v) { pick = it; break; }
        if (!ReferenceEquals(cb.SelectedItem, pick)) { hooked.AddOrUpdate(cb, "busy"); cb.SelectedItem = pick; hooked.AddOrUpdate(cb, "idle"); }
    }
    static void HookValue(ComboBox cb) {
        if (hooked.TryGetValue(cb, out _)) return;
        hooked.Add(cb, "idle");
        cb.Items.CollectionChanged += (s, e) => Dispatcher.UIThread.Post(() => Reselect(cb), DispatcherPriority.Background);
        cb.PropertyChanged += (s, e) => { if (e.Property == ItemsControl.ItemsSourceProperty) Dispatcher.UIThread.Post(() => Reselect(cb), DispatcherPriority.Background); };
        cb.SelectionChanged += (s, e) => {
            if (hooked.TryGetValue(cb, out object st) && (string)st == "busy") return;
            if (cb.SelectedItem == null) return;
            string c = CodeOf(cb.SelectedItem);
            if (c != null && c != cb.GetValue(ValueProperty)) cb.SetValue(ValueProperty, c);
        };
    }

    public static string DropPath(DragEventArgs a) {
        IStorageItem it = a.Data.GetFiles()?.FirstOrDefault();
        return it?.TryGetLocalPath();
    }
}

// ---------------------------------------------------------------- 스플라인 미리보기: 위에서 본 도로(차선, 방향 화살표, 인도) + 폭 + 단면(연석 높이는 과장)
public class SplinePreview : Drawn {
    public static readonly StyledProperty<SplineSpec> SpecProperty = AvaloniaProperty.Register<SplinePreview, SplineSpec>(nameof(Spec));
    public static readonly StyledProperty<int> VersionProperty = AvaloniaProperty.Register<SplinePreview, int>(nameof(Version));
    public SplineSpec Spec { get => GetValue(SpecProperty); set => SetValue(SpecProperty, value); }
    public int Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }
    static SplinePreview() { AffectsRender<SplinePreview>(SpecProperty, VersionProperty); }
    public SplinePreview() { Height = 236; }

    public override void Render(DrawingContext dc) {
        SplineSpec sp = Spec;
        if (sp == null || W < 50) return;
        bool ok = sp.Valid(out string err);
        double total = Math.Max(0.1, sp.TotalW), pad = 24;
        double k = (W - pad * 2) / total, x0 = pad, top = 14, bot = 146;
        bool dark = Look.Dark;
        Color asph = dark ? Color.FromRgb(62, 64, 70) : Color.FromRgb(78, 80, 86);
        Color walk = dark ? Color.FromRgb(104, 100, 94) : Color.FromRgb(214, 208, 198);
        Color joint = dark ? Color.FromRgb(88, 85, 80) : Color.FromRgb(190, 184, 174);
        Color white = Color.FromRgb(240, 240, 240), yellow = Color.FromRgb(250, 200, 40);
        double xl = x0, xr0 = x0 + sp.WalkL * k, xr1 = xr0 + sp.RoadW * k;
        using (dc.PushOpacity(ok ? 1 : 0.35))
        using (dc.PushGeometryClip(Art.RoundRect(new Rect(x0, top, total * k, bot - top), 12))) {
            // 인도 (보도블록 줄눈)
            DrawWalk(dc, new Rect(xl, top, sp.WalkL * k, bot - top), walk, joint, sp.WalkRepeat * k);
            DrawWalk(dc, new Rect(xr1, top, sp.WalkR * k, bot - top), walk, joint, sp.WalkRepeat * k);
            // 도로
            dc.DrawRectangle(Look.Brush(asph), null, new Rect(xr0, top, sp.RoadW * k, bot - top));
            IPen edge = Art.Pen(Look.A(white, 0.9), 2);
            dc.DrawLine(edge, new Point(xr0 + 3, top), new Point(xr0 + 3, bot));
            dc.DrawLine(edge, new Point(xr1 - 3, top), new Point(xr1 - 3, bot));
            List<SplineSpec.PathDef> paths = sp.Paths();
            int back = sp.BackLanes;
            for (int i = 1; i < sp.Lanes; i++) {
                double lx = xr0 + i * sp.LaneW * k;
                if (i == back) {   // 마주 오는 차선 사이: 노란 겹선
                    IPen y = Art.Pen(yellow, 2);
                    dc.DrawLine(y, new Point(lx - 2.5, top), new Point(lx - 2.5, bot));
                    dc.DrawLine(y, new Point(lx + 2.5, top), new Point(lx + 2.5, bot));
                } else {
                    IPen d = Art.Pen(Look.A(white, 0.85), 2);
                    for (double yy = top + 6; yy < bot; yy += 26) dc.DrawLine(d, new Point(lx, yy), new Point(lx, Math.Min(bot, yy + 13)));
                }
            }
            // 차/사람 길: 차선마다 방향 화살표, 인도는 점선
            foreach (SplineSpec.PathDef pd in paths) {
                double px = x0 + (pd.X + sp.RoadW / 2 + sp.WalkL) * k;
                if (pd.Kind == 0) Arrow(dc, px, (top + bot) / 2, pd.Dir == 0, Look.A(white, 0.95));
                else {
                    IPen d = Art.RoundPen(Look.A(Look.Accent, 0.9), 2.2);
                    for (double yy = top + 8; yy < bot - 4; yy += 12) dc.DrawLine(d, new Point(px, yy), new Point(px, yy + 3));
                }
            }
        }
        // 폭 표시
        Color mut = Look.Muted;
        double ly = bot + 8;
        IPen tick = Art.Pen(Look.A(mut, 0.6), 1);
        List<double[]> parts = new List<double[]>();
        if (sp.WalkL > 0) parts.Add(new[] { xl, xr0, sp.WalkL });
        if (sp.PerLane || sp.Lanes <= 4) for (int i = 0; i < sp.Lanes; i++) parts.Add(new[] { xr0 + i * sp.LaneW * k, xr0 + (i + 1) * sp.LaneW * k, sp.LaneW });
        else parts.Add(new[] { xr0, xr1, sp.RoadW });
        if (sp.WalkR > 0) parts.Add(new[] { xr1, xr1 + sp.WalkR * k, sp.WalkR });
        foreach (double[] pt in parts) {
            dc.DrawLine(tick, new Point(pt[0] + 1, ly + 4), new Point(pt[1] - 1, ly + 4));
            dc.DrawLine(tick, new Point(pt[0] + 1, ly), new Point(pt[0] + 1, ly + 8));
            dc.DrawLine(tick, new Point(pt[1] - 1, ly), new Point(pt[1] - 1, ly + 8));
            FormattedText ft = Art.Ft(pt[2].ToString("0.##", CultureInfo.InvariantCulture) + " m", 11, false, mut);
            if (ft.Width < pt[1] - pt[0] - 4) dc.DrawText(ft, new Point((pt[0] + pt[1] - ft.Width) / 2, ly + 10));
        }
        // 단면: 연석 높이를 보이게 과장 (최대 14px)
        double py = 222, ch = sp.Curb > 0 ? Math.Min(14, 4 + sp.Curb * 40) : 0;
        IPen prof = Art.RoundPen(Look.Text, 2);
        List<Point> pts = new List<Point> { new Point(xl, py - (sp.WalkL > 0 ? ch : 0)) };
        if (sp.WalkL > 0) { pts.Add(new Point(xr0, py - ch)); pts.Add(new Point(xr0, py)); }
        pts.Add(new Point(xr1, py));
        if (sp.WalkR > 0) { pts.Add(new Point(xr1, py - ch)); pts.Add(new Point(xr1 + sp.WalkR * k, py - ch)); }
        dc.DrawGeometry(null, prof, Art.Poly(false, pts.ToArray()));
    }

    static void DrawWalk(DrawingContext dc, Rect r, Color fill, Color joint, double tile) {
        if (r.Width <= 0.5) return;
        dc.DrawRectangle(Look.Brush(fill), null, r);
        IPen j = Art.Pen(joint, 1);
        tile = Math.Max(6, tile / 2);
        using (dc.PushClip(r)) {
            for (double x = r.X + tile; x < r.Right; x += tile) dc.DrawLine(j, new Point(x, r.Top), new Point(x, r.Bottom));
            for (double y = r.Y + tile; y < r.Bottom; y += tile) dc.DrawLine(j, new Point(r.X, y), new Point(r.Right, y));
        }
    }

    static void Arrow(DrawingContext dc, double x, double y, bool up, Color c) {
        double s = up ? -1 : 1;
        IPen p = Art.RoundPen(c, 2.6);
        dc.DrawLine(p, new Point(x, y - 16 * s), new Point(x, y + 16 * s));
        dc.DrawGeometry(null, p, Art.Poly(false, new Point(x - 7, y + 9 * s), new Point(x, y + 16 * s), new Point(x + 7, y + 9 * s)));
    }
}

// ---------------------------------------------------------------- 상태 점 (State: ok / warn / busy)
public class StatusDot : Drawn {
    public static readonly StyledProperty<string> StateProperty = AvaloniaProperty.Register<StatusDot, string>(nameof(State), "warn");
    public string State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    static StatusDot() { AffectsRender<StatusDot>(StateProperty); }
    public StatusDot() { Width = 10; Height = 10; }
    public override void Render(DrawingContext dc) {
        Color c = State == "ok" ? Look.Ok : State == "busy" ? Color.FromRgb(255, 152, 0) : Look.Warn;
        dc.DrawEllipse(Look.Brush(c), null, new Point(W / 2, H / 2), W / 2, H / 2);
    }
}
