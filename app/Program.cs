using System;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Media;
using Avalonia.Threading;

namespace Obj2x.UI;

public static class Program {
    public static string Here = AppContext.BaseDirectory;
    public static string[] Args = Array.Empty<string>();

    [STAThread]
    public static int Main(string[] args) {
        Args = args;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // OMSI 파일 글자: Windows는 시스템 ANSI 코드 페이지(.NET 8의 Encoding.Default는 UTF-8이라 직접 고른다), 맥은 설정에서 (Platform.ApplyEncoding)
        try { Omsi.Ansi = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage); } catch (Exception) { Omsi.Ansi = Encoding.GetEncoding(1252); }
        try {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        } catch (Exception ex) {
            try { File.WriteAllText(Path.Combine(Platform.DataDir, "BusDepot_crash.txt"), ex.ToString()); } catch (Exception) { }
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // 3D 보기(OpenGL): Windows는 ANGLE(OpenGL ES), 맥은 OpenGL. 안 되면 소프트웨어로 그린다 (3D만 비어 보임)
            .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software } })
            .With(new AvaloniaNativePlatformOptions { RenderingMode = new[] { AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software } })
            .With(new FontManagerOptions { DefaultFamilyName = "Malgun Gothic" })
            .LogToTrace();
    }
}

public class App : Application {
    public override void Initialize() {
        Name = "Bus Depot";     // 맥 메뉴 막대의 앱 이름 (기본값은 Avalonia Application)
        Styles.Add(new FluentTheme());
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://BusDepot/")) { Source = new Uri("avares://BusDepot/Views/Styles.axaml") });
    }

    public override void OnFrameworkInitializationCompleted() {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime life) return;
        FileFonts.Register();
        Dispatcher.UIThread.UnhandledException += (s, e) => {
            try { File.WriteAllText(Path.Combine(Platform.DataDir, "BusDepot_crash.txt"), e.Exception.ToString()); } catch (Exception) { }
            if (MainWindow.Instance != null) { MainWindow.Instance.Msg(e.Exception.ToString()); e.Handled = true; }
        };
        int code = Start(life);
        if (code >= 0) Dispatcher.UIThread.Post(() => life.Shutdown(code));
    }

    // 개발용 명령이면 그 결과 코드, 보통 실행이면 -1
    static int Start(IClassicDesktopStyleApplicationLifetime life) {
        string[] args = Program.Args;
        string here = Program.Here;
        string startPage = null, dropped = null; bool fps = false, fpsNav = false;
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--fps") { fps = true; fpsNav = i + 1 < args.Length && args[i + 1] == "nav"; }
            // exe(앱) 위에 폴더/파일을 끌어다 놓으면 그 경로가 첫 인수로 들어온다
            if (i == 0 && !args[0].StartsWith("-") && (Directory.Exists(args[0]) || File.Exists(args[0]))) dropped = args[0];
            if (i + 1 < args.Length && args[i] == "--page") startPage = args[i + 1];
            int r = DevTools.Run(args, i, here);
            if (r >= 0) return r;
        }

        Settings s = Platform.LoadSettings(here);
        Loc.SetLang(s.Lang);
        Platform.ApplyEncoding(s);
        Look.Apply(s.Theme, s.Accent, s.Font);
        Motion.Scale = s.DevMode ? s.DevAnim : 1;

        bool firstRun = !s.Agreed;
        void openMain() {
            MainWindow mw = new MainWindow(s, here, !firstRun);
            life.MainWindow = mw;
            mw.Closed += (o, e) => { s.Save(); life.Shutdown(); };
            if (startPage != null) mw.Opened += (o, e) => mw.VM.Page = startPage;
            if (dropped != null) mw.Opened += (o, e) => mw.VM.DropPath(dropped);
            // --fps [nav]: 프레임 간격을 BusDepot_fps.txt에 (nav: 0.4초마다 페이지 전환)
            if (fps) mw.Opened += (o, e) => {
                int n = 0; string[] cyc = { "wizard", "advanced", "settings", "dashboard" };
                FrameMeter.Start(mw, Path.Combine(Platform.DataDir, "BusDepot_fps.txt"), fpsNav ? () => mw.VM.Page = cyc[n++ % cyc.Length] : null);
            };
            mw.Show();
        }
        if (firstRun) {
            // 첫 실행: 환영 화면 (언어/보기 설정 -> 시작 -> 면책 조항)
            WelcomeWindow w = new WelcomeWindow(s);
            life.MainWindow = w;
            w.Done += ok => {
                s.Save();
                if (ok) { openMain(); w.Close(); }
                else life.Shutdown();
            };
            w.Show();
        } else openMain();
        return -1;
    }
}
