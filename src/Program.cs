using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Obj2x {

public static class AppIcon {
    static Icon ico;
    public static Icon Get() {
        if (ico != null) return ico;
        try {
            using (Bitmap bmp = new Bitmap(64, 64)) {
                using (Graphics g = Graphics.FromImage(bmp)) {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    LogoArt.Draw(g, 4, 4, 48);
                }
                ico = Icon.FromHandle(bmp.GetHicon());
            }
        } catch (Exception) { }
        return ico;
    }
}

public static class Program {
    public const string AppVersion = "2.1";
    public const string Repo = "HYB1022/obj2x";

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    public static int Main(string[] args) {
        bool selftest = false;
        foreach (string a in args) { if (a == "--selftest" || a == "/selftest") selftest = true; }
        try { SetProcessDPIAware(); } catch (Exception) { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string here = AppDomain.CurrentDomain.BaseDirectory;
        if (selftest) return SelfTest.Run(here);
        for (int i = 0; i < args.Length - 1; i++) { if (args[i] == "--shots") return Shots.Run(args[i + 1]); }

        try { string old = System.Reflection.Assembly.GetExecutingAssembly().Location + ".old"; if (File.Exists(old)) File.Delete(old); } catch (Exception) { }

        Settings s = Settings.Load(Path.Combine(here, "obj2x.ini"));
        L.Lang = s.Lang;
        Theme.Apply(s.Theme, s.Accent);
        Ui.BuildFonts(s.SysFont);

        bool firstRun = !s.Agreed;
        if (firstRun) {
            // 첫 실행: 환영 화면 (언어/보기 설정 -> 시작 -> 면책 조항)
            WelcomeForm wf = new WelcomeForm(s);
            Application.Run(wf);
            s.Save();
            if (!wf.Agreed) return 0;
        }
        MainForm mf = new MainForm(s);
        bool upgrade = !firstRun && s.NeedDisclaimer();
        mf.Shown += delegate {
            if (upgrade) mf.ShowDisclaimer(false);
            if (s.UpdCheck) mf.StartUpdateCheck(false);
        };
        Application.Run(mf);
        s.Save();
        return 0;
    }
}
}
