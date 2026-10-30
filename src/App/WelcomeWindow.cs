using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Obj2x.UI {

// 첫 실행 환영 화면: 인사말이 번갈아 나오고, 언어/보기 설정은 팝업으로 고른 뒤 시작을 누르면 면책 조항이 뜬다.
public class WelcomeWindow : Window {
    public event Action<bool> Done;          // 동의하면 true
    readonly Settings S;
    readonly Canvas cv = new Canvas();
    readonly Grid overlay = new Grid();
    readonly LogoArt tile = new LogoArt();
    readonly TextBlock title = new TextBlock(), gLang = new TextBlock(), gView = new TextBlock(), lblLang = new TextBlock(), lblView = new TextBlock();
    readonly Button start = new Button();
    readonly DispatcherTimer tm = new DispatcherTimer();
    int tick;
    bool finished;
    static readonly string[] Greetings = new string[] { "안녕하세요", "Hello" };
    public DialogHost LastDialog;

    public WelcomeWindow(Settings s) {
        S = s;
        Width = 1000; Height = 700; MinWidth = 760; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Maximized;
        Icon = Art.AppIcon();
        Grid host = new Grid();
        host.SetResourceReference(Panel.BackgroundProperty, "Bg");
        host.SetResourceReference(TextElement.FontFamilyProperty, "UiFont");
        TextOptions.SetTextFormattingMode(host, TextFormattingMode.Display);
        host.Children.Add(cv); host.Children.Add(overlay);
        Content = host;

        tile.Width = tile.Height = 150;
        title.FontSize = Art.Pt(30); title.FontWeight = FontWeights.Bold;
        FontFamily glyph = new FontFamily("Segoe MDL2 Assets");
        gLang.Text = ""; gView.Text = "";
        foreach (TextBlock g in new TextBlock[] { gLang, gView }) { g.FontFamily = glyph; g.FontSize = Art.Pt(20); g.Width = 44; g.Height = 44; g.TextAlignment = TextAlignment.Center; g.Padding = new Thickness(0, 8, 0, 0); }
        foreach (TextBlock l in new TextBlock[] { lblLang, lblView }) { l.FontSize = Art.Pt(17); l.FontWeight = FontWeights.Bold; l.Height = 44; l.Padding = new Thickness(0, 6, 0, 0); }
        foreach (TextBlock t in new TextBlock[] { gLang, gView, lblLang, lblView }) { t.Cursor = Cursors.Hand; t.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); }
        start.SetResourceReference(StyleProperty, "BtnPrimary");
        start.Height = 42; start.MinWidth = 110; start.FontWeight = FontWeights.Bold; start.Padding = new Thickness(24, 0, 24, 0);
        foreach (UIElement e in new UIElement[] { tile, title, gLang, lblLang, gView, lblView, start }) cv.Children.Add(e);

        MouseButtonEventHandler pickLang = delegate { PickLang(); };
        MouseButtonEventHandler pickView = delegate { PickView(); };
        gLang.MouseLeftButtonUp += pickLang; lblLang.MouseLeftButtonUp += pickLang;
        gView.MouseLeftButtonUp += pickView; lblView.MouseLeftButtonUp += pickView;
        start.Click += delegate { OnStart(); };
        SizeChanged += delegate { DoLayout(); };
        tm.Interval = TimeSpan.FromMilliseconds(40);
        tm.Tick += delegate { tick++; TickTitle(); };
        Loaded += delegate { tm.Start(); DoLayout(); };
        SourceInitialized += delegate { Theme.TitleBar(this); };
        Closed += delegate { tm.Stop(); if (!finished) { finished = true; if (Done != null) Done(false); } };
        ApplyAll();
    }

    void ApplyAll() {
        Loc.SetLang(S.Lang);
        Theme.Apply(S.Theme, S.Accent, S.SysFont);
        Title = L.T("app_title");
        foreach (string[] l in L.Languages) if (l[0] == L.Lang) lblLang.Text = l[1];
        lblView.Text = L.T("wel_view");
        start.Content = L.T("wel_start");
        Theme.TitleBar(this);
        TickTitle();
    }

    void TickTitle() {
        int cyc = tick % 60;
        double a = Math.Max(0, Math.Min(1.0, Math.Min(cyc / 8.0, (60 - cyc) / 8.0)));
        title.Text = Greetings[(tick / 60) % Greetings.Length];
        title.Foreground = Theme.Brush(Theme.Mix(Theme.Bg, Theme.Text, a));
    }

    void DoLayout() {
        double fw = ActualWidth > 0 ? ((FrameworkElement)Content).ActualWidth : Width, fh = ActualHeight > 0 ? ((FrameworkElement)Content).ActualHeight : Height;
        double lx = fw * 0.14, ty = fh * 0.24;
        Place(tile, lx + 14, ty + 14);
        Place(title, lx + 4, ty + 224);
        double rx = fw * 0.56, ry = fh * 0.56;
        Place(gLang, rx, ry); Place(lblLang, rx + 52, ry);
        Place(gView, rx, ry + 72); Place(lblView, rx + 52, ry + 72);
        start.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(start, fw - start.DesiredSize.Width - fw * 0.06, fh * 0.80);
    }
    static void Place(UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); }

    void PickLang() {
        LastDialog = Dialogs.Choose(overlay, L.T("wel_lang"), L.Languages, S.Lang,
            delegate(string r) { if (r != null && r != S.Lang) { S.Lang = r; ApplyAll(); } });
    }

    void PickView() {
        LastDialog = Dialogs.Choose(overlay, L.T("wel_view"), new string[][] {
            new string[] { "system", L.T("theme_system") }, new string[] { "light", L.T("theme_light") }, new string[] { "dark", L.T("theme_dark") } }, S.Theme,
            delegate(string r) { if (r != null && r != S.Theme) { S.Theme = r; ApplyAll(); } });
    }

    void OnStart() {
        start.IsEnabled = false;
        LastDialog = Dialogs.Disclaimer(overlay, false, delegate(bool ok) {
            if (ok) { S.Agreed = true; S.AgreedVer = AppInfo.Version; }
            else start.IsEnabled = true;
            if (!ok) { Close(); return; }
            finished = true;
            if (Done != null) Done(true);
        });
    }
}
}
