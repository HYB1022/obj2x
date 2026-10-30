using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Obj2x.UI {

// 라이트/다크 × 인디고/틸/로즈 색 팔레트. Apply하면 앱 리소스의 브러시를 바꿔서
// XAML의 {DynamicResource 이름} 과 직접 그리는 컨트롤(Changed 이벤트)이 함께 바뀐다.
public static class Theme {
    public static bool Dark;
    public static string AccentName = "indigo";
    public static Color Accent, AccentHov, NavPill, NavPillBdr, NavPillText;
    public static Color Bg, Sidebar, Card, CardBdr, Input, InputBdr, Btn, BtnHov, Text, Muted, Divider, DisBg, DisText, ToggleOff, Ok, Warn, Bad, Dim;
    public static Color BnBg, BnBorder, BnText, BnBtnBg, BnBtnFg, BnBtnHov;
    public static FontFamily Font = new FontFamily("Malgun Gothic");
    public static event Action Changed;

    static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }

    public static bool SystemIsDark() {
        try {
            object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            if (v is int) return ((int)v) == 0;
        } catch (Exception) { }
        return false;
    }

    public static void Apply(string theme, string accent, string font) {
        bool dark = theme == "dark" || (theme == "system" && SystemIsDark());
        Dark = dark;
        AccentName = (accent == "teal" || accent == "rose") ? accent : "indigo";
        if (accent == "teal") {
            if (dark) { Accent = C(20, 184, 166); AccentHov = C(45, 212, 191); NavPill = C(19, 56, 56); NavPillBdr = C(30, 84, 84); NavPillText = C(204, 251, 241); }
            else { Accent = C(13, 148, 136); AccentHov = C(15, 118, 110); NavPill = C(240, 253, 250); NavPillBdr = C(153, 246, 228); NavPillText = C(17, 94, 89); }
        } else if (accent == "rose") {
            if (dark) { Accent = C(244, 63, 94); AccentHov = C(251, 113, 133); NavPill = C(61, 27, 40); NavPillBdr = C(95, 38, 60); NavPillText = C(255, 228, 230); }
            else { Accent = C(225, 29, 72); AccentHov = C(190, 18, 60); NavPill = C(255, 241, 242); NavPillBdr = C(254, 205, 211); NavPillText = C(159, 18, 57); }
        } else {
            if (dark) { Accent = C(129, 140, 248); AccentHov = C(165, 180, 252); NavPill = C(43, 51, 86); NavPillBdr = C(63, 71, 115); NavPillText = C(224, 231, 255); }
            else { Accent = C(79, 70, 229); AccentHov = C(67, 56, 202); NavPill = C(238, 242, 255); NavPillBdr = C(199, 210, 254); NavPillText = C(55, 48, 163); }
        }
        if (dark) {
            Bg = C(18, 20, 26); Sidebar = C(24, 26, 34); Card = C(32, 35, 46); CardBdr = C(44, 49, 64);
            Input = C(22, 24, 32); InputBdr = C(54, 59, 78); Btn = C(42, 46, 60); BtnHov = C(54, 59, 78);
            Text = C(241, 243, 249); Muted = C(148, 155, 174); Divider = C(40, 44, 58);
            DisBg = C(28, 30, 40); DisText = C(90, 96, 114); ToggleOff = C(60, 65, 84);
            Ok = C(76, 209, 143); Warn = C(242, 180, 75); Bad = C(255, 107, 107); Dim = C(98, 104, 124);
            BnBg = C(72, 60, 22); BnBorder = C(214, 160, 20); BnText = C(255, 238, 180);
            BnBtnBg = C(255, 226, 130); BnBtnFg = C(30, 24, 10); BnBtnHov = C(255, 236, 165);
        } else {
            Bg = C(245, 246, 250); Sidebar = C(239, 241, 247); Card = C(255, 255, 255); CardBdr = C(230, 233, 242);
            Input = C(255, 255, 255); InputBdr = C(218, 222, 233); Btn = C(238, 240, 247); BtnHov = C(228, 231, 240);
            Text = C(28, 31, 42); Muted = C(100, 110, 130); Divider = C(235, 237, 245);
            DisBg = C(230, 232, 239); DisText = C(160, 165, 178); ToggleOff = C(209, 213, 219);
            Ok = C(30, 130, 80); Warn = C(176, 100, 0); Bad = C(186, 26, 26); Dim = C(154, 152, 164);
            BnBg = C(255, 240, 186); BnBorder = C(224, 168, 16); BnText = C(33, 28, 10);
            BnBtnBg = C(30, 24, 10); BnBtnFg = Colors.White; BnBtnHov = C(62, 52, 28);
        }
        Font = MakeFont(font);
        Push();
        if (Changed != null) Changed();
    }

    // 글꼴: nanum(나눔스퀘어 네오) | noto(Noto Sans KR) | system(Windows 기본). exe 옆 fonts 폴더의 것을 먼저 쓰고,
    // 없으면 설치된 것, 그것도 없으면 맑은 고딕. 한자/가나가 없는 글꼴이라 중국어/일본어일 땐 그 언어 글꼴이 먼저 대신 그린다.
    static readonly string[][] Bundled = new string[][] {
        new string[] { "nanum", "NanumSquare Neo", "NanumSquareNeo-bRg.ttf" },
        new string[] { "noto", "Noto Sans KR", "NotoSansKR-Regular.ttf" } };
    public static FontFamily MakeFont(string font) {
        string cjk = L.Lang == "zh" ? "Microsoft YaHei UI, Microsoft YaHei, " : (L.Lang == "ja" ? "Yu Gothic UI, Meiryo UI, Meiryo, " : "");
        string tail = cjk + "Malgun Gothic, Microsoft YaHei UI, Yu Gothic UI, Segoe UI";
        try {
            if (font == "system") return new FontFamily(SystemFonts.MessageFontFamily.Source + ", " + tail);
            if (font == "misans") {
                FontFamily mi = MiSans();
                if (mi != null) return new FontFamily(mi.BaseUri, mi.Source + ", " + tail);
                font = "nanum";                   // 없으면 기본 글꼴로
            }
            foreach (string[] b in Bundled) {
                if (b[0] != font) continue;
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts");
                if (System.IO.File.Exists(System.IO.Path.Combine(dir, b[2])))
                    return new FontFamily(new Uri(dir + System.IO.Path.DirectorySeparatorChar), "./#" + b[1] + ", " + tail);
                return new FontFamily(b[1] + ", " + tail);
            }
        } catch (Exception) { }
        return new FontFamily(tail);
    }
    // MiSans: 라이선스상 글꼴 파일을 함께 배포할 수 없어서 넣지 않는다. 사용자가 Windows에 설치했거나
    // exe 옆 fonts 폴더에 MiSans*.ttf/otf를 넣어 두었으면 그것을 쓴다. 없으면 null.
    static FontFamily miSans; static bool miChecked;
    public static FontFamily MiSans() {
        if (miChecked) return miSans;
        miChecked = true;
        try {
            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts");
            if (System.IO.Directory.Exists(dir) && (System.IO.Directory.GetFiles(dir, "MiSans*.ttf").Length > 0 || System.IO.Directory.GetFiles(dir, "MiSans*.otf").Length > 0))
                miSans = PickMi(Fonts.GetFontFamilies(new Uri(dir + System.IO.Path.DirectorySeparatorChar)));
            if (miSans == null) miSans = PickMi(Fonts.SystemFontFamilies);
        } catch (Exception) { miSans = null; }
        return miSans;
    }
    // 이름이 MiSans인 것을 먼저, 없으면 MiSans Normal/Regular, 그다음 MiSans로 시작하는 아무거나 (Latin 등 일부 글자 전용판은 피한다)
    static FontFamily PickMi(System.Collections.Generic.ICollection<FontFamily> fams) {
        FontFamily best = null; int score = 0;
        foreach (FontFamily f in fams) {
            foreach (string n in f.FamilyNames.Values) {
                int sc = n == "MiSans" ? 4 : (n == "MiSans Normal" || n == "MiSans Regular") ? 3 : (n.StartsWith("MiSans") && n.IndexOf("Latin") < 0) ? 1 : 0;
                if (sc > score) { score = sc; best = f; }
            }
        }
        return best;
    }

    public static bool BundledFont(string font) {
        foreach (string[] b in Bundled) if (b[0] == font) return System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fonts", b[2]));
        return false;
    }

    // 앱 리소스에 브러시로 넣는다
    static void Push() {
        if (Application.Current == null) return;
        ResourceDictionary r = Application.Current.Resources;
        Set(r, "Accent", Accent); Set(r, "AccentHov", AccentHov); Set(r, "NavPill", NavPill); Set(r, "NavPillBdr", NavPillBdr); Set(r, "NavPillText", NavPillText);
        Set(r, "Bg", Bg); Set(r, "Sidebar", Sidebar); Set(r, "Card", Card); Set(r, "CardBdr", CardBdr);
        Set(r, "Input", Input); Set(r, "InputBdr", InputBdr); Set(r, "Btn", Btn); Set(r, "BtnHov", BtnHov);
        Set(r, "Text", Text); Set(r, "Muted", Muted); Set(r, "Divider", Divider);
        Set(r, "DisBg", DisBg); Set(r, "DisText", DisText); Set(r, "ToggleOff", ToggleOff);
        Set(r, "Ok", Ok); Set(r, "Warn", Warn); Set(r, "Bad", Bad); Set(r, "Dim", Dim);
        Set(r, "BnBg", BnBg); Set(r, "BnBorder", BnBorder); Set(r, "BnText", BnText);
        Set(r, "BnBtnBg", BnBtnBg); Set(r, "BnBtnFg", BnBtnFg); Set(r, "BnBtnHov", BnBtnHov);
        Set(r, "Busy", C(255, 152, 0));
        // [업데이트] 같은 연한 강조색 알약
        Set(r, "AccentSoft", Color.FromArgb((byte)(Dark ? 70 : 34), Accent.R, Accent.G, Accent.B));
        Set(r, "AccentSoftText", Dark ? Mix(Accent, Colors.White, 0.45) : Accent);
        r["UiFont"] = Font;
    }
    static void Set(ResourceDictionary r, string k, Color c) { r[k] = Brush(c); }

    // 같은 색이면 같은 브러시를 다시 쓴다 (직접 그리는 컨트롤이 그릴 때마다 새로 만들지 않게). 화면 스레드에서만 부른다.
    static readonly Dictionary<Color, SolidColorBrush> brushes = new Dictionary<Color, SolidColorBrush>();
    public static SolidColorBrush Brush(Color c) {
        SolidColorBrush b;
        if (brushes.TryGetValue(c, out b)) return b;
        if (brushes.Count > 4096) brushes.Clear();   // 애니메이션 중간색이 쌓이지 않게
        b = new SolidColorBrush(c); b.Freeze();
        brushes[c] = b;
        return b;
    }

    // 테마를 바꿀 때 지금 화면을 찍어 위에 덮고, 새 테마를 적용한 뒤 찍은 화면을 서서히 지운다
    public static void Crossfade(Panel host, Action apply) {
        int w = (int)host.ActualWidth, h = (int)host.ActualHeight;
        if (Motion.Disabled || w < 1 || h < 1 || !host.IsVisible) { apply(); return; }
        double sc = 1;
        PresentationSource ps = PresentationSource.FromVisual(host);
        if (ps != null && ps.CompositionTarget != null) sc = ps.CompositionTarget.TransformToDevice.M11;
        RenderTargetBitmap shot = new RenderTargetBitmap((int)Math.Ceiling(w * sc), (int)Math.Ceiling(h * sc), 96 * sc, 96 * sc, PixelFormats.Pbgra32);
        shot.Render(host);
        Image img = new Image(); img.Source = shot; img.Stretch = Stretch.Fill; img.IsHitTestVisible = false;
        img.Width = w; img.Height = h; img.HorizontalAlignment = HorizontalAlignment.Left; img.VerticalAlignment = VerticalAlignment.Top;
        Panel.SetZIndex(img, 10000);
        host.Children.Add(img);
        apply();
        DoubleAnimation a = new DoubleAnimation(1, 0, Motion.D(0.35));
        a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
        a.Completed += delegate { host.Children.Remove(img); img.Source = null; };
        img.BeginAnimation(UIElement.OpacityProperty, a);
    }
    // 창이 작업 영역(작업 표시줄 제외)보다 크면 줄인다. 최소 크기도 같이 줄여서 잘리지 않게 한다
    public static void FitToScreen(Window w) {
        Rect wa = SystemParameters.WorkArea;
        double mw = wa.Width - 40, mh = wa.Height - 40;
        if (w.Width > mw) { w.Width = mw; w.MinWidth = Math.Min(w.MinWidth, mw); }
        if (w.Height > mh) { w.Height = mh; w.MinHeight = Math.Min(w.MinHeight, mh); }
    }

    public static Color A(Color c, double a) { return Color.FromArgb((byte)Math.Max(0, Math.Min(255, c.A * a)), c.R, c.G, c.B); }
    public static Color Mix(Color a, Color b, double t) {
        return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    // 제목 표시줄을 테마에 맞춘다 (Windows 10 2004 이상)
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    public static void TitleBar(Window w) {
        try {
            IntPtr h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero) return;
            int v = Dark ? 1 : 0;
            DwmSetWindowAttribute(h, 20, ref v, 4);
        } catch (Exception) { }
    }
}

// 움직임 공통 설정
public static class Motion {
    public static bool Off;          // 셀프 테스트/화면 캡처: 모든 움직임을 즉시 끝낸다
    public static double Scale = 1;  // 개발자 옵션 '애니메이션 배율' (0 = 끔)
    public static bool Disabled { get { return Off || Scale <= 0; } }
    public static Duration D(double sec) { return new Duration(TimeSpan.FromSeconds(Disabled ? 0 : sec * Scale)); }
    public static double EaseOut(double t) { if (t <= 0) return 0; if (t >= 1) return 1; double u = 1 - t; return 1 - u * u * u; }
}
}
