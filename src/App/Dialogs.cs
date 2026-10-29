using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Obj2x.UI {

// 같은 창 위에 어둡게 깔리는 대화 상자 (메시지, 면책 조항, 선택 목록 모두 이것을 쓴다)
public class DialogHost : Grid {
    public Button[] Btn;
    public CheckBox Check;          // 면책 조항 동의 체크 (있을 때만)
    Panel layer; Action<int> done;
    Rectangle shade; Border card; ScaleTransform sc; TranslateTransform tt;
    bool closing;

    public static DialogHost Show(Panel layer, string title, string body, UIElement extra, string[] btns, int primary, double width, Action<int> done) {
        DialogHost d = new DialogHost(layer, title, body, extra, btns, primary, width, done);
        layer.Children.Add(d);
        d.Intro();
        return d;
    }

    DialogHost(Panel layer, string title, string body, UIElement extra, string[] btns, int primary, double width, Action<int> done) {
        this.layer = layer; this.done = done;
        shade = new Rectangle(); shade.Fill = Brushes.Black; shade.Opacity = 0;
        Children.Add(shade);
        card = new Border(); card.Width = width; card.CornerRadius = new CornerRadius(28); card.BorderThickness = new Thickness(1); card.Padding = new Thickness(32, 32, 32, 28);
        card.SetResourceReference(Border.BackgroundProperty, "Card"); card.SetResourceReference(Border.BorderBrushProperty, "CardBdr");
        card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center;
        sc = new ScaleTransform(1, 1); tt = new TranslateTransform();
        TransformGroup tg = new TransformGroup(); tg.Children.Add(sc); tg.Children.Add(tt);
        card.RenderTransform = tg; card.RenderTransformOrigin = new Point(0.5, 0.5);
        Children.Add(card);

        StackPanel sp = new StackPanel(); card.Child = sp;
        TextBlock t = new TextBlock(); t.Text = title; t.FontSize = Art.Pt(17); t.FontWeight = FontWeights.Bold; t.TextWrapping = TextWrapping.Wrap;
        t.SetResourceReference(TextBlock.ForegroundProperty, "Text");
        sp.Children.Add(t);
        if (body != null) {
            TextBlock b = new TextBlock(); b.Text = body; b.TextWrapping = TextWrapping.Wrap; b.Margin = new Thickness(0, 16, 0, 0); b.LineHeight = 19;
            b.SetResourceReference(TextBlock.ForegroundProperty, "Text");
            sp.Children.Add(b);
        }
        if (extra != null) { FrameworkElement fe = extra as FrameworkElement; if (fe != null) fe.Margin = new Thickness(0, 20, 0, 0); sp.Children.Add(extra); }
        StackPanel bp = new StackPanel(); bp.Orientation = Orientation.Horizontal; bp.HorizontalAlignment = HorizontalAlignment.Right; bp.Margin = new Thickness(0, 22, 0, 0);
        sp.Children.Add(bp);
        Btn = new Button[btns.Length];
        for (int i = 0; i < btns.Length; i++) {
            Button bt = new Button(); bt.Content = btns[i]; bt.MinWidth = 96; bt.Height = 40;
            bt.SetResourceReference(StyleProperty, i == primary ? "BtnPrimary" : "BtnNormal");
            if (i > 0) bt.Margin = new Thickness(12, 0, 0, 0);
            int idx = i;
            bt.Click += delegate { Close(idx); };
            Btn[i] = bt; bp.Children.Add(bt);
        }
    }

    static DoubleAnimation Anim(double to, double sec) {
        DoubleAnimation a = new DoubleAnimation(to, Motion.D(sec));
        a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
        return a;
    }
    void Intro() {
        if (Motion.Off) { shade.Opacity = 0.59; return; }
        card.Opacity = 0; sc.ScaleX = sc.ScaleY = 0.92; tt.Y = 10;
        shade.BeginAnimation(OpacityProperty, Anim(0.59, 0.22));
        card.BeginAnimation(OpacityProperty, Anim(1, 0.18));
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(1, 0.22));
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1, 0.22));
        tt.BeginAnimation(TranslateTransform.YProperty, Anim(0, 0.22));
    }

    public void Close(int idx) {
        if (closing) return;
        closing = true;
        if (Motion.Off) { Remove(idx); return; }
        DoubleAnimation fade = Anim(0, 0.15);
        fade.Completed += delegate { Remove(idx); };
        card.BeginAnimation(OpacityProperty, Anim(0, 0.12));
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.96, 0.15));
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.96, 0.15));
        shade.BeginAnimation(OpacityProperty, fade);
    }
    void Remove(int idx) {
        layer.Children.Remove(this);
        Action<int> cb = done; done = null;
        if (cb != null) cb(idx);
    }
}

public static class Dialogs {
    // 확인 하나만 있는 안내
    public static DialogHost Msg(Panel layer, string title, string text) {
        return DialogHost.Show(layer, title, text, null, new string[] { L.T("wel_ok") }, 0, 480, null);
    }
    // 면책 조항. review=true면 닫기만, false면 체크 후 계속/종료. 계속이면 true.
    public static DialogHost Disclaimer(Panel layer, bool review, Action<bool> done) {
        CheckBox ck = null;
        if (!review) { ck = new CheckBox(); ck.Content = L.T("disclaimer_agree_chk"); ck.SetResourceReference(FrameworkElement.StyleProperty, "Chk"); }
        string[] btns = review ? new string[] { L.T("btn_close") } : new string[] { L.T("btn_exit"), L.T("btn_continue") };
        DialogHost d = DialogHost.Show(layer, L.T("disclaimer_title"), L.T("disclaimer_text"), ck, btns, btns.Length - 1, 520,
            delegate(int i) { if (done != null) done(review || i == 1); });
        if (!review) {
            d.Check = ck; Button ok = d.Btn[1]; ok.IsEnabled = false;
            ck.Checked += delegate { ok.IsEnabled = true; }; ck.Unchecked += delegate { ok.IsEnabled = false; };
        }
        return d;
    }
    // 라디오 선택. 확인하면 고른 값, 취소하면 null
    public static DialogHost Choose(Panel layer, string title, string[][] items, string cur, Action<string> done) {
        StackPanel list = new StackPanel();
        string group = "g" + Guid.NewGuid().ToString("N");
        string[] value = new string[] { cur };
        foreach (string[] it in items) {
            RadioButton rb = new RadioButton(); rb.Content = it[1]; rb.GroupName = group; rb.IsChecked = it[0] == cur; rb.Height = 46;
            rb.SetResourceReference(FrameworkElement.StyleProperty, "Radio");
            string v = it[0];
            rb.Checked += delegate { value[0] = v; };
            list.Children.Add(rb);
        }
        return DialogHost.Show(layer, title, null, list, new string[] { L.T("btn_cancel"), L.T("wel_ok") }, 1, 420,
            delegate(int i) { done(i == 1 ? value[0] : null); });
    }
}
}
