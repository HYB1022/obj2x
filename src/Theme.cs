using System;
using System.Drawing;
using Microsoft.Win32;

namespace Obj2x {
// 라이트/다크 × 인디고/틸/로즈 색 팔레트
public static class Theme {
    public static bool Dark;
    public static string AccentName = "indigo";
    public static Color Accent, AccentHov, NavPill, NavPillBdr, NavPillText;
    public static Color Bg, Sidebar, Card, CardBdr, Input, InputBdr, Btn, BtnHov, Text, Muted, Divider, DisBg, DisText, ToggleOff, Ok, Warn, Bad, Dim;
    public static Color BnBg, BnBorder, BnText, BnBtnBg, BnBtnFg, BnBtnHov;

    static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

    public static bool SystemIsDark() {
        try {
            object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            if (v is int) return ((int)v) == 0;
        } catch (Exception) { }
        return false;
    }

    public static void Apply(string theme, string accent) {
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
            BnBtnBg = C(30, 24, 10); BnBtnFg = Color.White; BnBtnHov = C(62, 52, 28);
        }
    }
}
}
