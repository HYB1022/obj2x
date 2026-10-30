using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Obj2x.UI {

// 직접 그리는 컨트롤의 공통 부분: 테마/언어가 바뀌면 다시 그리고, 값을 부드럽게 움직인다
public class Drawn : FrameworkElement {
    public Drawn() {
        Loaded += delegate { if (hooked) return; hooked = true; Theme.Changed += OnLook; Loc.Changed += OnLook; };
        Unloaded += delegate { if (!hooked) return; hooked = false; Theme.Changed -= OnLook; Loc.Changed -= OnLook; };
        IsEnabledChanged += delegate { InvalidateVisual(); };
    }
    bool hooked;
    void OnLook() { InvalidateMeasure(); InvalidateVisual(); }
    protected static DependencyProperty Dp(string name, Type t, Type owner, object def) {
        return DependencyProperty.Register(name, t, owner, new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }
    protected void Animate(DependencyProperty p, double to, double sec) {
        DoubleAnimation a = new DoubleAnimation(to, Motion.D(sec));
        a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(p, a);
    }
    // 빈 곳도 마우스가 닿게
    protected void HitArea(DrawingContext dc) { dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight)); }
}

// ---------------------------------------------------------------- 왼쪽 메뉴 줄 (샤오미 태블릿 설정 앱처럼 카드 안의 한 줄)
// 아이콘 타일 + 이름 + 오른쪽 [업데이트] 알약. 선택되면 줄이 강조색으로 옅게 칠해지며 나타난다.
public class NavItem : Drawn {
    public static readonly DependencyProperty PageProperty = Dp("Page", typeof(string), typeof(NavItem), "");
    public static readonly DependencyProperty KeyProperty = Dp("Key", typeof(string), typeof(NavItem), "");
    public static readonly DependencyProperty CurrentProperty = Dp("Current", typeof(string), typeof(NavItem), "");
    // 알약이 새로 생기면 글자가 아래에서 올라오며 나타난다 (BadgeIn 0 → 1)
    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register("Badge", typeof(string), typeof(NavItem),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            NavItem n = (NavItem)d;
            if (string.IsNullOrEmpty(e.OldValue as string) && !string.IsNullOrEmpty(e.NewValue as string)) { n.BeginAnimation(BadgeInProperty, null); n.SetValue(BadgeInProperty, 0.0); n.Animate(BadgeInProperty, 1, 0.45); }
        }));
    static readonly DependencyProperty BadgeInProperty = Dp("BadgeIn", typeof(double), typeof(NavItem), 1.0);
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(NavItem));
    static readonly DependencyProperty HoverProperty = Dp("Hover", typeof(double), typeof(NavItem), 0.0);
    static readonly DependencyProperty SelProperty = Dp("Sel", typeof(double), typeof(NavItem), 0.0);
    public string Page { get { return (string)GetValue(PageProperty); } set { SetValue(PageProperty, value); } }
    public string Key { get { return (string)GetValue(KeyProperty); } set { SetValue(KeyProperty, value); } }
    public string Current { get { return (string)GetValue(CurrentProperty); } set { SetValue(CurrentProperty, value); } }
    public string Badge { get { return (string)GetValue(BadgeProperty); } set { SetValue(BadgeProperty, value); } }
    public ICommand Command { get { return (ICommand)GetValue(CommandProperty); } set { SetValue(CommandProperty, value); } }
    public bool Active { get { return Current == Page; } }

    readonly RippleFx fx;
    public NavItem() { Height = 50; Cursor = Cursors.Hand; fx = new RippleFx(this); }
    Rect Box { get { return new Rect(0, 0, ActualWidth, ActualHeight); } }
    const double Rad = 14;
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property != CurrentProperty && e.Property != PageProperty) return;
        if (IsLoaded) Animate(SelProperty, Active ? 1 : 0, 0.2);
        else { BeginAnimation(SelProperty, null); SetValue(SelProperty, Active ? 1.0 : 0.0); }
    }
    protected override void OnMouseEnter(MouseEventArgs e) { Animate(HoverProperty, 1, 0.18); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(MouseEventArgs e) { Animate(HoverProperty, 0, 0.18); fx.Release(); base.OnMouseLeave(e); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { fx.Press(e.GetPosition(this), Box, 0); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { fx.Release(); if (Command != null) Command.Execute(Page); base.OnMouseLeftButtonUp(e); }

    protected override void OnRender(DrawingContext dc) {
        HitArea(dc);
        Rect r = Box;
        double hv = (double)GetValue(HoverProperty), sel = Math.Max(0, Math.Min(1, (double)GetValue(SelProperty)));
        if (hv * (1 - sel) > 0.01) dc.DrawRoundedRectangle(Theme.Brush(Theme.A(Theme.Btn, hv * (1 - sel))), null, r, Rad, Rad);
        if (sel > 0.01) dc.DrawRoundedRectangle(Theme.Brush(Theme.A(Theme.NavPill, sel)), null, r, Rad, Rad);
        if (fx.Active) { dc.PushClip(new RectangleGeometry(r, Rad, Rad)); fx.Draw(dc, RippleFx.Tint(Theme.Text), 0); dc.Pop(); }
        double isz = 30, ix = r.X + 12;
        Art.Icon(dc, Page, ix, r.Y + (r.Height - isz) / 2, isz, 1);
        double tx = ix + isz + 14, right = r.Right - 12;
        if (!string.IsNullOrEmpty(Badge)) {
            double e = Motion.EaseOut((double)GetValue(BadgeInProperty));
            Color fg = Theme.Dark ? Theme.Mix(Theme.Accent, Colors.White, 0.45) : Theme.Accent;
            FormattedText bf = Art.Ft(L.T(Badge), Art.Pt(8.5), false, Theme.A(fg, e));
            double bw = bf.Width + 20, bh = 24;
            Rect br = new Rect(right - bw, r.Y + (r.Height - bh) / 2, bw, bh);
            dc.DrawRoundedRectangle(Theme.Brush(Theme.A(Theme.Accent, (Theme.Dark ? 70 / 255.0 : 34 / 255.0) * e)), null, br, bh / 2, bh / 2);
            dc.PushClip(new RectangleGeometry(br, bh / 2, bh / 2));
            dc.DrawText(bf, new Point(br.X + (bw - bf.Width) / 2, br.Y + (bh - bf.Height) / 2 + (1 - e) * 14));
            dc.Pop();
            right = br.X - 6;
        }
        FormattedText t = Art.Line(L.T(Key), 14, false, Theme.Mix(Theme.Text, Theme.NavPillText, sel), right - tx);
        dc.DrawText(t, new Point(tx, r.Y + (r.Height - t.Height) / 2));
    }
}

// ---------------------------------------------------------------- 마법사 단계 표시줄
public class Stepper : Drawn {
    public static readonly DependencyProperty StepProperty = Dp("Step", typeof(int), typeof(Stepper), 1);
    public int Step { get { return (int)GetValue(StepProperty); } set { SetValue(StepProperty, value); } }
    static readonly string[] keys = new string[] { "step_1", "step_2", "step_3", "step_4" };
    public Stepper() { Height = 58; }
    protected override void OnRender(DrawingContext dc) {
        double W = ActualWidth, cy = 18, r = 12;
        double usable = Math.Max(240, W - 96), span = usable / 3;
        dc.DrawLine(Art.Pen(Theme.Divider, 2), new Point(48, cy), new Point(48 + 3 * span, cy));
        if (Step > 1) dc.DrawLine(Art.Pen(Theme.Accent, 2), new Point(48, cy), new Point(48 + Math.Min(3, Step - 1) * span, cy));   // 지나온 구간은 강조색
        for (int i = 0; i < 4; i++) {
            double cx = 48 + i * span;
            bool done = (i + 1 < Step), active = (i + 1 == Step);
            Color fill = (active || done) ? Theme.Accent : Theme.Card;
            dc.DrawEllipse(Theme.Brush(fill), Art.Pen((active || done) ? Theme.Accent : Theme.Divider, 2), new Point(cx, cy), r, r);
            if (done) {                            // 글꼴의 ✓ 대신 둥근 선으로 그린 체크
                dc.DrawGeometry(null, Art.RoundPen(Colors.White, 2.2), Art.Poly(false,
                    new Point(cx - 4.6, cy + 0.3), new Point(cx - 1.4, cy + 3.5), new Point(cx + 4.8, cy - 3.4)));
            } else {
                FormattedText num = Art.Ft((i + 1).ToString(), 12, true, active ? Colors.White : Theme.Muted);
                dc.DrawText(num, new Point(cx - num.Width / 2, cy - num.Height / 2));
            }
            FormattedText t = Art.Ft(L.T(keys[i]), Art.Pt(8.5), false, active ? Theme.Text : Theme.Muted);
            double tw = t.Width + 12;
            double lx = Math.Max(0, Math.Min(W - tw, cx - tw / 2));
            dc.DrawText(t, new Point(lx + 6, cy + 13 + (22 - t.Height) / 2));
        }
    }
}

// ---------------------------------------------------------------- 마법사 변환 방식 카드
public class RadioCard : Drawn {
    public static readonly DependencyProperty ModeProperty = Dp("Mode", typeof(string), typeof(RadioCard), "");
    public static readonly DependencyProperty TitleKeyProperty = Dp("TitleKey", typeof(string), typeof(RadioCard), "");
    public static readonly DependencyProperty SubKeyProperty = Dp("SubKey", typeof(string), typeof(RadioCard), "");
    public static readonly DependencyProperty BadgeKeyProperty = Dp("BadgeKey", typeof(string), typeof(RadioCard), null);
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register("Selected", typeof(bool), typeof(RadioCard),
        new FrameworkPropertyMetadata(false, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { RadioCard c = (RadioCard)d; c.Animate(SelProperty, (bool)e.NewValue ? 1 : 0, 0.2); }));
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(RadioCard));
    static readonly DependencyProperty SelProperty = Dp("Sel", typeof(double), typeof(RadioCard), 0.0);
    static readonly DependencyProperty HoverProperty = Dp("Hover", typeof(double), typeof(RadioCard), 0.0);
    public string Mode { get { return (string)GetValue(ModeProperty); } set { SetValue(ModeProperty, value); } }
    public string TitleKey { get { return (string)GetValue(TitleKeyProperty); } set { SetValue(TitleKeyProperty, value); } }
    public string SubKey { get { return (string)GetValue(SubKeyProperty); } set { SetValue(SubKeyProperty, value); } }
    public string BadgeKey { get { return (string)GetValue(BadgeKeyProperty); } set { SetValue(BadgeKeyProperty, value); } }
    public bool Selected { get { return (bool)GetValue(SelectedProperty); } set { SetValue(SelectedProperty, value); } }
    public ICommand Command { get { return (ICommand)GetValue(CommandProperty); } set { SetValue(CommandProperty, value); } }

    readonly RippleFx fx;
    public RadioCard() { Height = 72; IsEnabledChanged += delegate { Cursor = IsEnabled ? Cursors.Hand : Cursors.Arrow; }; Cursor = Cursors.Hand; fx = new RippleFx(this); }
    protected override void OnMouseEnter(MouseEventArgs e) { Animate(HoverProperty, 1, 0.18); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(MouseEventArgs e) { Animate(HoverProperty, 0, 0.18); fx.Release(); base.OnMouseLeave(e); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { if (IsEnabled) fx.Press(e.GetPosition(this), new Rect(0, 0, ActualWidth, ActualHeight), 0); base.OnMouseLeftButtonDown(e); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { fx.Release(); if (IsEnabled && Command != null) Command.Execute(Mode); base.OnMouseLeftButtonUp(e); }

    protected override void OnRender(DrawingContext dc) {
        double W = ActualWidth, H = ActualHeight, k = H / 72.0;
        double s = Math.Max(0, Math.Min(1, (double)GetValue(SelProperty)));
        double h = IsEnabled ? Math.Max(0, Math.Min(1, (double)GetValue(HoverProperty))) : 0;
        Color bdr = Theme.Mix(Theme.Mix(Theme.CardBdr, Theme.Muted, h), Theme.Accent, s);
        dc.DrawRoundedRectangle(Theme.Brush(Theme.Mix(Theme.Card, Theme.NavPill, s)), Art.Pen(bdr, 1 + s), new Rect(1, 1, W - 2, H - 2), 10 * k, 10 * k);
        if (fx.Active) { dc.PushClip(new RectangleGeometry(new Rect(1, 1, W - 2, H - 2), 10 * k, 10 * k)); fx.Draw(dc, RippleFx.Tint(Theme.Text), 0); dc.Pop(); }
        Color tc = IsEnabled ? Theme.Text : Theme.DisText, sc = IsEnabled ? Theme.Muted : Theme.DisText;
        Color rc = Selected ? Theme.Accent : (IsEnabled ? Theme.Muted : Theme.DisText);
        dc.DrawEllipse(null, Art.Pen(rc, 2), new Point(27 * k, H / 2), 9 * k, 9 * k);
        if (s > 0.02) dc.DrawEllipse(Theme.Brush(Theme.Accent), null, new Point(27 * k, H / 2), 4 * k * s, 4 * k * s);
        double tx = 54 * k, gap = 8 * k, bh = 20 * k;
        FormattedText title = Art.Ft(L.T(TitleKey), 12, true, tc);
        FormattedText sub = Art.Ft(L.T(SubKey), Art.Pt(8.5), false, sc);
        sub.MaxTextWidth = Math.Max(1, W - tx - 12 * k); sub.MaxLineCount = 2; sub.Trimming = TextTrimming.CharacterEllipsis;
        // 제목 줄 높이는 배지와 제목 중 큰 쪽, 묶음 전체를 카드 가운데에
        double line = Math.Max(title.Height, string.IsNullOrEmpty(BadgeKey) ? 0 : bh);
        double top = (H - (line + gap + sub.Height)) / 2, mid = top + line / 2;
        dc.DrawText(title, new Point(tx, mid - title.Height / 2));
        if (!string.IsNullOrEmpty(BadgeKey)) {
            FormattedText bt = Art.Ft(L.T(BadgeKey), Art.Pt(8), true, IsEnabled ? Colors.White : Theme.DisText);
            double bw = bt.Width + 16 * k, bx = tx + title.Width + 10 * k, by = mid - bh / 2 + k;   // 글자는 줄 상자 가운데보다 조금 아래에 찍혀서 1px 내린다
            Color bc = (Mode == "fast") ? Theme.Warn : Theme.Accent;
            dc.DrawRoundedRectangle(Theme.Brush(IsEnabled ? bc : Theme.DisBg), null, new Rect(bx, by, bw, bh), bh / 2, bh / 2);
            dc.DrawText(bt, new Point(bx + (bw - bt.Width) / 2, by + (bh - bt.Height) / 2));
        }
        dc.DrawText(sub, new Point(tx, top + line + gap));
    }
}

// ---------------------------------------------------------------- 세그먼트 버튼 (선택 막대가 미끄러져 움직인다)
// Items: "값=문구키;값=문구키" (예: light=theme_light;dark=theme_dark)
public class Segmented : Drawn {
    public static readonly DependencyProperty ItemsProperty = Dp("Items", typeof(string), typeof(Segmented), "");
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(Segmented),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((Segmented)d).MovePill(true); }));
    static readonly DependencyProperty PillXProperty = Dp("PillX", typeof(double), typeof(Segmented), 0.0);
    static readonly DependencyProperty PillWProperty = Dp("PillW", typeof(double), typeof(Segmented), 0.0);
    public string Items { get { return (string)GetValue(ItemsProperty); } set { SetValue(ItemsProperty, value); } }
    public string Value { get { return (string)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }

    class Item { public string Value, Key; public double W; }
    List<Item> list = new List<Item>();
    bool placed;

    public Segmented() { Height = 32; Cursor = Cursors.Hand; Loaded += delegate { MovePill(false); }; }

    void Parse() {
        list.Clear();
        foreach (string part in (Items ?? "").Split(';')) {
            int i = part.IndexOf('='); if (i <= 0) continue;
            Item it = new Item(); it.Value = part.Substring(0, i); it.Key = part.Substring(i + 1);
            it.W = Art.MaxW(it.Key, 12, true) + 28;
            list.Add(it);
        }
    }
    protected override Size MeasureOverride(Size avail) {
        Parse();
        // 언어/글꼴이 바뀌면 칸 폭이 달라진다. 알약도 새 폭에 맞춰 바로 옮긴다 (측정 중에는 값을 바꾸지 않고 다음 순간에)
        if (placed) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(delegate { MovePill(false); }));
        double w = 4; foreach (Item it in list) w += it.W;
        return new Size(w, 32);
    }
    void MovePill(bool anim) {
        if (list.Count == 0) Parse();
        double x = 2;
        foreach (Item it in list) {
            if (it.Value == Value) {
                if (!anim || !placed || Motion.Disabled) { BeginAnimation(PillXProperty, null); BeginAnimation(PillWProperty, null); SetValue(PillXProperty, x); SetValue(PillWProperty, it.W); placed = true; }
                else { Animate(PillXProperty, x, 0.22); Animate(PillWProperty, it.W, 0.22); }
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
                if (it.Value == Value) return Math.Abs((double)GetValue(PillXProperty) - x) < 0.5 && Math.Abs((double)GetValue(PillWProperty) - it.W) < 0.5;
                x += it.W;
            }
            return false;
        }
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        double x = 2, mx = e.GetPosition(this).X;
        foreach (Item it in list) { if (mx >= x && mx < x + it.W) { Value = it.Value; break; } x += it.W; }
        base.OnMouseLeftButtonUp(e);
    }
    protected override void OnRender(DrawingContext dc) {
        if (!placed) MovePill(false);
        double W = ActualWidth, H = ActualHeight;
        dc.DrawRoundedRectangle(Theme.Brush(Theme.Btn), null, new Rect(0.5, 0.5, W - 1, H - 1), 8, 8);
        double px = (double)GetValue(PillXProperty), pw = (double)GetValue(PillWProperty);
        if (pw > 1) dc.DrawRoundedRectangle(Theme.Brush(Theme.Accent), null, new Rect(px, 2, pw, H - 4), 6, 6);
        double x = 2;
        foreach (Item it in list) {
            bool on = it.Value == Value && Math.Abs(px - x) < it.W / 2;
            FormattedText t = Art.Ft(L.T(it.Key), 12, on, on ? Colors.White : Theme.Text);
            dc.DrawText(t, new Point(x + (it.W - t.Width) / 2, (H - t.Height) / 2));
            x += it.W;
        }
    }
}

// ---------------------------------------------------------------- 숫자 상자 [ − | 20 | + ]
// 버튼(누르고 있으면 반복), 직접 입력(Enter/포커스 이동 시 적용), 마우스 휠로 바꾼다.
public class NumBox : Border {
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(int), typeof(NumBox),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((NumBox)d).ShowValue(); }));
    public int Value { get { return (int)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
    public int Minimum { get; set; }
    public int Maximum { get; set; }
    readonly TextBox tb = new TextBox();

    // 버튼 모양: 투명 바탕 + 호버 시 연한 회색
    const string BtnXaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='RepeatButton'>" +
        "<Border x:Name='b' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='Transparent' CornerRadius='6' Margin='2'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource Btn}'/></Trigger>" +
        "<Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource BtnHov}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>";
    // 가운데 입력 칸: 테두리/비활성 회색 없이 글자만 (꺼질 때는 상자 전체가 흐려진다)
    const string TbXaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'>" +
        "<ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center' Focusable='False'/></ControlTemplate>";
    static ControlTemplate btnTpl, tbTpl;

    public NumBox() {
        Minimum = 1; Maximum = 20;
        Height = 30; Width = 116; CornerRadius = new CornerRadius(8); BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, "Input"); SetResourceReference(BorderBrushProperty, "InputBdr");
        if (btnTpl == null) btnTpl = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(BtnXaml);
        if (tbTpl == null) tbTpl = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(TbXaml);
        Grid g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.Children.Add(MakeBtn("M 0,5 L 10,5", -1, 0));
        g.Children.Add(MakeBtn("M 0,5 L 10,5 M 5,0 L 5,10", 1, 2));
        tb.Template = tbTpl; tb.Background = Brushes.Transparent; tb.Height = double.NaN; tb.Margin = new Thickness(0);
        tb.HorizontalContentAlignment = HorizontalAlignment.Center; tb.FontWeight = FontWeights.Bold; tb.Padding = new Thickness(0);
        Grid.SetColumn(tb, 1); g.Children.Add(tb);
        Child = g;
        tb.PreviewTextInput += delegate(object s, TextCompositionEventArgs e) { foreach (char c in e.Text) if (!char.IsDigit(c)) e.Handled = true; };
        DataObject.AddPastingHandler(tb, delegate(object s, DataObjectPastingEventArgs e) { e.CancelCommand(); });
        tb.KeyDown += delegate(object s, KeyEventArgs e) {
            if (e.Key == Key.Enter) { Commit(); tb.SelectAll(); }
            else if (e.Key == Key.Up) { Step(1); e.Handled = true; }
            else if (e.Key == Key.Down) { Step(-1); e.Handled = true; }
        };
        tb.LostKeyboardFocus += delegate { Commit(); SetResourceReference(BorderBrushProperty, "InputBdr"); };
        tb.GotKeyboardFocus += delegate { SetResourceReference(BorderBrushProperty, "Accent"); };
        MouseWheel += delegate(object s, MouseWheelEventArgs e) { if (IsEnabled) { Step(e.Delta > 0 ? 1 : -1); e.Handled = true; } };
        IsEnabledChanged += delegate { Opacity = IsEnabled ? 1 : 0.5; };
        ShowValue();
    }

    RepeatButton MakeBtn(string path, int delta, int col) {
        System.Windows.Shapes.Path p = new System.Windows.Shapes.Path();
        p.Data = Geometry.Parse(path); p.StrokeThickness = 1.6; p.Width = 10; p.Height = 10;
        p.StrokeStartLineCap = PenLineCap.Round; p.StrokeEndLineCap = PenLineCap.Round;
        p.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Text");
        RepeatButton b = new RepeatButton(); b.Template = btnTpl; b.Content = p; b.Focusable = false; b.Cursor = Cursors.Hand;
        b.Delay = 350; b.Interval = 70;
        b.Click += delegate { Step(delta); };
        Grid.SetColumn(b, col);
        return b;
    }

    void Step(int d) { Commit(); Value = Clamp(Value + d); }
    int Clamp(int v) { return Math.Max(Minimum, Math.Min(Maximum, v)); }
    void Commit() {
        int v;
        if (int.TryParse(tb.Text, out v)) Value = Clamp(v);
        ShowValue();
    }
    void ShowValue() { string s = Value.ToString(); if (tb.Text != s) tb.Text = s; }
}

// ---------------------------------------------------------------- 레이아웃 경계 표시 (개발자 옵션)
// Target 아래의 보이는 요소마다 경계선(파랑)과 모서리 표시(분홍)를 그린다. 레이아웃이 바뀌면 다시 그린다.
public class BoundsLayer : FrameworkElement {
    public FrameworkElement Target;
    bool queued;
    public BoundsLayer() {
        IsHitTestVisible = false;
        IsVisibleChanged += delegate {
            if (Target == null) return;
            if (IsVisible) { Target.LayoutUpdated += OnLayout; InvalidateVisual(); } else Target.LayoutUpdated -= OnLayout;
        };
    }
    void OnLayout(object s, EventArgs e) {
        if (queued) return;
        queued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { queued = false; InvalidateVisual(); }));
    }
    protected override void OnRender(DrawingContext dc) {
        if (Target == null || !IsVisible) return;
        Pen line = Art.Pen(Color.FromArgb(150, 40, 120, 255), 1), mark = Art.Pen(Color.FromRgb(255, 40, 160), 1.5);
        Walk(dc, Target, line, mark, 0);
    }
    void Walk(DrawingContext dc, DependencyObject d, Pen line, Pen mark, int depth) {
        if (depth > 60) return;
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++) {
            DependencyObject c = VisualTreeHelper.GetChild(d, i);
            FrameworkElement fe = c as FrameworkElement;
            if (fe != null) {
                if (!fe.IsVisible) continue;
                if (fe.ActualWidth >= 2 && fe.ActualHeight >= 2) {
                    try {
                        Rect r = fe.TransformToAncestor(this.Parent as Visual ?? Target).TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
                        r = new Rect(Math.Round(r.X) + 0.5, Math.Round(r.Y) + 0.5, Math.Max(0, Math.Round(r.Width) - 1), Math.Max(0, Math.Round(r.Height) - 1));
                        dc.DrawRectangle(null, line, r);
                        double k = Math.Min(6, Math.Min(r.Width, r.Height) / 2);
                        dc.DrawLine(mark, r.TopLeft, new Point(r.Left + k, r.Top)); dc.DrawLine(mark, r.TopLeft, new Point(r.Left, r.Top + k));
                        dc.DrawLine(mark, r.BottomRight, new Point(r.Right - k, r.Bottom)); dc.DrawLine(mark, r.BottomRight, new Point(r.Right, r.Bottom - k));
                    } catch (InvalidOperationException) { }
                }
            }
            Walk(dc, c, line, mark, depth + 1);
        }
    }
}

// ---------------------------------------------------------------- 값이 매개변수와 같으면 보이기 (마법사 단계: ConverterParameter=2)
public class EqVis : System.Windows.Data.IValueConverter {
    public static readonly EqVis I = new EqVis();
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) {
        return (v != null && p != null && v.ToString() == p.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) { throw new NotSupportedException(); }
}

// ---------------------------------------------------------------- 버튼 모양 (Styles.xaml) + 폴더 끌어다 놓기 (TextBox에 ui:Behave.FolderDrop="True")
public static class Behave {
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached("Radius", typeof(CornerRadius), typeof(Behave), new PropertyMetadata(new CornerRadius(8)));
    public static CornerRadius GetRadius(DependencyObject d) { return (CornerRadius)d.GetValue(RadiusProperty); }
    public static void SetRadius(DependencyObject d, CornerRadius v) { d.SetValue(RadiusProperty, v); }
    public static readonly DependencyProperty HoverProperty = DependencyProperty.RegisterAttached("Hover", typeof(Brush), typeof(Behave), new PropertyMetadata(null));
    public static Brush GetHover(DependencyObject d) { return (Brush)d.GetValue(HoverProperty); }
    public static void SetHover(DependencyObject d, Brush v) { d.SetValue(HoverProperty, v); }

    public static readonly DependencyProperty FolderDropProperty = DependencyProperty.RegisterAttached("FolderDrop", typeof(bool), typeof(Behave),
        new PropertyMetadata(false, OnFolderDrop));
    public static bool GetFolderDrop(DependencyObject d) { return (bool)d.GetValue(FolderDropProperty); }
    public static void SetFolderDrop(DependencyObject d, bool v) { d.SetValue(FolderDropProperty, v); }
    static void OnFolderDrop(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        TextBox t = d as TextBox;
        if (t == null || !(bool)e.NewValue) return;
        t.AllowDrop = true;
        t.PreviewDragOver += delegate(object s, DragEventArgs a) {
            a.Effects = a.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            a.Handled = true;
        };
        t.PreviewDrop += delegate(object s, DragEventArgs a) {
            string[] f = a.Data.GetData(DataFormats.FileDrop) as string[];
            if (f != null && f.Length > 0) t.Text = Directory.Exists(f[0]) ? f[0] : Path.GetDirectoryName(f[0]);
            a.Handled = true;
        };
    }
}

// ---------------------------------------------------------------- 스플라인 미리보기: 위에서 본 도로(차선, 방향 화살표, 인도) + 폭 + 단면(연석 높이는 과장)
public class SplinePreview : Drawn {
    public static readonly DependencyProperty SpecProperty = Dp("Spec", typeof(SplineSpec), typeof(SplinePreview), null);
    public static readonly DependencyProperty VersionProperty = Dp("Version", typeof(int), typeof(SplinePreview), 0);
    public SplineSpec Spec { get { return (SplineSpec)GetValue(SpecProperty); } set { SetValue(SpecProperty, value); } }
    public int Version { get { return (int)GetValue(VersionProperty); } set { SetValue(VersionProperty, value); } }
    public SplinePreview() { Height = 236; }

    protected override void OnRender(DrawingContext dc) {
        SplineSpec sp = Spec;
        double W = ActualWidth;
        if (sp == null || W < 50) return;
        string err; bool ok = sp.Valid(out err);
        double total = Math.Max(0.1, sp.TotalW), pad = 24;
        double k = (W - pad * 2) / total, x0 = pad, top = 14, bot = 146;
        bool dark = Theme.Dark;
        Color asph = dark ? Color.FromRgb(62, 64, 70) : Color.FromRgb(78, 80, 86);
        Color walk = dark ? Color.FromRgb(104, 100, 94) : Color.FromRgb(214, 208, 198);
        Color joint = dark ? Color.FromRgb(88, 85, 80) : Color.FromRgb(190, 184, 174);
        Color white = Color.FromRgb(240, 240, 240), yellow = Color.FromRgb(250, 200, 40);
        if (!ok) { dc.PushOpacity(0.35); }
        double xl = x0, xr0 = x0 + sp.WalkL * k, xr1 = xr0 + sp.RoadW * k;
        dc.PushClip(new RectangleGeometry(new Rect(x0, top, total * k, bot - top), 12, 12));
        // 인도 (보도블록 줄눈)
        DrawWalk(dc, new Rect(xl, top, sp.WalkL * k, bot - top), walk, joint, sp.WalkRepeat * k);
        DrawWalk(dc, new Rect(xr1, top, sp.WalkR * k, bot - top), walk, joint, sp.WalkRepeat * k);
        // 도로
        dc.DrawRectangle(Theme.Brush(asph), null, new Rect(xr0, top, sp.RoadW * k, bot - top));
        Pen edge = Art.Pen(Theme.A(white, 0.9), 2);
        dc.DrawLine(edge, new Point(xr0 + 3, top), new Point(xr0 + 3, bot));
        dc.DrawLine(edge, new Point(xr1 - 3, top), new Point(xr1 - 3, bot));
        List<SplineSpec.PathDef> paths = sp.Paths();
        int back = sp.OneWay ? 0 : sp.Lanes / 2;
        for (int i = 1; i < sp.Lanes; i++) {
            double lx = xr0 + i * sp.LaneW * k;
            if (i == back) {   // 마주 오는 차선 사이: 노란 겹선
                Pen y = Art.Pen(yellow, 2);
                dc.DrawLine(y, new Point(lx - 2.5, top), new Point(lx - 2.5, bot));
                dc.DrawLine(y, new Point(lx + 2.5, top), new Point(lx + 2.5, bot));
            } else {
                Pen d = Art.Pen(Theme.A(white, 0.85), 2);
                for (double yy = top + 6; yy < bot; yy += 26) dc.DrawLine(d, new Point(lx, yy), new Point(lx, Math.Min(bot, yy + 13)));
            }
        }
        // 차/사람 길: 차선마다 방향 화살표, 인도는 점선
        foreach (SplineSpec.PathDef pd in paths) {
            double px = x0 + (pd.X + sp.RoadW / 2 + sp.WalkL) * k;
            if (pd.Kind == 0) Arrow(dc, px, (top + bot) / 2, pd.Dir == 0, Theme.A(white, 0.95));
            else {
                Pen d = Art.RoundPen(Theme.A(Theme.Accent, 0.9), 2.2);
                for (double yy = top + 8; yy < bot - 4; yy += 12) dc.DrawLine(d, new Point(px, yy), new Point(px, yy + 3));
            }
        }
        dc.Pop();
        if (!ok) dc.Pop();
        // 폭 표시
        Color mut = Theme.Muted;
        double ly = bot + 8;
        Pen tick = Art.Pen(Theme.A(mut, 0.6), 1);
        List<double[]> parts = new List<double[]>();
        if (sp.WalkL > 0) parts.Add(new double[] { xl, xr0, sp.WalkL });
        if (sp.PerLane || sp.Lanes <= 4) for (int i = 0; i < sp.Lanes; i++) parts.Add(new double[] { xr0 + i * sp.LaneW * k, xr0 + (i + 1) * sp.LaneW * k, sp.LaneW });
        else parts.Add(new double[] { xr0, xr1, sp.RoadW });
        if (sp.WalkR > 0) parts.Add(new double[] { xr1, xr1 + sp.WalkR * k, sp.WalkR });
        foreach (double[] pt in parts) {
            dc.DrawLine(tick, new Point(pt[0] + 1, ly + 4), new Point(pt[1] - 1, ly + 4));
            dc.DrawLine(tick, new Point(pt[0] + 1, ly), new Point(pt[0] + 1, ly + 8));
            dc.DrawLine(tick, new Point(pt[1] - 1, ly), new Point(pt[1] - 1, ly + 8));
            FormattedText ft = Art.Ft(pt[2].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m", 11, false, mut);
            if (ft.Width < pt[1] - pt[0] - 4) dc.DrawText(ft, new Point((pt[0] + pt[1] - ft.Width) / 2, ly + 10));
        }
        // 단면: 연석 높이를 보이게 과장 (최대 14px)
        double py = 222, ch = sp.Curb > 0 ? Math.Min(14, 4 + sp.Curb * 40) : 0;
        Pen prof = Art.RoundPen(Theme.Text, 2);
        List<Point> pts = new List<Point>();
        pts.Add(new Point(xl, py - (sp.WalkL > 0 ? ch : 0)));
        if (sp.WalkL > 0) { pts.Add(new Point(xr0, py - ch)); pts.Add(new Point(xr0, py)); }
        pts.Add(new Point(xr1, py));
        if (sp.WalkR > 0) { pts.Add(new Point(xr1, py - ch)); pts.Add(new Point(xr1 + sp.WalkR * k, py - ch)); }
        dc.DrawGeometry(null, prof, Art.Poly(false, pts.ToArray()));
    }

    static void DrawWalk(DrawingContext dc, Rect r, Color fill, Color joint, double tile) {
        if (r.Width <= 0.5) return;
        dc.DrawRectangle(Theme.Brush(fill), null, r);
        Pen j = Art.Pen(joint, 1);
        tile = Math.Max(6, tile / 2);
        dc.PushClip(new RectangleGeometry(r));
        for (double x = r.X + tile; x < r.Right; x += tile) dc.DrawLine(j, new Point(x, r.Top), new Point(x, r.Bottom));
        for (double y = r.Y + tile; y < r.Bottom; y += tile) dc.DrawLine(j, new Point(r.X, y), new Point(r.Right, y));
        dc.Pop();
    }

    static void Arrow(DrawingContext dc, double x, double y, bool up, Color c) {
        double s = up ? -1 : 1;
        Pen p = Art.RoundPen(c, 2.6);
        dc.DrawLine(p, new Point(x, y - 16 * s), new Point(x, y + 16 * s));
        dc.DrawGeometry(null, p, Art.Poly(false, new Point(x - 7, y + 9 * s), new Point(x, y + 16 * s), new Point(x + 7, y + 9 * s)));
    }
}
}
