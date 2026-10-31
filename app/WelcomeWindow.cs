using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Obj2x.UI;

// 첫 실행 환영 화면 (HyperOS 첫 설정 화면 모양): 흐르는 그라데이션 위에 로고, 모든 언어로 번갈아 나오는 큰 인사말,
// 언어/보기 설정 카드, 아래쪽 둥근 화살표 버튼. 화살표를 누르면 면책 조항이 뜬다.
public class WelcomeWindow : Window {
    public event Action<bool> Done;          // 동의하면 true
    readonly Settings S;
    readonly Panel host = new Panel();
    readonly Panel overlay = new Panel();
    readonly TextBlock title = new TextBlock(), sub = new TextBlock(), lblLang = new TextBlock(), lblView = new TextBlock(), valLang = new TextBlock(), valView = new TextBlock();
    readonly TranslateTransform titleMove = new TranslateTransform();
    readonly Button start = new Button(), rowLang = new Button(), rowView = new Button();
    readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
    bool ticking;
    const double Cycle = 2.6, Fade = 0.45;     // 인사말 하나가 머무는 시간 / 올라오며 나타나고 올라가며 사라지는 시간(초)
    bool finished;
    readonly List<string> greetings = new();
    readonly HeroBackdrop backdrop = new HeroBackdrop();
    double seekT = double.NaN;                 // 개발용(--welcome-frames): 정한 시간으로 그린다
    public DialogHost LastDialog;
    public Panel Host => host;

    public WelcomeWindow(Settings s) {
        S = s;
        Width = 1000; Height = 700; MinWidth = 760; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        try { Icon = Art.AppIcon(); } catch (Exception) { }
        Res.Dyn(host, Panel.BackgroundProperty, "Bg");
        Res.Dyn(host, TextElement.FontFamilyProperty, "UiFont");
        Res.Dyn(host, TextElement.ForegroundProperty, "Text");
        Res.Dyn(this, FontFamilyProperty, "UiFont");
        Content = host;

        // 가운데 줄: 로고 → 인사말 → 설명 → 설정 카드
        StackPanel mid = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 640, Margin = new Thickness(0, 0, 0, 110) };
        mid.Children.Add(new LogoArt { Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center });

        Panel tbox = new Panel { Height = 96, Margin = new Thickness(0, 26, 0, 0), ClipToBounds = true };   // 인사말이 지나가는 창 (위아래로 잘린다)
        title.FontSize = 58; title.FontWeight = FontWeight.Bold;
        title.HorizontalAlignment = HorizontalAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center;
        title.RenderTransform = titleMove;
        tbox.Children.Add(title);
        mid.Children.Add(tbox);

        sub.FontSize = 14; sub.TextWrapping = TextWrapping.Wrap; sub.TextAlignment = TextAlignment.Center;
        sub.Margin = new Thickness(10, 6, 10, 0); sub.LineHeight = 22;
        Res.Dyn(sub, TextBlock.ForegroundProperty, "Muted");
        mid.Children.Add(sub);

        Border card = Res.Th(new Border { Margin = new Thickness(0, 36, 0, 0), Width = 460 }, "DevCard");
        StackPanel rows = new StackPanel();
        rows.Children.Add(MakeRow(rowLang, "lang", lblLang, valLang));
        rows.Children.Add(MakeRow(rowView, "view", lblView, valView));
        card.Child = rows;
        mid.Children.Add(card);

        // 아래쪽 가운데: 둥근 화살표 버튼
        Res.Th(start, "BtnRound");
        start.HorizontalAlignment = HorizontalAlignment.Center; start.VerticalAlignment = VerticalAlignment.Bottom;
        start.Margin = new Thickness(0, 0, 0, 64);

        host.Children.Add(backdrop);
        host.Children.Add(mid);
        host.Children.Add(start);
        host.Children.Add(overlay);

        rowLang.Click += (o, e) => PickLang();
        rowView.Click += (o, e) => PickView();
        start.Click += (o, e) => OnStart();
        Opened += (o, e) => { Look.FitToScreen(this); StartTicking(); };
        Closed += (o, e) => { StopTicking(); if (!finished) { finished = true; Done?.Invoke(false); } };

        // 인사말: 언어 파일마다 그 언어로 쓴 인사(wel_title). 지금 언어부터 시작한다
        Loc.SetLang(S.Lang);
        AddGreeting(L.TIn(S.Lang, "wel_title"));
        foreach (string[] l in L.Languages) AddGreeting(L.TIn(l[0], "wel_title"));
        if (greetings.Count == 0) greetings.Add("Hello");
        ApplyAll();
    }

    void AddGreeting(string g) { if (!string.IsNullOrEmpty(g) && !greetings.Contains(g)) greetings.Add(g); }
    public string[] Greetings => greetings.ToArray();   // 셀프 테스트용

    // HyperOS 설정 줄: [색 타일] 제목 ........ 값 >
    static Button MakeRow(Button b, string icon, TextBlock label, TextBlock value) {
        Res.Th(b, "DevRow");
        DockPanel d = new DockPanel();
        StackPanel right = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(right, Dock.Right);
        Res.Th(value, "DevValue");
        right.Children.Add(value); right.Children.Add(Res.Th(new Path(), "DevChevron"));
        d.Children.Add(right);
        d.Children.Add(new TileIcon { NameKey = icon, Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Center });
        Res.Th(label, "DevTitle");
        label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new Thickness(14, 0, 0, 0);
        d.Children.Add(label);
        b.Content = d;
        return b;
    }

    void ApplyAll() {
        Loc.SetLang(S.Lang);
        Look.Apply(S.Theme, S.Accent, S.Font);
        Title = L.T("app_title");
        foreach (string[] l in L.Languages) if (l[0] == L.Lang) valLang.Text = l[1];
        lblLang.Text = L.T("wel_lang");
        lblView.Text = L.T("wel_view");
        valView.Text = L.T("theme_" + S.Theme);
        sub.Text = L.T("wel_sub");
        ToolTip.SetTip(start, L.T("wel_start"));
        TickTitle();
    }

    // 화면이 새로 그려질 때마다 지난 시간으로 위치/투명도를 계산한다
    void StartTicking() { if (ticking) return; ticking = true; clock.Start(); RequestAnimationFrame(OnFrame); }
    void StopTicking() { ticking = false; clock.Stop(); }
    void OnFrame(TimeSpan t) { if (!ticking) return; TickTitle(); RequestAnimationFrame(OnFrame); }

    void TickTitle() {
        if (Motion.Disabled && double.IsNaN(seekT)) {   // 애니메이션 꺼짐: 지금 언어의 인사만
            title.Text = greetings[0]; title.Opacity = 1; titleMove.Y = 0; return;
        }
        double t = double.IsNaN(seekT) ? clock.Elapsed.TotalSeconds / Math.Max(0.05, Motion.Scale) : seekT;   // 개발자 옵션 애니메이션 배율
        double period = Cycle * greetings.Count;
        t = ((t % period) + period) % period;
        int idx = (int)(t / Cycle) % greetings.Count;
        double c = t % Cycle;
        double rise = 22;
        if (title.Text != greetings[idx]) title.Text = greetings[idx];
        if (c < Fade) {                            // 아래에서 올라오며 나타난다
            double e = Motion.EaseOut(c / Fade);
            title.Opacity = e; titleMove.Y = (1 - e) * rise;
        } else if (c > Cycle - Fade) {             // 위로 올라가며 사라진다
            double u = (c - (Cycle - Fade)) / Fade, e = u * u * u;
            title.Opacity = 1 - e; titleMove.Y = -e * rise;
        } else { title.Opacity = 1; titleMove.Y = 0; }
    }

    // 개발용: t초 순간의 인사말과 배경 (README 움직이는 그림을 한 장씩 찍을 때)
    public double Period => Cycle * greetings.Count;
    public void Seek(double t) { seekT = t; TickTitle(); backdrop.Seek(t); }

    void PickLang() {
        LastDialog = Dialogs.Choose(overlay, L.T("wel_lang"), L.Languages, S.Lang, r => { if (r != null && r != S.Lang) { S.Lang = r; ApplyAll(); } });
    }

    void PickView() {
        LastDialog = Dialogs.Choose(overlay, L.T("wel_view"), new[] {
            new[] { "system", L.T("theme_system") }, new[] { "light", L.T("theme_light") }, new[] { "dark", L.T("theme_dark") } }, S.Theme,
            r => { if (r != null && r != S.Theme) { S.Theme = r; ApplyAll(); } });
    }

    void OnStart() {
        start.IsEnabled = false;
        LastDialog = Dialogs.Disclaimer(overlay, false, ok => {
            if (ok) { S.Agreed = true; S.AgreedVer = AppInfo.Version; }
            else start.IsEnabled = true;
            if (!ok) { Close(); return; }
            finished = true;
            Done?.Invoke(true);
        });
    }
}
