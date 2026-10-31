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
            MessageBox.Show(e.Exception.ToString(), AppInfo.Name);
            e.Handled = true;
        };

        // 개발용: --selftest, --shots 폴더, --readme-shots 폴더, --page 이름(시작 페이지), --fps (프레임 측정을 BusDepot_fps.txt에)
        string startPage = null, dropped = null; bool fps = false, fpsNav = false;
        for (int i = 0; i < args.Length; i++) {
            // exe 위에 폴더/파일을 끌어다 놓으면 그 경로가 첫 인수로 들어온다
            if (i == 0 && !args[0].StartsWith("-") && !args[0].StartsWith("/") && (Directory.Exists(args[0]) || File.Exists(args[0]))) dropped = args[0];
            if (args[i] == "--fps") { fps = true; fpsNav = i + 1 < args.Length && args[i + 1] == "nav"; }
            if (i + 1 < args.Length && args[i] == "--page") startPage = args[i + 1];
            // --selftest [결과 파일]: 결과 파일을 주면 exe 폴더 대신 거기에 쓴다 (개발자 옵션의 '자체 점검 실행')
            if (args[i] == "--selftest" || args[i] == "/selftest")
                return SelfTest.Run(here, i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : Path.Combine(here, "BusDepot_selftest.txt"));
            if (i + 1 < args.Length && args[i] == "--shots") return Shots.Run(args[i + 1]);
            if (i + 1 < args.Length && args[i] == "--readme-shots") return Shots.Readme(args[i + 1], i + 2 < args.Length ? args[i + 2] : "ko");
            if (i + 1 < args.Length && args[i] == "--make-icon") { Theme.Apply("light", "indigo", "nanum"); Art.WriteIco(args[i + 1]); return 0; }
            if (i + 2 < args.Length && args[i] == "--video-frames") return Video.Run(args[i + 1], args[i + 2], i + 3 < args.Length ? args[i + 3] : null);
            if (i + 2 < args.Length && args[i] == "--cafe-shots") return Shots.Cafe(args[i + 1], args[i + 2]);
            if (i + 2 < args.Length && args[i] == "--map-shot") return Shots.MapShot(args[i + 1], args[i + 2], i + 3 < args.Length ? args[i + 3] : null);
            if (i + 2 < args.Length && args[i] == "--save-model") return Shots.SaveModel(args[i + 1], args[i + 2], i + 3 < args.Length ? args[i + 3] : null);
            if (i + 2 < args.Length && args[i] == "--view-shot")
                return Shots.ViewShot(args[i + 1], args[i + 2], i + 3 < args.Length ? args[i + 3] : "light", i + 4 < args.Length ? args[i + 4] : "ext");
            if (i + 1 < args.Length && args[i] == "--welcome-frames") return Shots.WelcomeFrames(args[i + 1], i + 2 < args.Length ? args[i + 2] : "light", i + 3 < args.Length ? args[i + 3] : "ko");
        }

        Updater.CleanupOld(System.Reflection.Assembly.GetExecutingAssembly().Location);
        // 설정: BusDepot.ini. 이름을 바꾸기 전(obj2x.ini)에서 올라왔으면 한 번 옮겨 온다 (원래 파일은 남겨 둠)
        string ini = Path.Combine(here, AppInfo.Exe + ".ini"), oldIni = Path.Combine(here, "obj2x.ini");
        try { if (!File.Exists(ini) && File.Exists(oldIni)) File.Copy(oldIni, ini); } catch (Exception) { }
        Updater.CleanupObj2x(here);
        Settings s = Settings.Load(ini);
        Loc.SetLang(s.Lang);
        Theme.Apply(s.Theme, s.Accent, s.Font);
        Motion.Scale = s.DevMode ? s.DevAnim : 1;

        bool firstRun = !s.Agreed;
        Action openMain = delegate {
            MainWindow mw = new MainWindow(s, here, !firstRun);
            mw.Closed += delegate { s.Save(); app.Shutdown(); };
            if (startPage != null) mw.Loaded += delegate { mw.VM.Page = startPage; };
            if (dropped != null) mw.Loaded += delegate { mw.VM.DropPath(dropped); };
            if (fps) mw.Loaded += delegate {
                int n = 0; string[] cyc = new string[] { "wizard", "advanced", "settings", "dashboard" };
                FrameMeter.Start(mw, Path.Combine(here, "BusDepot_fps.txt"), fpsNav ? (Action)delegate { mw.VM.Page = cyc[n++ % cyc.Length]; } : null);
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
