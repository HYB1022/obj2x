using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Obj2x.UI;

// 코드에서 만드는 컨트롤에 XAML처럼 모양/색을 붙이는 도구
public static class Res {
    public static T Th<T>(T c, string theme) where T : StyledElement {
        if (Application.Current.TryFindResource(theme, out object o) && o is ControlTheme ct) c.Theme = ct;
        return c;
    }
    public static T Dyn<T>(T c, AvaloniaProperty p, string key) where T : AvaloniaObject {
        c[!p] = new DynamicResourceExtension(key);
        return c;
    }
}

// 같은 창 위에 어둡게 깔리는 대화 상자 (메시지, 면책 조항, 선택 목록 모두 이것을 쓴다)
public class DialogHost : Panel {
    public Button[] Btn;
    public CheckBox Check;          // 면책 조항 동의 체크 (있을 때만)
    readonly Panel layer; Action<int> done;
    readonly Rectangle shade; readonly Border card; readonly ScaleTransform sc; readonly TranslateTransform tt;
    bool closing;

    public static DialogHost Show(Panel layer, string title, string body, Control extra, string[] btns, int primary, double width, Action<int> done) {
        DialogHost d = new DialogHost(layer, title, body, extra, btns, primary, width, done);
        layer.Children.Add(d);
        d.Intro();
        return d;
    }

    DialogHost(Panel layer, string title, string body, Control extra, string[] btns, int primary, double width, Action<int> done) {
        this.layer = layer; this.done = done;
        shade = new Rectangle { Fill = Brushes.Black, Opacity = 0 };
        Children.Add(shade);
        card = new Border { Width = width, CornerRadius = new CornerRadius(28), BorderThickness = new Thickness(1), Padding = new Thickness(32, 32, 32, 28),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Res.Dyn(card, Border.BackgroundProperty, "Card"); Res.Dyn(card, Border.BorderBrushProperty, "CardBdr");
        sc = new ScaleTransform(1, 1); tt = new TranslateTransform();
        card.RenderTransform = new TransformGroup { Children = { sc, tt } };
        card.RenderTransformOrigin = RelativePoint.Center;
        Children.Add(card);

        StackPanel sp = new StackPanel(); card.Child = sp;
        TextBlock t = new TextBlock { Text = title, FontSize = Art.Pt(17), FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
        Res.Dyn(t, TextBlock.ForegroundProperty, "Text");
        sp.Children.Add(t);
        if (body != null) {
            TextBlock b = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0), LineHeight = 19 };
            Res.Dyn(b, TextBlock.ForegroundProperty, "Text");
            sp.Children.Add(b);
        }
        if (extra != null) { extra.Margin = new Thickness(0, 20, 0, 0); sp.Children.Add(extra); }
        StackPanel bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        sp.Children.Add(bp);
        Btn = new Button[btns.Length];
        for (int i = 0; i < btns.Length; i++) {
            Button bt = Res.Th(new Button { Content = btns[i], MinWidth = 96, Height = 40 }, i == primary ? "BtnPrimary" : "BtnNormal");
            if (i > 0) bt.Margin = new Thickness(12, 0, 0, 0);
            int idx = i;
            bt.Click += (s, e) => Close(idx);
            Btn[i] = bt; bp.Children.Add(bt);
        }
    }

    void Intro() {
        if (Motion.Disabled) { shade.Opacity = 0.59; return; }
        card.Opacity = 0; sc.ScaleX = sc.ScaleY = 0.92; tt.Y = 10;
        Tween.Run(shade, "op", 0, 0.59, Motion.Sec(0.22), v => shade.Opacity = v);
        Tween.Run(card, "op", 0, 1, Motion.Sec(0.18), v => card.Opacity = v);
        Tween.Run(sc, "s", 0.92, 1, Motion.Sec(0.22), v => sc.ScaleX = sc.ScaleY = v);
        Tween.Run(tt, "y", 10, 0, Motion.Sec(0.22), v => tt.Y = v);
    }

    public void Close(int idx) {
        if (closing) return;
        closing = true;
        if (Motion.Disabled) { Remove(idx); return; }
        Tween.Run(card, "op", card.Opacity, 0, Motion.Sec(0.12), v => card.Opacity = v);
        Tween.Run(sc, "s", sc.ScaleX, 0.96, Motion.Sec(0.15), v => sc.ScaleX = sc.ScaleY = v);
        Tween.Run(shade, "op", shade.Opacity, 0, Motion.Sec(0.15), v => shade.Opacity = v, null, () => Remove(idx));
    }
    void Remove(int idx) {
        layer.Children.Remove(this);
        Action<int> cb = done; done = null;
        cb?.Invoke(idx);
    }
}

public static class Dialogs {
    // 확인 하나만 있는 안내
    public static DialogHost Msg(Panel layer, string title, string text) {
        return DialogHost.Show(layer, title, text, null, new[] { L.T("wel_ok") }, 0, 480, null);
    }
    // 면책 조항. review=true면 닫기만, false면 체크 후 계속/종료. 계속이면 true.
    public static DialogHost Disclaimer(Panel layer, bool review, Action<bool> done) {
        CheckBox ck = null;
        if (!review) ck = Res.Th(new CheckBox { Content = L.T("disclaimer_agree_chk") }, "Chk");
        string[] btns = review ? new[] { L.T("btn_close") } : new[] { L.T("btn_exit"), L.T("btn_continue") };
        StackPanel extra = new StackPanel();
        if (ck != null) extra.Children.Add(ck);
        extra.Children.Add(Credits());
        DialogHost d = DialogHost.Show(layer, L.T("disclaimer_title"), L.T("disclaimer_text"), extra, btns, btns.Length - 1, 520,
            i => done?.Invoke(review || i == 1));
        if (!review) {
            d.Check = ck; Button ok = d.Btn[1]; ok.IsEnabled = false;
            ck.IsCheckedChanged += (s, e) => ok.IsEnabled = ck.IsChecked == true;
        }
        return d;
    }
    // 면책 조항 맨 아래 크레딧: 이름을 누르면 GitHub(또는 소개 페이지)가 열린다
    static readonly string[][] People = {
        new[] { "HYB1022", "https://github.com/HYB1022" },
        new[] { "qbasys08", "https://github.com/qbasys08" },
        new[] { "Claude (Anthropic)", "https://www.anthropic.com/claude" } };
    static Control Credits() {
        StackPanel sp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        sp.Children.Add(Res.Th(new Rectangle(), "Div"));
        sp.Children.Add(Res.Th(new TextBlock { Text = L.T("disc_credits"), Margin = new Thickness(0, 12, 0, 4) }, "TTiny"));
        WrapPanel names = new WrapPanel();
        for (int i = 0; i < People.Length; i++) {
            if (i > 0) names.Children.Add(Res.Dyn(new TextBlock { Text = "  ·  " }, TextBlock.ForegroundProperty, "Muted"));
            string url = People[i][1];
            TextBlock h = Res.Dyn(new TextBlock { Text = People[i][0], Cursor = new Cursor(StandardCursorType.Hand) }, TextBlock.ForegroundProperty, "Accent");
            ToolTip.SetTip(h, url);
            h.PointerReleased += (s, e) => Updater.OpenUrl(url);
            names.Children.Add(h);
        }
        sp.Children.Add(names);
        return sp;
    }

    // 취소 / [확인 문구] 두 단추. 확인이면 true
    public static DialogHost Confirm(Panel layer, string title, string body, string okText, Action<bool> done) {
        return DialogHost.Show(layer, title, body, null, new[] { L.T("btn_cancel"), okText }, 1, 500, i => done?.Invoke(i == 1));
    }

    // 라디오 선택. 확인하면 고른 값, 취소하면 null
    public static DialogHost Choose(Panel layer, string title, string[][] items, string cur, Action<string> done) {
        StackPanel list = new StackPanel();
        string group = "g" + Guid.NewGuid().ToString("N");
        string[] value = { cur };
        foreach (string[] it in items) {
            RadioButton rb = Res.Th(new RadioButton { Content = it[1], GroupName = group, IsChecked = it[0] == cur, Height = 46 }, "Radio");
            string v = it[0];
            rb.IsCheckedChanged += (s, e) => { if (rb.IsChecked == true) value[0] = v; };
            list.Children.Add(rb);
        }
        return DialogHost.Show(layer, title, null, list, new[] { L.T("btn_cancel"), L.T("wel_ok") }, 1, 420, i => done(i == 1 ? value[0] : null));
    }
}
