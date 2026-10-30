using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Obj2x.UI {
// 개발용: 2초 기다린 뒤 3초 동안 프레임 간격을 재서 파일로 남긴다 (BusDepot.exe --fps [--page about])
// step이 있으면 재는 동안 0.4초마다 불러서 페이지 전환 같은 애니메이션을 일으킨다 (--fps nav)
public static class FrameMeter {
    public static void Start(Window w, string path) { Start(w, path, null); }
    public static void Start(Window w, string path, Action step) {
        System.Collections.Generic.List<double> t = new System.Collections.Generic.List<double>();
        System.Collections.Generic.List<double> steps = new System.Collections.Generic.List<double>();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        bool rec = false;
        EventHandler h = delegate(object o, EventArgs e) { if (rec) t.Add(sw.Elapsed.TotalMilliseconds); };
        System.Windows.Threading.DispatcherTimer a = new System.Windows.Threading.DispatcherTimer(), b = new System.Windows.Threading.DispatcherTimer(), c = new System.Windows.Threading.DispatcherTimer();
        a.Interval = TimeSpan.FromSeconds(2); b.Interval = TimeSpan.FromSeconds(3); c.Interval = TimeSpan.FromSeconds(0.4);
        c.Tick += delegate { steps.Add(sw.Elapsed.TotalMilliseconds); step(); };
        a.Tick += delegate { a.Stop(); CompositionTarget.Rendering += h; rec = true; b.Start(); if (step != null) c.Start(); };
        b.Tick += delegate {
            b.Stop(); c.Stop(); rec = false; CompositionTarget.Rendering -= h;
            string r;
            if (step == null) {
                double max = 0; int slow = 0;
                for (int i = 1; i < t.Count; i++) { double g = t[i] - t[i - 1]; if (g > max) max = g; if (g > 20) slow++; }
                double span = t.Count > 1 ? t[t.Count - 1] - t[0] : 1;
                r = string.Format("frames={0} fps={1:0.0} maxGap={2:0.0}ms gaps>20ms={3}", t.Count, (t.Count - 1) / (span / 1000), max, slow);
            } else {
                // 전환마다: 누른 뒤 첫 프레임까지 걸린 시간, 그 뒤 0.26초 애니메이션 동안의 프레임 간격
                double firstMax = 0, firstSum = 0, gapMax = 0; int frames = 0, slow = 0, n = 0;
                foreach (double s0 in steps) {
                    int i = t.FindIndex(delegate(double x) { return x > s0; });
                    if (i < 0) continue;
                    n++; double first = t[i] - s0; firstSum += first; if (first > firstMax) firstMax = first;
                    for (int j = i + 1; j < t.Count && t[j] <= t[i] + 260; j++) { double g = t[j] - t[j - 1]; frames++; if (g > gapMax) gapMax = g; if (g > 20) slow++; }
                }
                r = string.Format("switches={0} firstFrame avg={1:0.0}ms max={2:0.0}ms | anim frames={3} maxGap={4:0.0}ms gaps>20ms={5}", n, n > 0 ? firstSum / n : 0, firstMax, frames, gapMax, slow);
            }
            File.WriteAllText(path, r + " active=" + w.IsActive);
        };
        a.Start();
    }
}

// 개발용: 화면을 PNG로 저장한다 (BusDepot.exe --shots 폴더, --readme-shots 폴더)
// 화면을 복사하지 않고 창 내용을 직접 그려서 저장하므로, 다른 창에 가려져도 같은 결과가 나온다.
public static class Shots {
    static void Save(MainWindow f, string path) { Save(f, f.Host, path); }
    static double RenderScale;   // 0이면 화면 배율, 아니면 이 배율로 그린다 (README 움직이는 그림은 2배로 찍어 선명하게)
    static void Save(Window f, FrameworkElement c, string path, int pump = 300) {
        Pump.Run(pump);
        double sc = 1;
        PresentationSource ps = PresentationSource.FromVisual(f);
        if (ps != null && ps.CompositionTarget != null) sc = ps.CompositionTarget.TransformToDevice.M11;
        if (RenderScale > 0) sc = RenderScale;
        RenderTargetBitmap b = new RenderTargetBitmap((int)Math.Round(c.ActualWidth * sc), (int)Math.Round(c.ActualHeight * sc), 96 * sc, 96 * sc, PixelFormats.Pbgra32);
        b.Render(c);
        PngBitmapEncoder enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
        using (FileStream fs = File.Create(path)) enc.Save(fs);
    }

    // 언어 드롭다운을 펼친 모습 (팝업은 따로 그린다)
    static void SaveDrop(MainWindow f, string path) {
        System.Windows.Controls.ComboBox cb = UiTree.Find<System.Windows.Controls.ComboBox>(f.Host);
        if (cb == null) return;
        cb.IsDropDownOpen = true; Pump.Run(400);
        System.Windows.Controls.Primitives.Popup pp = cb.Template.FindName("PART_Popup", cb) as System.Windows.Controls.Primitives.Popup;
        if (pp != null && pp.Child is FrameworkElement) Save(f, (FrameworkElement)pp.Child, path);
        cb.IsDropDownOpen = false; Pump.Run(100);
    }
    // 개발용: --map-shot <맵 폴더> <png> [hof] — 맵 탭을 열어 맵을 읽고(hof를 주면 그것도) 위에서부터 몇 장 찍는다
    public static int MapShot(string map, string png, string hofPath) {
        Motion.Off = true;
        string dir = Path.GetDirectoryName(Path.GetFullPath(png));
        Settings s = new Settings(); s.Path = Path.Combine(dir, "shot.ini"); s.Lang = "ko"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
        s.OmsiDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(map).TrimEnd('\\')));
        MainWindow f = Open(s, dir);
        f.VM.Page = "map"; Pump.Run(100);
        // UI 스레드가 얼마나 오래 막혔는지(가장 긴 틈)를 함께 남긴다
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        double lastTick = 0, maxGap = 0;
        System.Windows.Threading.DispatcherTimer tm = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal);
        tm.Interval = TimeSpan.FromMilliseconds(15);
        tm.Tick += delegate { double now = sw.Elapsed.TotalMilliseconds; maxGap = Math.Max(maxGap, now - lastTick); lastTick = now; };
        tm.Start();
        f.VM.LoadMap(map);
        for (int i = 0; i < 2400 && f.VM.MapBusy; i++) Pump.Run(50);
        double loaded = sw.Elapsed.TotalSeconds;
        if (hofPath != null) f.VM.LoadHof(hofPath);
        Pump.Run(400);
        tm.Stop();
        string timing = string.Format("load {0:0.0}s, UI max gap {1:0}ms", loaded, maxGap);
        double t0 = sw.Elapsed.TotalMilliseconds;
        Save(f, png);
        timing += string.Format(", render {0:0}ms", sw.Elapsed.TotalMilliseconds - t0);
        ScrollViewer sv = null;
        foreach (ScrollViewer x in UiTreeAll<ScrollViewer>(f.Host)) if (x.IsVisible && x.ActualWidth > 500) { sv = x; break; }
        if (sv != null) for (int k = 1; k <= 5; k++) { sv.ScrollToVerticalOffset(k * 640); Pump.Run(100); Save(f, Path.Combine(dir, Path.GetFileNameWithoutExtension(png) + "_" + k + ".png")); }
        File.WriteAllText(Path.ChangeExtension(png, ".txt"), f.VM.MapStats + "\r\n" + f.VM.MissingText + "\r\n" + timing, new UTF8Encoding(true));
        f.Close();
        return 0;
    }

    // 개발용: --view-shot <model.cfg|.sco> <png> [light|dark] [ext|int|all] — 모델 보기 화면에 모델을 띄워 찍는다
    public static int ViewShot(string cfg, string png, string theme, string preset) {
        Motion.Off = true;
        string dir = Path.GetDirectoryName(Path.GetFullPath(png));
        Settings s = new Settings(); s.Path = Path.Combine(dir, "shot.ini"); s.Theme = theme; s.Lang = "ko"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
        MainWindow f = Open(s, dir);
        f.VM.Page = "bus"; Pump.Run(100);
        f.VM.LoadModel(cfg);
        for (int i = 0; i < 600 && f.VM.ViewBusy; i++) Pump.Run(50);
        // pose: 변수를 최대의 70%로 놓고 첫 번째 움직이는 부품을 골라 피벗까지 보이게 + 아래 카드들도 찍는다
        if (preset == "pose") {
            foreach (VarItem v in f.VM.Vars) v.Value = v.Min + (v.Max - v.Min) * 0.85;
            foreach (PartItem p in f.VM.Parts) if (p.Checked && p.Mesh.Animated) { f.VM.PickPartCmd.Execute(p); break; }
        } else if (preset != "ext") f.VM.ViewPreset = preset;
        Pump.Run(300);
        Save(f, png);
        if (preset == "pose") {
            ScrollViewer sv = null;
            foreach (ScrollViewer x in UiTreeAll<ScrollViewer>(f.Host)) if (x.IsVisible && x.ActualWidth > 500) { sv = x; break; }
            if (sv != null) for (int k = 1; k <= 4; k++) { sv.ScrollToVerticalOffset(k * 640); Pump.Run(100); Save(f, Path.Combine(Path.GetDirectoryName(png), Path.GetFileNameWithoutExtension(png) + "_" + k + ".png")); }
        }
        List<string> pn = new List<string>(); if (f.VM.Paints != null) foreach (TrItem p in f.VM.Paints) pn.Add(p.Name);
        File.WriteAllText(Path.ChangeExtension(png, ".txt"), f.VM.ViewStatus + "\r\n" + string.Join(", ", pn.ToArray()), new UTF8Encoding(true));
        f.Close();
        return 0;
    }

    // 개발용: --save-model <model.cfg|.sco> <출력 .blend|.obj> [blender.exe] — 모델 보기에서 저장하기를 대화 상자 없이
    public static int SaveModel(string cfg, string outFile, string blender) {
        Motion.Off = true;
        string dir = Path.GetDirectoryName(Path.GetFullPath(outFile));
        Settings s = new Settings(); s.Path = Path.Combine(dir, "shot.ini"); s.Lang = "ko"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
        if (blender != null) s.BlendExe = blender;
        MainWindow f = Open(s, AppDomain.CurrentDomain.BaseDirectory);
        f.VM.Page = "bus"; f.VM.LoadModel(cfg);
        for (int i = 0; i < 1200 && f.VM.ViewBusy; i++) Pump.Run(50);
        f.VM.ExportTo(outFile.EndsWith(".blend", StringComparison.OrdinalIgnoreCase), Path.GetFullPath(outFile), null);
        for (int i = 0; i < 12000 && f.VM.ViewBusy; i++) Pump.Run(50);
        File.WriteAllText(outFile + ".txt", (f.VM.LastExportError ?? "OK") + "\r\n" + f.VM.ViewStatus, new UTF8Encoding(true));
        f.Close();
        return f.VM.LastExportError == null ? 0 : 1;
    }

    static MainWindow Open(Settings s, string dir) {
        Loc.SetLang(s.Lang); Theme.Apply(s.Theme, s.Accent, s.Font);
        MainWindow f = new MainWindow(s, dir, false);
        f.FixClientSize(1040, 686);
        // 화면 밖에 띄운다 (마우스가 올라가 있던 줄이 칠해진 채로 찍히지 않게)
        f.ShowInTaskbar = false; f.ShowActivated = false; f.Left = -6000; f.Top = 0;
        f.Show(); Pump.Run(300);
        return f;
    }

    static List<T> UiTreeAll<T>(DependencyObject root) where T : DependencyObject {
        List<T> r = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            DependencyObject c = VisualTreeHelper.GetChild(root, i);
            if (c is T) r.Add((T)c);
            r.AddRange(UiTreeAll<T>(c));
        }
        return r;
    }

    // 지금 보이는 페이지를 맨 아래로 스크롤
    static void PageBottom(MainWindow f) {
        foreach (ScrollViewer sv in UiTreeAll<ScrollViewer>(f.Host)) if (sv.IsVisible && sv.ActualWidth > 500) { sv.ScrollToEnd(); Pump.Run(50); return; }
    }

    public static int Run(string dir) {
        Directory.CreateDirectory(dir);
        Motion.Off = true;
        string sample = Path.Combine(dir, "sample_obj"); Directory.CreateDirectory(sample);
        File.WriteAllText(Path.Combine(sample, "x.obj"), "v 0 0 0\nv 1 0 0\nv 1 1 0\nf 1 2 3\n");
        // 오브젝트 만들기 화면용 o3d 모델
        string models = Path.Combine(dir, "Bushaltestelle"); Directory.CreateDirectory(models);
        int nv, nt, nm; string wn;
        foreach (string mn in new string[] { "dach", "glas", "sitzbank" }) {
            File.WriteAllText(Path.Combine(models, mn + ".obj"), "mtllib " + mn + ".mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nvn 0 0 1\nusemtl m\nf 1/1/1 2/2/1 3/3/1\n");
            File.WriteAllText(Path.Combine(models, mn + ".mtl"), "newmtl m\nmap_Kd " + mn + (mn == "glas" ? ".tga" : ".bmp") + "\n");
            FastO3D.ConvertEx(Path.Combine(models, mn + ".obj"), Path.Combine(models, mn + ".o3d"), out nv, out nt, out nm, out wn);
        }
        byte[] tga = new byte[18]; tga[2] = 2; tga[16] = 32; tga[17] = 8;
        File.WriteAllBytes(Path.Combine(models, "glas.tga"), tga); File.WriteAllBytes(Path.Combine(models, "dach.bmp"), new byte[] { 0x42, 0x4D });
        foreach (string[] v in new string[][] { new string[] { "light", "ko", "indigo" }, new string[] { "dark", "en", "teal" }, new string[] { "light", "de", "rose" }, new string[] { "dark", "zh", "indigo" }, new string[] { "light", "ja", "teal" } }) {
            Settings s = new Settings(); s.Path = Path.Combine(dir, "shot.ini");
            s.Theme = v[0]; s.Lang = v[1]; s.Accent = v[2]; s.Inp = sample; s.ObjModels = models; s.ObjFolder = "Bushaltestelle"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
            s.AddRecent(sample); s.AddRecent(@"C:\Models\Bus\ELE19"); s.AddRecent(@"D:\OMSI\Vehicles\Regs_SD77"); s.AddRecent(@"E:\gone\folder");
            MainWindow f = Open(s, dir);
            string tag = v[0] + "_" + v[1];
            f.VM.Page = "dashboard"; Save(f, Path.Combine(dir, tag + "_dash.png"));
            f.VM.Page = "wizard"; Save(f, Path.Combine(dir, tag + "_wiz1.png"));
            for (int i = 2; i <= 4; i++) { f.VM.WizStep = i; Save(f, Path.Combine(dir, tag + "_wiz" + i + ".png")); }
            f.VM.Page = "object"; Save(f, Path.Combine(dir, tag + "_obj.png"));
            PageBottom(f); Save(f, Path.Combine(dir, tag + "_obj2.png"));
            f.VM.Page = "spline"; Save(f, Path.Combine(dir, tag + "_spl.png"));
            PageBottom(f); Save(f, Path.Combine(dir, tag + "_spl2.png"));
            f.VM.Page = "advanced"; Save(f, Path.Combine(dir, tag + "_adv.png"));
            f.VM.Page = "settings"; Save(f, Path.Combine(dir, tag + "_set.png"));
            SaveDrop(f, Path.Combine(dir, tag + "_set_lang.png"));
            f.VM.Page = "about"; Save(f, Path.Combine(dir, tag + "_about.png"));
            if (tag == "light_ko") {                 // 업데이트 버튼이 올라오는 중간 모습 (애니메이션 0.6초 중 약 0.25초)
                Motion.Off = false; f.VM.FakeUpdate("9.9");
                Save(f, f.Host, Path.Combine(dir, tag + "_about_upd_mid.png"), 250);
                Motion.Off = true;
            }
            f.VM.FakeUpdate("9.9"); Save(f, Path.Combine(dir, tag + "_about_upd.png"));
            f.VM.DevMode = true; f.VM.Page = "dev"; Save(f, Path.Combine(dir, tag + "_dev.png"));
            foreach (ScrollViewer sv in UiTreeAll<ScrollViewer>(f.Host)) if (sv.IsVisible) sv.ScrollToEnd();
            Pump.Run(50); Save(f, Path.Combine(dir, tag + "_dev2.png"));
            if (tag == "light_ko" || tag == "dark_en") {   // 누름 효과: 위로 올려서 '버그 신고' 줄 오른쪽을 누른 모습
                foreach (ScrollViewer sv in UiTreeAll<ScrollViewer>(f.Host)) if (sv.IsVisible) sv.ScrollToHome();
                Pump.Run(50);
                Button row = UiTreeAll<Button>(f.Host).Find(delegate(Button b) { return b.IsVisible && b.Command == f.VM.BugReportCmd; });
                Ripple rp = row == null ? null : UiTreeAll<Ripple>(row).Find(delegate(Ripple x) { return true; });
                if (rp != null) {
                    Motion.Off = false;
                    foreach (double sc in new double[] { 4, 1 }) {
                        Motion.Scale = sc; rp.Press(new Point(rp.ActualWidth * 0.7, rp.ActualHeight * 0.5));
                        Save(f, Path.Combine(dir, tag + "_ripple" + sc + ".png"));
                        rp.Release(); Pump.Run(1500);
                    }
                    Motion.Scale = 1; Motion.Off = true;
                }
            }
            f.VM.DevMode = false;
            f.VM.Page = "dashboard"; f.Disclaimer(false, null); Save(f, Path.Combine(dir, tag + "_disc.png"));
            f.LastDialog.Close(1); Pump.Run(50);   // 0은 "종료"라 창이 닫힌다
            // 큰 창: 배율이 커져야 한다
            f.FixClientSize(1248, 824);   // 1.2배 (화면보다 크면 창이 잘려서 화면 안에 들어가는 크기로)
            f.VM.Page = "dashboard"; Save(f, Path.Combine(dir, tag + "_z_dash.png"));
            f.VM.Page = "settings"; Save(f, Path.Combine(dir, tag + "_z_set.png"));
            f.VM.Page = "dashboard"; f.Msg(L.T("m_log_saved")); Save(f, Path.Combine(dir, tag + "_z_msg.png"));
            f.Close();
            WelcomeWindow w = new WelcomeWindow(s);
            w.WindowState = WindowState.Normal; w.Width = 1000; w.Height = 700; w.Left = 0; w.Top = 0;
            w.Show(); Pump.Run(1500);   // 인사말이 나타날 때까지
            Save(w, (FrameworkElement)w.Content, Path.Combine(dir, tag + "_welcome.png"));
            w.Close();
        }
        return 0;
    }

    // 개발용: README 맨 위 움직이는 그림용 환영 화면 프레임 (BusDepot.exe --welcome-frames 폴더 [light|dark])
    // 인사말 한 바퀴(Period초)를 초당 Fps장으로, 앞에 Blend초를 더 찍는다. 이음매 없이 반복되게 섞는 것은 바깥 스크립트가 한다.
    // 파일 이름: f_0000.png ... (0번 = -Blend초), 첫 줄에 times.txt로 장 수/초당 장 수/한 바퀴 시간을 남긴다.
    public static int WelcomeFrames(string dir, string theme) {
        const int Fps = 25; const double Blend = 1.6;   // Blend × Fps가 정수여야 한다
        Directory.CreateDirectory(dir);
        Settings s = new Settings(); s.Path = Path.Combine(dir, "wf.ini"); s.Lang = "ko"; s.Theme = theme == "dark" ? "dark" : "light"; s.Accent = "indigo";
        Loc.SetLang(s.Lang); Theme.Apply(s.Theme, s.Accent, s.Font);
        WelcomeWindow w = new WelcomeWindow(s);
        w.WindowState = WindowState.Normal; w.Width = 1000; w.Height = 640; w.Left = -6000; w.Top = 0; w.ShowInTaskbar = false; w.ShowActivated = false;
        ((FrameworkElement)w.Content).IsHitTestVisible = false;   // 마우스가 어디 있든 호버 상태가 생기지 않게
        w.Show(); Pump.Run(800);
        int n = (int)Math.Round((w.Period + Blend) * Fps);
        RenderScale = 2;
        for (int i = 0; i < n; i++) {
            w.Seek(-Blend + i / (double)Fps);
            Save(w, (FrameworkElement)w.Content, Path.Combine(dir, "f_" + i.ToString("0000") + ".png"), 15);
        }
        File.WriteAllText(Path.Combine(dir, "times.txt"), n + " " + Fps + " " + w.Period.ToString(CultureInfo.InvariantCulture) + " " + Blend.ToString(CultureInfo.InvariantCulture));
        w.Close();
        return 0;
    }

    // 개발용: README에 넣을 화면 (BusDepot.exe --readme-shots 폴더)
    // 가짜 blender.exe/OmsiXConv.exe로 경로 경고를 없애고, 샘플 obj를 빠른 변환으로 실제 변환해 결과 표를 채운다.
    // 사용자 경로가 보이는 화면(마법사 경로 단계, 고급의 프로그램 경로, 설정의 백업 폴더)은 찍지 않는다.
    public static int Readme(string dir) {
        Directory.CreateDirectory(dir);
        Motion.Off = true;
        string work = Path.Combine(Path.GetTempPath(), "BusDepot_readme");
        string models = Path.Combine(work, "ELE19");
        try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch (Exception) { }
        Directory.CreateDirectory(models);
        string bl = Path.Combine(work, "blender.exe"), cv = Path.Combine(work, "OmsiXConv.exe");
        File.WriteAllText(bl, ""); File.WriteAllText(cv, "");
        SampleModels(models);

        foreach (string th in new string[] { "light", "dark" }) {
            Settings s = new Settings(); s.Path = Path.Combine(work, "shot.ini");
            s.Theme = th; s.Lang = "ko"; s.Accent = "indigo"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
            s.Inp = models; s.Blender = bl; s.Conv = cv; s.SameDir = true;
            MainWindow f = Open(s, work);
            if (th == "light") {
                BitmapSource logo = Art.Render(256, 256, delegate(DrawingContext dc) { Art.Logo(dc, 28, 28, 200); });
                PngBitmapEncoder enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(logo));
                using (FileStream fs = File.Create(Path.Combine(dir, "logo.png"))) enc.Save(fs);
            }
            f.VM.Page = "wizard"; Save(f, Path.Combine(dir, th + "_wizard.png"));
            f.VM.ShotRun("fast");
            DateTime t0 = DateTime.Now;
            while (f.VM.Running && (DateTime.Now - t0).TotalSeconds < 30) Pump.Run(50);
            f.VM.Page = "dashboard"; Save(f, Path.Combine(dir, th + "_dashboard.png"));
            f.VM.FakeUpdate(NextVersion());
            f.VM.Page = "about"; Save(f, Path.Combine(dir, th + "_about.png"));
            f.Close();
        }
        try { Directory.Delete(work, true); } catch (Exception) { }
        return 0;
    }

    static string NextVersion() {
        try { Version v = new Version(AppInfo.Version); return v.Major + "." + (v.Minor + 1); } catch (Exception) { return AppInfo.Version; }
    }

    // ---- 샘플 모델 (버스 부품 흉내: 상자/원통)
    public static void SampleModelsPublic(string dir) { SampleModels(dir); }
    static void SampleModels(string dir) {
        File.WriteAllText(Path.Combine(dir, "parts.mtl"),
            "newmtl body\nKd 0.85 0.85 0.88\nnewmtl glass\nKd 0.2 0.3 0.35\nd 0.4\nnewmtl tyre\nKd 0.08 0.08 0.08\nnewmtl rim\nKd 0.7 0.7 0.72\nnewmtl seat\nKd 0.2 0.25 0.6\n");
        Box(dir, "body", "body", 2.55f, 3.1f, 12f, 1, 8);
        Box(dir, "windows", "glass", 2.6f, 1.1f, 11.2f, 1, 6);
        Box(dir, "door_front", "glass", 0.06f, 2.3f, 1.25f, 2, 4);
        Box(dir, "door_middle", "glass", 0.06f, 2.3f, 1.25f, 2, 4);
        Cyl(dir, "wheel_front", "tyre", 0.5f, 0.3f, 48);
        Cyl(dir, "wheel_rear", "tyre", 0.5f, 0.6f, 48);
        Cyl(dir, "steering_wheel", "rim", 0.25f, 0.04f, 36);
        Box(dir, "seats", "seat", 0.45f, 1f, 0.5f, 3, 3);
        // mtl에 없는 재질 → 결과 표에 '주의'
        Box(dir, "mirror_left", "chrome", 0.08f, 0.35f, 0.25f, 1, 1);
    }

    static void Box(string dir, string name, string mat, float w, float h, float d, int sx, int sz) {
        StringBuilder sb = new StringBuilder();
        sb.Append("mtllib parts.mtl\no ").Append(name).Append("\n");
        CultureInfo ic = CultureInfo.InvariantCulture;
        int vi = 1;
        // 면마다 sx x sz 격자로 나눠서 정점/삼각형 수가 그럴듯하게
        float[][] n = new float[][] { new float[] { 1, 0, 0 }, new float[] { -1, 0, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, -1, 0 }, new float[] { 0, 0, 1 }, new float[] { 0, 0, -1 } };
        sb.Append("usemtl ").Append(mat).Append("\n");
        foreach (float[] nn in n) sb.AppendFormat(ic, "vn {0} {1} {2}\n", nn[0], nn[1], nn[2]);
        for (int fi = 0; fi < 6; fi++) {
            float[] nn = n[fi];
            for (int i = 0; i <= sx; i++) for (int j = 0; j <= sz; j++) {
                float u = i / (float)sx - 0.5f, v = j / (float)sz - 0.5f;
                float x, y, z;
                if (nn[0] != 0) { x = nn[0] * w / 2; y = u * h; z = v * d; }
                else if (nn[1] != 0) { y = nn[1] * h / 2; x = u * w; z = v * d; }
                else { z = nn[2] * d / 2; x = u * w; y = v * h; }
                sb.AppendFormat(ic, "v {0} {1} {2}\nvt {3} {4}\n", x, y, z, u + 0.5f, v + 0.5f);
            }
            for (int i = 0; i < sx; i++) for (int j = 0; j < sz; j++) {
                int a = vi + i * (sz + 1) + j, b = a + sz + 1;
                sb.AppendFormat("f {0}/{0}/{4} {1}/{1}/{4} {2}/{2}/{4}\nf {0}/{0}/{4} {2}/{2}/{4} {3}/{3}/{4}\n", a, b, b + 1, a + 1, fi + 1);
            }
            vi += (sx + 1) * (sz + 1);
        }
        File.WriteAllText(Path.Combine(dir, name + ".obj"), sb.ToString());
    }

    static void Cyl(string dir, string name, string mat, float r, float len, int seg) {
        StringBuilder sb = new StringBuilder();
        CultureInfo ic = CultureInfo.InvariantCulture;
        sb.Append("mtllib parts.mtl\no ").Append(name).Append("\nusemtl ").Append(mat).Append("\n");
        for (int i = 0; i < seg; i++) {
            double a = i * 2 * Math.PI / seg;
            float y = (float)(Math.Sin(a) * r), z = (float)(Math.Cos(a) * r);
            sb.AppendFormat(ic, "v {0} {1} {2}\nv {3} {1} {2}\nvn 0 {4} {5}\nvt {6} 0\nvt {6} 1\n", -len / 2, y, z, len / 2, (float)Math.Sin(a), (float)Math.Cos(a), i / (float)seg);
        }
        for (int i = 0; i < seg; i++) {
            int j = (i + 1) % seg;
            int a = 2 * i + 1, b = 2 * i + 2, c = 2 * j + 1, d = 2 * j + 2;
            sb.AppendFormat("f {0}/{0}/{4} {2}/{2}/{5} {3}/{3}/{5}\nf {0}/{0}/{4} {3}/{3}/{5} {1}/{1}/{4}\n", a, b, c, d, i + 1, j + 1);
        }
        File.WriteAllText(Path.Combine(dir, name + ".obj"), sb.ToString());
    }
}
}
