using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Obj2x;

namespace BusDepotMac;

public static class Program {
    [STAThread]
    public static int Main(string[] args) {
        MacEnv.Init();
        // --selftest <결과 파일>: 화면 없이 공통 로직과 맥 쪽 경로/인코딩을 점검 (Windows에서도 돌릴 수 있다)
        if (args.Length > 0 && args[0] == "--selftest") return MacSelfTest.Run(args.Length > 1 ? args[1] : "BusDepot_mac_selftest.txt");
        if (args.Length > 1 && args[0] == "--shots") { MacShots.Dir = args[1]; MacShots.Bus = args.Length > 2 ? args[2] : null; MacShots.Map = args.Length > 3 ? args[3] : null; }
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
        return 0;
    }
}

public class App : Application {
    public override void Initialize() {
        Styles.Add(new FluentTheme());
        // 강조 색은 Windows 버전과 같은 인디고 (맥/Windows 시스템 강조 색을 따르지 않게)
        Avalonia.Media.Color acc = Avalonia.Media.Color.FromRgb(80, 70, 229);
        Resources["SystemAccentColor"] = acc;
        Resources["SystemAccentColorLight1"] = Avalonia.Media.Color.FromRgb(108, 99, 236);
        Resources["SystemAccentColorLight2"] = Avalonia.Media.Color.FromRgb(140, 132, 242);
        Resources["SystemAccentColorLight3"] = Avalonia.Media.Color.FromRgb(175, 169, 247);
        Resources["SystemAccentColorDark1"] = Avalonia.Media.Color.FromRgb(66, 57, 204);
        Resources["SystemAccentColorDark2"] = Avalonia.Media.Color.FromRgb(53, 45, 170);
        Resources["SystemAccentColorDark3"] = Avalonia.Media.Color.FromRgb(40, 34, 130);
        ApplyTheme();
    }
    public static void ApplyTheme() {
        string t = MacEnv.S.Theme;
        Current.RequestedThemeVariant = t == "dark" ? ThemeVariant.Dark : t == "light" ? ThemeVariant.Light : ThemeVariant.Default;
    }
    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) {
            if (MacShots.Dir != null) MacShots.Start(d); else d.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
