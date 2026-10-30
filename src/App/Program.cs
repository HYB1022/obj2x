using System;
using System.IO;
using System.Windows;

namespace Obj2x.UI {

public static class Program {
    [STAThread]
    public static int Main(string[] args) {
        string here = AppDomain.CurrentDomain.BaseDirectory;
        Application app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Xaml.Load("Styles.xaml"));
        app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            MessageBox.Show(e.Exception.ToString(), "obj2x");
            e.Handled = true;
        };

        // 개발용: --selftest, --shots 폴더, --readme-shots 폴더, --page 이름(시작 페이지), --fps (프레임 측정을 obj2x_fps.txt에)
        string startPage = null; bool fps = false, fpsNav = false;
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--fps") { fps = true; fpsNav = i + 1 < args.Length && args[i + 1] == "nav"; }
            if (i + 1 < args.Length && args[i] == "--page") startPage = args[i + 1];
            if (args[i] == "--selftest" || args[i] == "/selftest") return SelfTest.Run(here);
            if (i + 1 < args.Length && args[i] == "--shots") return Shots.Run(args[i + 1]);
            if (i + 1 < args.Length && args[i] == "--readme-shots") return Shots.Readme(args[i + 1]);
        }

        Updater.CleanupOld(System.Reflection.Assembly.GetExecutingAssembly().Location);
        Settings s = Settings.Load(Path.Combine(here, "obj2x.ini"));
        Loc.SetLang(s.Lang);
        Theme.Apply(s.Theme, s.Accent, s.Font);
        Motion.Scale = s.DevMode ? s.DevAnim : 1;

        bool firstRun = !s.Agreed;
        Action openMain = delegate {
            MainWindow mw = new MainWindow(s, here, !firstRun);
            mw.Closed += delegate { s.Save(); app.Shutdown(); };
            if (startPage != null) mw.Loaded += delegate { mw.VM.Page = startPage; };
            if (fps) mw.Loaded += delegate {
                int n = 0; string[] cyc = new string[] { "wizard", "advanced", "settings", "dashboard" };
                FrameMeter.Start(mw, Path.Combine(here, "obj2x_fps.txt"), fpsNav ? (Action)delegate { mw.VM.Page = cyc[n++ % cyc.Length]; } : null);
            };
            mw.Show();
        };
        if (firstRun) {
            // 첫 실행: 환영 화면 (언어/보기 설정 -> 시작 -> 면책 조항)
            WelcomeWindow w = new WelcomeWindow(s);
            w.Done += delegate(bool ok) {
                s.Save();
                if (ok) { openMain(); w.Close(); }
                else app.Shutdown();
            };
            w.Show();
        } else openMain();
        return app.Run();
    }
}
}
