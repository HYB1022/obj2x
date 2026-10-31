using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Obj2x.UI;

// 라이트/다크 × 인디고/틸/로즈 색 팔레트. Apply하면 앱 리소스의 브러시를 바꿔서
// XAML의 {DynamicResource 이름} 과 직접 그리는 컨트롤(Changed 이벤트)이 함께 바뀐다.
public static class Look {
    public static bool Dark;
    public static string AccentName = "indigo";
    public static Color Accent, AccentHov, NavPill, NavPillBdr, NavPillText;
    public static Color Bg, Sidebar, Card, CardBdr, Input, InputBdr, Btn, BtnHov, Text, Muted, Divider, DisBg, DisText, ToggleOff, Ok, Warn, Bad, Dim;
    public static Color BnBg, BnBorder, BnText, BnBtnBg, BnBtnFg, BnBtnHov;
    public static FontFamily Font = FontFamily.Default;
    public static event Action Changed;

    static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }

    // Windows는 레지스트리, 맥은 Avalonia가 알려 주는 시스템 테마
    public static bool SystemIsDark() {
        try {
            if (Omsi.IsWindows) {
                object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                if (v is int i) return i == 0;
                return false;
            }
            var ps = Application.Current?.PlatformSettings;
            if (ps != null) return ps.GetColorValues().ThemeVariant == Avalonia.Platform.PlatformThemeVariant.Dark;
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
        Changed?.Invoke();
    }

    // 글꼴: nanum(나눔스퀘어 네오) | noto(Noto Sans KR) | misans | system. exe 옆 fonts 폴더의 것을 먼저 쓰고 (FileFonts),
    // 없으면 설치된 것, 그다음 시스템 한글 글꼴. 한자/가나가 없는 글꼴이라 중국어/일본어일 땐 그 언어 글꼴이 먼저 대신 그린다.
    static readonly string[][] Bundled = {
        new[] { "nanum", "NanumSquare Neo", "NanumSquareNeo-bRg.ttf" },
        new[] { "noto", "Noto Sans KR", "NotoSansKR-Regular.ttf" } };
    // Avalonia는 "avares://...#이름"과 시스템 글꼴을 한 목록에 섞지 못한다. 함께 넣은 글꼴은 하나만 주고,
    // 없는 글자(한자/가나 등)는 Avalonia가 시스템 글꼴에서 알아서 찾는다. 중국어/일본어는 그 언어 글꼴을 먼저 (예전과 같다)
    public static FontFamily MakeFont(string font) {
        string sys = (Omsi.IsWindows ? "Malgun Gothic, Segoe UI" : "Apple SD Gothic Neo, Helvetica Neue");
        try {
            if (L.Lang == "zh") return new FontFamily(Omsi.IsWindows ? "Microsoft YaHei UI, Microsoft YaHei" : "PingFang SC, Hiragino Sans GB");
            if (L.Lang == "ja") return new FontFamily(Omsi.IsWindows ? "Yu Gothic UI, Meiryo UI, Meiryo" : "Hiragino Sans, Hiragino Kaku Gothic ProN");
            if (font == "system") return new FontFamily((Omsi.IsWindows ? "Segoe UI, " : "Helvetica Neue, ") + sys);
            if (font == "misans") {
                if (MiSans() != null) return new FontFamily(MiSans() + ", " + sys);
                font = "nanum";                   // 없으면 기본 글꼴로
            }
            foreach (string[] b in Bundled) {
                if (b[0] != font) continue;
                if (FileFonts.Has(b[1])) return new FontFamily(FileFonts.Prefix + b[1]);
                return new FontFamily(b[1] + ", " + sys);
            }
        } catch (Exception) { }
        return new FontFamily(sys);
    }
    // MiSans: 라이선스상 글꼴 파일을 함께 배포할 수 없어서 넣지 않는다. 사용자가 시스템에 설치했으면 그것을 쓴다. 없으면 null.
    public static string MiSans() {
        try {
            HashSet<string> sysFonts = new(FontManager.Current.SystemFonts.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            foreach (string n in new[] { "MiSans", "MiSans Normal", "MiSans Regular" }) if (sysFonts.Contains(n)) return n;
        } catch (Exception) { }
        return null;
    }
    public static bool BundledFont(string font) { foreach (string[] b in Bundled) if (b[0] == font) return FileFonts.Has(b[1]); return false; }

    // 앱 리소스에 브러시로 넣는다
    static void Push() {
        if (Application.Current == null) return;
        IResourceDictionary r = Application.Current.Resources;
        Set(r, "Accent", Accent); Set(r, "AccentHov", AccentHov); Set(r, "NavPill", NavPill); Set(r, "NavPillBdr", NavPillBdr); Set(r, "NavPillText", NavPillText);
        Set(r, "Bg", Bg); Set(r, "Sidebar", Sidebar); Set(r, "Card", Card); Set(r, "CardBdr", CardBdr);
        Set(r, "Input", Input); Set(r, "InputBdr", InputBdr); Set(r, "Btn", Btn); Set(r, "BtnHov", BtnHov);
        Set(r, "Text", Text); Set(r, "Muted", Muted); Set(r, "Divider", Divider);
        Set(r, "DisBg", DisBg); Set(r, "DisText", DisText); Set(r, "ToggleOff", ToggleOff);
        Set(r, "Ok", Ok); Set(r, "Warn", Warn); Set(r, "Bad", Bad); Set(r, "Dim", Dim);
        Set(r, "BnBg", BnBg); Set(r, "BnBorder", BnBorder); Set(r, "BnText", BnText);
        Set(r, "BnBtnBg", BnBtnBg); Set(r, "BnBtnFg", BnBtnFg); Set(r, "BnBtnHov", BnBtnHov);
        Set(r, "Busy", C(255, 152, 0));
        Set(r, "White", Colors.White);
        // [업데이트] 같은 연한 강조색 알약
        Set(r, "AccentSoft", Color.FromArgb((byte)(Dark ? 70 : 34), Accent.R, Accent.G, Accent.B));
        Set(r, "AccentSoftText", Dark ? Mix(Accent, Colors.White, 0.45) : Accent);
        r["UiFont"] = Font;
        // Fluent 기본 컨트롤(스크롤 막대 등)도 밝기와 강조색을 따르게
        Application.Current.RequestedThemeVariant = Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        r["SystemAccentColor"] = Accent;
        r["SystemAccentColorLight1"] = AccentHov; r["SystemAccentColorLight2"] = AccentHov; r["SystemAccentColorLight3"] = AccentHov;
        r["SystemAccentColorDark1"] = AccentHov; r["SystemAccentColorDark2"] = AccentHov; r["SystemAccentColorDark3"] = AccentHov;
    }
    static void Set(IResourceDictionary r, string k, Color c) { r[k] = Brush(c); }

    // 같은 색이면 같은 브러시를 다시 쓴다 (직접 그리는 컨트롤이 그릴 때마다 새로 만들지 않게)
    static readonly Dictionary<Color, ImmutableSolidColorBrush> brushes = new();
    public static ImmutableSolidColorBrush Brush(Color c) {
        lock (brushes) {
            if (brushes.TryGetValue(c, out var b)) return b;
            if (brushes.Count > 4096) brushes.Clear();   // 애니메이션 중간색이 쌓이지 않게
            b = new ImmutableSolidColorBrush(c);
            brushes[c] = b;
            return b;
        }
    }

    // 창이 작업 영역보다 크면 줄인다. 최소 크기도 같이 줄여서 잘리지 않게 한다
    public static void FitToScreen(Window w) {
        var sc = w.Screens.Primary;
        if (sc == null) return;
        double k = sc.Scaling <= 0 ? 1 : sc.Scaling;
        double mw = sc.WorkingArea.Width / k - 40, mh = sc.WorkingArea.Height / k - 40;
        if (w.Width > mw) { w.Width = mw; w.MinWidth = Math.Min(w.MinWidth, mw); }
        if (w.Height > mh) { w.Height = mh; w.MinHeight = Math.Min(w.MinHeight, mh); }
    }

    public static Color A(Color c, double a) { return Color.FromArgb((byte)Math.Max(0, Math.Min(255, c.A * a)), c.R, c.G, c.B); }
    public static Color Mix(Color a, Color b, double t) {
        return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }
}

// 움직임 공통 설정
public static class Motion {
    public static bool Off;          // 셀프 테스트/화면 캡처: 모든 움직임을 즉시 끝낸다
    public static double Scale = 1;  // 개발자 옵션 '애니메이션 배율' (0 = 끔)
    public static bool Disabled { get { return Off || Scale <= 0; } }
    public static TimeSpan D(double sec) { return TimeSpan.FromSeconds(Disabled ? 0 : sec * Scale); }
    public static double Sec(double sec) { return Disabled ? 0 : sec * Scale; }
    public static double EaseInOut(double t) { if (t <= 0) return 0; if (t >= 1) return 1; return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; }
    public static double EaseOut(double t) { if (t <= 0) return 0; if (t >= 1) return 1; double u = 1 - t; return 1 - u * u * u; }
}
