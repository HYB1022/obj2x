using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Obj2x.UI {

// 개발용: 소개 영상의 화면 (BusDepot.exe --video-frames <폴더> <장면 파일> [모델 cfg/sco])
// 장면 파일 한 줄 = "장면이름 초". 장면마다 <폴더>\<장면이름>\f00000.png ... (1920x1080, 초당 30장)을 만든다.
// 화면은 영어 UI (오디오 트랙이 여러 언어라서). 소리/자막은 tools\video\make_video.py가 붙인다.
public static class Video {
    const int W = 1920, H = 1080, Fps = 30;
    const double AppScale = 1.4;            // 1040x686 창 → 1456x960
    static string outDir;
    static Color bgA, bgB;

    public static int Run(string dir, string scenesFile, string model) {
        Motion.Off = true;
        outDir = dir; Directory.CreateDirectory(dir);
        // 화면에 보이는 경로에 사용자 이름이 나오지 않게 드라이브 바로 아래에서 작업하고 끝나면 지운다
        string work = Path.Combine(Path.GetPathRoot(Path.GetFullPath(dir)), "BusDepot_demo");
        // 같은 이름의 폴더가 원래 있었다면(표시 파일이 없음) 건드리지 않고 다른 이름을 쓴다
        for (int k = 2; Directory.Exists(work) && !File.Exists(Path.Combine(work, ".busdepot_video")); k++) work = Path.Combine(Path.GetPathRoot(work), "BusDepot_demo" + k);
        try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch (Exception) { }
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, ".busdepot_video"), "");
        List<string[]> scenes = new List<string[]>();
        foreach (string ln in File.ReadAllLines(scenesFile)) { string[] a = ln.Trim().Split(' '); if (a.Length == 2) scenes.Add(a); }

        // 준비: 샘플 obj 폴더(하나는 깨진 파일 → 실패 표시), 가짜 도구 경로, 데모 버스
        string models = Path.Combine(work, "Bus_Stop"); Directory.CreateDirectory(models);
        Shots.SampleModelsPublic(models);
        File.WriteAllText(Path.Combine(models, "broken_part.obj"), "v 0 0 0\n");
        string bl = Path.Combine(work, "blender.exe"), cv = Path.Combine(work, "OmsiXConv.exe");
        File.WriteAllText(bl, ""); File.WriteAllText(cv, "");
        if (string.IsNullOrEmpty(model) || !File.Exists(model)) model = DemoBus(Path.Combine(work, "demo"));

        Settings s = new Settings(); s.Path = Path.Combine(work, "video.ini");
        s.Theme = "light"; s.Lang = "en"; s.Accent = "indigo"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
        s.Inp = models; s.Blender = bl; s.Conv = cv; s.SameDir = true; s.Fast = true;
        s.ObjModels = models; s.ObjFolder = "Bus_Stop"; s.OmsiDir = "";
        Loc.SetLang("en"); Theme.Apply("light", "indigo", s.Font);
        Palette();
        MainWindow f = new MainWindow(s, work, false);
        // 화면 밖에 띄워서 사용자가 실수로 닫거나 눌러 화면이 멈추지 않게 (작업 표시줄에도 안 보임)
        f.ShowInTaskbar = false; f.ShowActivated = false;
        f.FixClientSize(1040, 686); f.Left = -6000; f.Top = 0;
        f.Show(); Pump.Run(500);

        foreach (string[] sc in scenes) {
            double sec = double.Parse(sc[1], CultureInfo.InvariantCulture);
            int n = Math.Max(1, (int)Math.Round(sec * Fps));
            string sd = Path.Combine(dir, sc[0]);
            try { if (Directory.Exists(sd)) Directory.Delete(sd, true); } catch (Exception) { }
            Directory.CreateDirectory(sd);
            Console.WriteLine(sc[0] + " " + n);
            switch (sc[0]) {
                case "intro": Intro(s, sd, n); break;
                case "convert": Convert(f, sd, n); break;
                case "dash": Dash(f, sd, n); break;
                case "object": ObjectScene(f, sd, n); break;
                case "spline": SplineScene(f, sd, n); break;
                case "bus": ViewerScene(f, sd, n, model); break;
                case "langs": Langs(f, sd, n); break;
                case "outro": Outro(sd, n); break;
            }
        }
        f.Close();
        try { Directory.Delete(work, true); } catch (Exception) { }
        return 0;
    }

    static void Palette() {
        bgA = Theme.Dark ? Color.FromRgb(24, 26, 38) : Color.FromRgb(232, 234, 255);
        bgB = Theme.Dark ? Color.FromRgb(40, 30, 58) : Color.FromRgb(250, 238, 252);
    }

    // ------------------------------------------------------------ 한 장 그리기: 그라데이션 바탕 위에 창(둥근 모서리 + 그림자)
    static BitmapSource Snap(FrameworkElement c, double scale) {
        RenderTargetBitmap b = new RenderTargetBitmap((int)Math.Round(c.ActualWidth * scale), (int)Math.Round(c.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        b.Render(c);
        b.Freeze();
        return b;
    }

    static void Write(BitmapSource app, string path) {
        Grid stage = new Grid(); stage.Width = W; stage.Height = H;
        LinearGradientBrush g = new LinearGradientBrush(bgA, bgB, new Point(0, 0), new Point(1, 1));
        stage.Background = g;
        if (app != null) {
            Border card = new Border(); card.Width = app.PixelWidth; card.Height = app.PixelHeight;
            card.HorizontalAlignment = HorizontalAlignment.Center; card.VerticalAlignment = VerticalAlignment.Center;
            card.Effect = new DropShadowEffect { BlurRadius = 60, ShadowDepth = 18, Direction = 270, Opacity = Theme.Dark ? 0.5 : 0.22 };
            Border clip = new Border(); clip.CornerRadius = new CornerRadius(22); clip.Background = new ImageBrush(app);
            card.Child = clip;
            stage.Children.Add(card);
        }
        Render(stage, path);
    }

    static void Render(FrameworkElement stage, string path) {
        stage.Measure(new Size(W, H)); stage.Arrange(new Rect(0, 0, W, H)); stage.UpdateLayout();
        RenderTargetBitmap rb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        rb.Render(stage);
        PngBitmapEncoder enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rb));
        using (FileStream fs = File.Create(path)) enc.Save(fs);
    }

    static string F(string sd, int i) { return Path.Combine(sd, "f" + i.ToString("00000") + ".png"); }

    // 같은 화면이 이어지는 구간은 한 장만 그리고 복사한다
    static void Hold(string sd, int from, int to, Action draw) {
        if (from >= to) return;
        draw();
        for (int i = from + 1; i < to; i++) File.Copy(F(sd, from), F(sd, i), true);
    }
    static void App(MainWindow f, string sd, int i) { Pump.Run(120); Write(Snap(f.Host, AppScale), F(sd, i)); }

    // ------------------------------------------------------------ 장면들
    static void Intro(Settings s, string sd, int n) {
        WelcomeWindow w = new WelcomeWindow(s);
        w.ShowInTaskbar = false; w.ShowActivated = false;
        w.WindowState = WindowState.Normal; w.Width = 1040; w.Height = 686; w.Left = -6000; w.Top = 0;
        w.Show(); Pump.Run(600);
        FrameworkElement c = (FrameworkElement)w.Content;
        for (int i = 0; i < n; i++) {
            w.Seek(i / (double)Fps);
            Pump.Run(10);
            Write(Snap(c, AppScale), F(sd, i));
        }
        w.Close();
    }

    static void Convert(MainWindow f, string sd, int n) {
        f.VM.Page = "wizard";
        int half = n * 2 / 5;
        Hold(sd, 0, half, delegate { App(f, sd, 0); });
        f.VM.WizStep = 2;
        f.DropPreview(true);
        Hold(sd, half, half + n / 4, delegate { App(f, sd, half); });
        f.DropPreview(false);
        f.VM.DropPath(f.VM.S.Inp);
        Hold(sd, half + n / 4, n, delegate { App(f, sd, half + n / 4); });
    }

    static void Dash(MainWindow f, string sd, int n) {
        f.VM.SkipTexCheck = true;
        f.VM.ShotRun("fast");
        for (int i = 0; i < 400 && f.VM.Running; i++) Pump.Run(30);
        Pump.Run(300);
        f.VM.Tab = "res";
        Hold(sd, 0, n, delegate { App(f, sd, 0); });
    }

    static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject {
        List<T> r = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            DependencyObject c = VisualTreeHelper.GetChild(root, i);
            if (c is T) r.Add((T)c);
            r.AddRange(FindAll<T>(c));
        }
        return r;
    }

    static ScrollViewer PageScroll(MainWindow f) {
        foreach (ScrollViewer sv in FindAll<ScrollViewer>(f.Host)) if (sv.IsVisible && sv.ActualWidth > 500) return sv;
        return null;
    }

    static void ObjectScene(MainWindow f, string sd, int n) {
        f.VM.Page = "object"; f.VM.ObjModels = f.VM.S.Inp;
        Pump.Run(300);
        ScrollViewer sv = PageScroll(f);
        int still = n / 4;
        Hold(sd, 0, still, delegate { App(f, sd, 0); });
        for (int i = still; i < n; i++) {
            double t = Math.Min(1, (i - still) / (double)Math.Max(1, n - still - Fps));
            if (sv != null) sv.ScrollToVerticalOffset(Motion.EaseInOut(t) * sv.ScrollableHeight);
            App(f, sd, i);
        }
        if (sv != null) sv.ScrollToTop();
    }

    static void SplineScene(MainWindow f, string sd, int n) {
        f.VM.Page = "spline";
        f.VM.SplLanes = 2; f.VM.SplDir = "two"; f.VM.SplWalkL = "2"; f.VM.SplWalkR = "2";
        // 값이 바뀌는 순간마다 한 장씩, 그 사이는 복사
        object[][] steps = new object[][] {
            new object[] { 0.00, null }, new object[] { 0.22, "lanes3" }, new object[] { 0.38, "lanes4" },
            new object[] { 0.55, "walk3" }, new object[] { 0.72, "oneway" }, new object[] { 1.0, null } };
        for (int k = 0; k < steps.Length - 1; k++) {
            string act = steps[k][1] as string;
            if (act == "lanes3") f.VM.SplLanes = 3;
            if (act == "lanes4") f.VM.SplLanes = 4;
            if (act == "walk3") { f.VM.SplWalkL = "3"; f.VM.SplWalkR = "3"; }
            if (act == "oneway") f.VM.SplDir = "one";
            int a = (int)((double)steps[k][0] * n), b = (int)((double)steps[k + 1][0] * n);
            Hold(sd, a, b, delegate { App(f, sd, a); });
        }
        f.VM.SplLanes = 2; f.VM.SplDir = "two"; f.VM.SplWalkL = "2"; f.VM.SplWalkR = "2";
    }

    static void ViewerScene(MainWindow f, string sd, int n, string model) {
        f.VM.Page = "bus";
        f.VM.LoadModel(model);
        for (int i = 0; i < 600 && f.VM.ViewBusy; i++) Pump.Run(50);
        Pump.Run(300);
        ModelView mv = null;
        foreach (ModelView m in FindAll<ModelView>(f.Host)) if (m.IsVisible) mv = m;
        for (int i = 0; i < n; i++) {
            double t = i / (double)n;
            if (mv != null) mv.Orbit(35 + 300 * Motion.EaseInOut(t), 16 + 8 * Math.Sin(t * Math.PI), 1);
            App(f, sd, i);
        }
    }

    static void Langs(MainWindow f, string sd, int n) {
        f.VM.Page = "dashboard";
        string[][] seq = new string[][] { new string[] { "ko", "dark" }, new string[] { "de", "light" }, new string[] { "ja", "dark" }, new string[] { "zh", "light" }, new string[] { "en", "dark" } };
        for (int k = 0; k < seq.Length; k++) {
            f.VM.Lang = seq[k][0]; f.VM.ThemeName = seq[k][1];
            Pump.Run(400); Palette();
            int a = k * n / seq.Length, b = (k + 1) * n / seq.Length;
            Hold(sd, a, b, delegate { App(f, sd, a); });
        }
        f.VM.Lang = "en"; f.VM.ThemeName = "light"; Pump.Run(300); Palette();
    }

    static void Outro(string sd, int n) {
        Hold(sd, 0, n, delegate {
            Grid stage = new Grid(); stage.Width = W; stage.Height = H;
            stage.Background = new LinearGradientBrush(bgA, bgB, new Point(0, 0), new Point(1, 1));
            StackPanel sp = new StackPanel(); sp.HorizontalAlignment = HorizontalAlignment.Center; sp.VerticalAlignment = VerticalAlignment.Center;
            Image logo = new Image(); logo.Width = 220; logo.Height = 220;
            logo.Source = Art.Render(256, 256, delegate(DrawingContext dc) { Art.Logo(dc, 28, 28, 200); });
            sp.Children.Add(logo);
            TextBlock t = new TextBlock(); t.Text = AppInfo.Name; t.FontSize = 96; t.FontWeight = FontWeights.Bold; t.HorizontalAlignment = HorizontalAlignment.Center;
            t.FontFamily = Theme.MakeFont("nanum"); t.Foreground = Theme.Brush(Theme.Text); t.Margin = new Thickness(0, 18, 0, 0);
            sp.Children.Add(t);
            TextBlock u = new TextBlock(); u.Text = "github.com/" + AppInfo.Repo; u.FontSize = 36; u.HorizontalAlignment = HorizontalAlignment.Center;
            u.FontFamily = Theme.MakeFont("nanum"); u.Foreground = Theme.Brush(Theme.Accent); u.Margin = new Thickness(0, 14, 0, 0);
            sp.Children.Add(u);
            stage.Children.Add(sp);
            Render(stage, F(sd, 0));
        });
    }

    // ------------------------------------------------------------ 데모 버스 (영상용으로 직접 만든 단순한 모델: 상자 + 원기둥)
    static string DemoBus(string dir) {
        Directory.CreateDirectory(dir);
        string src = Path.Combine(dir, "src"); Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "demo_bus.mtl"),
            "newmtl body\nKd 1 1 1\nnewmtl stripe\nKd 0.3 0.38 1\nnewmtl glass\nKd 0.12 0.15 0.2\nnewmtl tyre\nKd 0.1 0.1 0.1\nnewmtl rim\nKd 0.75 0.75 0.78\nnewmtl light\nKd 1 0.95 0.7\nnewmtl roof\nKd 0.8 0.82 0.86\n");
        vnCount = 0;
        StringBuilder sb = new StringBuilder("mtllib demo_bus.mtl\no demo_bus\n");
        int[] vi = new int[] { 1 };
        const float hw = 1.25f, len = 6f;
        BoxObj(sb, vi, "body", -hw, hw, 0.35f, 1.35f, -len, len);            // 아래 차체
        BoxObj(sb, vi, "body", -hw, hw, 2.75f, 3.05f, -len, len);            // 지붕 테
        BoxObj(sb, vi, "glass", -hw + 0.03f, hw - 0.03f, 1.35f, 2.75f, -len + 0.05f, len - 0.05f);   // 유리 띠
        for (float z = -len + 0.6f; z < len - 0.4f; z += 1.55f) BoxObj(sb, vi, "body", -hw, hw, 1.35f, 2.75f, z, z + 0.14f);   // 창 기둥
        BoxObj(sb, vi, "stripe", -hw - 0.01f, hw + 0.01f, 1.0f, 1.22f, -len - 0.01f, len + 0.01f);
        BoxObj(sb, vi, "roof", -0.7f, 0.7f, 3.05f, 3.32f, -1.5f, 2.5f);
        BoxObj(sb, vi, "light", -1.05f, -0.7f, 0.62f, 0.82f, len, len + 0.03f);
        BoxObj(sb, vi, "light", 0.7f, 1.05f, 0.62f, 0.82f, len, len + 0.03f);
        foreach (float z in new float[] { -3.6f, 3.9f }) foreach (float x in new float[] { -1.12f, 1.12f }) {
            CylObj(sb, vi, "tyre", x, 0.5f, z, 0.5f, 0.32f);
            CylObj(sb, vi, "rim", x + (x > 0 ? 0.17f : -0.17f), 0.5f, z, 0.3f, 0.02f);
        }
        File.WriteAllText(Path.Combine(src, "demo_bus.obj"), sb.ToString());
        int nv, nt, nm; string wn;
        FastO3D.ConvertEx(Path.Combine(src, "demo_bus.obj"), Path.Combine(src, "demo_bus.o3d"), out nv, out nt, out nm, out wn);
        ScoOptions o = new ScoOptions(); o.Combine = true; o.Name = "Demo Bus";
        ScoMaker.Make(new List<string> { Path.Combine(src, "demo_bus.o3d") }, dir, o);
        return Path.Combine(dir, "Demo Bus.sco");
    }

    // 개발용(--cafe-shots): 앞문 애니메이션, 사람 길, 좌석까지 있는 데모 버스 (.bus). 남의 모드 대신 소개 화면에 쓴다
    public static string CafeBus(string dir) {
        string md = Path.Combine(dir, "model"); Directory.CreateDirectory(md);
        File.WriteAllText(Path.Combine(md, "demo.mtl"),
            "newmtl body\nKd 1 1 1\nnewmtl stripe\nKd 0.3 0.38 1\nnewmtl glass\nKd 0.12 0.15 0.2\nnewmtl tyre\nKd 0.1 0.1 0.1\nnewmtl rim\nKd 0.75 0.75 0.78\n" +
            "newmtl light\nKd 1 0.95 0.7\nnewmtl roof\nKd 0.8 0.82 0.86\nnewmtl door\nKd 0.95 0.75 0.2\nnewmtl floor\nKd 0.35 0.36 0.4\n");
        const float hw = 1.25f, len = 6f, d0 = 4.4f, d1 = 5.5f;   // 앞문: obj z d0..d1, 오른쪽(x+)
        // 차체
        vnCount = 0; int[] vi = new int[] { 1 };
        StringBuilder sb = new StringBuilder("mtllib demo.mtl\no body\n");
        BoxObj(sb, vi, "body", -hw, hw, 0.35f, 1.35f, -len, len);
        BoxObj(sb, vi, "body", -hw, hw, 2.75f, 3.05f, -len, len);
        BoxObj(sb, vi, "glass", -hw + 0.03f, hw - 0.03f, 1.35f, 2.75f, -len + 0.05f, len - 0.05f);
        for (float z = -len + 0.6f; z < len - 0.4f; z += 1.55f) BoxObj(sb, vi, "body", -hw, hw, 1.35f, 2.75f, z, z + 0.14f);
        BoxObj(sb, vi, "stripe", -hw - 0.01f, hw + 0.01f, 1.0f, 1.22f, -len - 0.01f, len + 0.01f);
        BoxObj(sb, vi, "roof", -0.7f, 0.7f, 3.05f, 3.32f, -1.5f, 2.5f);
        BoxObj(sb, vi, "light", -1.05f, -0.7f, 0.62f, 0.82f, len, len + 0.03f);
        BoxObj(sb, vi, "light", 0.7f, 1.05f, 0.62f, 0.82f, len, len + 0.03f);
        foreach (float z in new float[] { -3.6f, 3.9f }) foreach (float x in new float[] { -1.12f, 1.12f }) {
            CylObj(sb, vi, "tyre", x, 0.5f, z, 0.5f, 0.32f);
            CylObj(sb, vi, "rim", x + (x > 0 ? 0.17f : -0.17f), 0.5f, z, 0.3f, 0.02f);
        }
        File.WriteAllText(Path.Combine(md, "body.obj"), sb.ToString());
        // 앞문 (차체 바깥에 붙은 판)
        vnCount = 0; vi = new int[] { 1 };
        sb = new StringBuilder("mtllib demo.mtl\no door\n");
        BoxObj(sb, vi, "door", hw + 0.01f, hw + 0.06f, 0.4f, 2.7f, d0, d1);
        BoxObj(sb, vi, "glass", hw + 0.06f, hw + 0.07f, 1.45f, 2.55f, d0 + 0.12f, d1 - 0.12f);
        File.WriteAllText(Path.Combine(md, "door.obj"), sb.ToString());
        int nv, nt, nm; string wn;
        FastO3D.ConvertEx(Path.Combine(md, "body.obj"), Path.Combine(md, "body.o3d"), out nv, out nt, out nm, out wn);
        FastO3D.ConvertEx(Path.Combine(md, "door.obj"), Path.Combine(md, "door.o3d"), out nv, out nt, out nm, out wn);
        // 문 경첩: 문 판의 바깥 x, 앞쪽 끝. o3d (x, y위, z앞) → cfg (x, z, y). 앞쪽 = 절댓값이 큰 쪽
        O3dModel dm = O3dModel.Load(Path.Combine(md, "door.o3d"));
        double xmax = double.MinValue, ylo = double.MaxValue, yhi = double.MinValue;
        for (int i = 0; i < dm.V.Length / 8; i++) { xmax = Math.Max(xmax, dm.V[i * 8]); ylo = Math.Min(ylo, dm.V[i * 8 + 2]); yhi = Math.Max(yhi, dm.V[i * 8 + 2]); }
        double front = Math.Abs(yhi) > Math.Abs(ylo) ? yhi : ylo, back = front == yhi ? ylo : yhi, s = Math.Sign(front);
        CultureInfo ic = CultureInfo.InvariantCulture;
        Func<double, string> N = delegate(double v) { return v.ToString("0.###", ic); };
        File.WriteAllText(Path.Combine(md, "model.cfg"),
            "[friendlyname]\r\nDemo Bus\r\n\r\n[mesh]\r\nbody.o3d\r\n\r\n[mesh]\r\ndoor.o3d\r\n[newanim]\r\norigin_trans\r\n" + N(xmax) + "\r\n" + N(front) + "\r\n0\r\n" +
            "origin_rot_x\r\n0\r\norigin_rot_y\r\n-90\r\norigin_rot_z\r\n0\r\nanim_rot\r\ndoor_0\r\n" + (s > 0 ? "95" : "-95") + "\r\n", Encoding.ASCII);
        // 사람 길: 앞문 → 통로 → 뒤, 좌석: 통로 양쪽 + 기사석
        double dc = (front + back) / 2, fl = 0.4;
        StringBuilder p = new StringBuilder();
        List<double[]> pts = new List<double[]> { new double[] { 1.6, dc, 0 }, new double[] { 0.8, dc, fl }, new double[] { 0, dc, fl } };
        for (int k = 1; k <= 6; k++) pts.Add(new double[] { 0, dc - s * 1.6 * k, fl });
        foreach (double[] q in pts) p.Append("[pathpnt]\r\n").Append(N(q[0])).Append("\r\n").Append(N(q[1])).Append("\r\n").Append(N(q[2])).Append("\r\n\r\n");
        for (int i = 0; i + 1 < pts.Count; i++) p.Append("[pathlink]\r\n").Append(i).Append("\r\n").Append(i + 1).Append("\r\n\r\n");
        File.WriteAllText(Path.Combine(md, "paths.cfg"), p.ToString(), Encoding.ASCII);
        StringBuilder c = new StringBuilder();
        c.Append("[drivpos]\r\n-0.65\r\n").Append(N(front - s * 1.1)).Append("\r\n0.9\r\n0.45\r\n0\r\n\r\n");
        for (int r = 0; r < 6; r++) foreach (double x in new double[] { -0.75, 0.75 }) {
            double y = dc - s * (1.4 + r * 1.3);
            if (x > 0 && r == 0) continue;
            c.Append("[passpos]\r\n").Append(N(x)).Append("\r\n").Append(N(y)).Append("\r\n0.85\r\n0.45\r\n").Append(s > 0 ? "0" : "180").Append("\r\n\r\n");
        }
        c.Append("[entry]\r\n0\r\n\r\n[exit]\r\n0\r\n");
        File.WriteAllText(Path.Combine(md, "passengercabin.cfg"), c.ToString(), Encoding.ASCII);
        string bus = Path.Combine(dir, "Demo Bus.bus");
        File.WriteAllText(bus, "[friendlyname]\r\nBus Depot\r\nDemo Bus\r\n\r\n[model]\r\nmodel\\model.cfg\r\n\r\n[paths]\r\nmodel\\paths.cfg\r\n\r\n[passengercabin]\r\nmodel\\passengercabin.cfg\r\n", Encoding.ASCII);
        return bus;
    }

    // 개발용(--cafe-shots): 오브젝트 만들기로 만든 정류장 (.sco). OMSI 기본 오브젝트는 o3d가 암호화돼 있어 열 수 없으므로 직접 만든다
    public static string CafeShelter(string dir) {
        string src = Path.Combine(dir, "src"); Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "shelter.mtl"),
            "newmtl frame\nKd 0.25 0.27 0.3\nnewmtl glass\nKd 0.55 0.7 0.78\nnewmtl roof\nKd 0.92 0.93 0.95\nnewmtl bench\nKd 0.62 0.42 0.25\nnewmtl sign\nKd 0.98 0.8 0.1\nnewmtl signb\nKd 0.1 0.45 0.25\nnewmtl ground\nKd 0.7 0.7 0.68\n");
        vnCount = 0; int[] vi = new int[] { 1 };
        StringBuilder sb = new StringBuilder("mtllib shelter.mtl\no shelter\n");
        BoxObj(sb, vi, "ground", -2.6f, 2.6f, 0f, 0.12f, -1.2f, 1.6f);
        foreach (float x in new float[] { -2f, 2f }) foreach (float z in new float[] { -0.75f, 0.65f }) BoxObj(sb, vi, "frame", x - 0.05f, x + 0.05f, 0.12f, 2.5f, z - 0.05f, z + 0.05f);
        BoxObj(sb, vi, "roof", -2.25f, 2.25f, 2.5f, 2.62f, -1f, 0.95f);
        BoxObj(sb, vi, "glass", -1.95f, 1.95f, 0.35f, 2.35f, -0.78f, -0.74f);
        foreach (float x in new float[] { -2f, 2f }) BoxObj(sb, vi, "glass", x - 0.02f, x + 0.02f, 0.35f, 2.35f, -0.7f, 0.4f);
        BoxObj(sb, vi, "bench", -1.4f, 1.4f, 0.55f, 0.62f, -0.68f, -0.28f);
        foreach (float x in new float[] { -1.2f, 1.2f }) BoxObj(sb, vi, "frame", x - 0.04f, x + 0.04f, 0.12f, 0.55f, -0.6f, -0.36f);
        BoxObj(sb, vi, "frame", 2.4f, 2.48f, 0.12f, 3.2f, 1.2f, 1.28f);
        BoxObj(sb, vi, "signb", 2.2f, 2.68f, 2.6f, 3.1f, 1.18f, 1.2f);
        BoxObj(sb, vi, "sign", 2.25f, 2.63f, 2.65f, 3.05f, 1.15f, 1.18f);
        File.WriteAllText(Path.Combine(src, "shelter.obj"), sb.ToString());
        int nv, nt, nm; string wn;
        FastO3D.ConvertEx(Path.Combine(src, "shelter.obj"), Path.Combine(src, "shelter.o3d"), out nv, out nt, out nm, out wn);
        ScoOptions o = new ScoOptions(); o.Combine = true; o.Name = "Bus Shelter";
        ScoMaker.Make(new List<string> { Path.Combine(src, "shelter.o3d") }, dir, o);
        return Path.Combine(dir, "Bus Shelter.sco");
    }

    static void BoxObj(StringBuilder sb, int[] vi, string mat, float x0, float x1, float y0, float y1, float z0, float z1) {
        CultureInfo ic = CultureInfo.InvariantCulture;
        sb.Append("usemtl ").Append(mat).Append("\n");
        float[][] c = new float[][] {
            new float[] { x0, y0, z0 }, new float[] { x1, y0, z0 }, new float[] { x1, y1, z0 }, new float[] { x0, y1, z0 },
            new float[] { x0, y0, z1 }, new float[] { x1, y0, z1 }, new float[] { x1, y1, z1 }, new float[] { x0, y1, z1 } };
        int[][] faces = new int[][] { new int[] { 0, 3, 2, 1 }, new int[] { 4, 5, 6, 7 }, new int[] { 0, 4, 7, 3 }, new int[] { 1, 2, 6, 5 }, new int[] { 3, 7, 6, 2 }, new int[] { 0, 1, 5, 4 } };
        float[][] nrm = new float[][] { new float[] { 0, 0, -1 }, new float[] { 0, 0, 1 }, new float[] { -1, 0, 0 }, new float[] { 1, 0, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, -1, 0 } };
        for (int fi = 0; fi < 6; fi++) {
            sb.AppendFormat(ic, "vn {0} {1} {2}\n", nrm[fi][0], nrm[fi][1], nrm[fi][2]);
            foreach (int k in faces[fi]) sb.AppendFormat(ic, "v {0} {1} {2}\n", c[k][0], c[k][1], c[k][2]);
            int b = vi[0], ni = NextVn(vi);
            sb.AppendFormat("f {0}//{4} {1}//{4} {2}//{4} {3}//{4}\n", b, b + 1, b + 2, b + 3, ni);
            vi[0] += 4;
        }
    }

    // x축 방향 원기둥 (바퀴)
    static void CylObj(StringBuilder sb, int[] vi, string mat, float cx, float cy, float cz, float r, float w) {
        CultureInfo ic = CultureInfo.InvariantCulture;
        const int seg = 32;
        sb.Append("usemtl ").Append(mat).Append("\n");
        for (int i = 0; i < seg; i++) {
            double a0 = i * 2 * Math.PI / seg, a1 = (i + 1) * 2 * Math.PI / seg, am = (a0 + a1) / 2;
            float y0 = cy + (float)(r * Math.Sin(a0)), z0 = cz + (float)(r * Math.Cos(a0)), y1 = cy + (float)(r * Math.Sin(a1)), z1 = cz + (float)(r * Math.Cos(a1));
            // 옆면 사각형 + 양쪽 마개 삼각형 (정점은 면마다 따로: 노멀 번호 = 정점 번호를 4로 묶지 않으므로 vn도 정점마다)
            Quad(sb, vi, ic, new float[] { cx - w / 2, y0, z0 }, new float[] { cx - w / 2, y1, z1 }, new float[] { cx + w / 2, y1, z1 }, new float[] { cx + w / 2, y0, z0 },
                 new float[] { 0, (float)Math.Sin(am), (float)Math.Cos(am) });
            Tri(sb, vi, ic, new float[] { cx + w / 2, cy, cz }, new float[] { cx + w / 2, y0, z0 }, new float[] { cx + w / 2, y1, z1 }, new float[] { 1, 0, 0 });
            Tri(sb, vi, ic, new float[] { cx - w / 2, cy, cz }, new float[] { cx - w / 2, y1, z1 }, new float[] { cx - w / 2, y0, z0 }, new float[] { -1, 0, 0 });
        }
    }
    static int vnCount;
    static void Quad(StringBuilder sb, int[] vi, CultureInfo ic, float[] a, float[] b, float[] c, float[] d, float[] n) {
        foreach (float[] p in new float[][] { a, b, c, d }) sb.AppendFormat(ic, "v {0} {1} {2}\n", p[0], p[1], p[2]);
        sb.AppendFormat(ic, "vn {0} {1} {2}\n", n[0], n[1], n[2]);
        int ni = NextVn(vi);
        sb.AppendFormat("f {0}//{4} {1}//{4} {2}//{4} {3}//{4}\n", vi[0], vi[0] + 1, vi[0] + 2, vi[0] + 3, ni);
        vi[0] += 4;
    }
    static void Tri(StringBuilder sb, int[] vi, CultureInfo ic, float[] a, float[] b, float[] c, float[] n) {
        foreach (float[] p in new float[][] { a, b, c }) sb.AppendFormat(ic, "v {0} {1} {2}\n", p[0], p[1], p[2]);
        sb.AppendFormat(ic, "vn {0} {1} {2}\n", n[0], n[1], n[2]);
        int ni = NextVn(vi);
        sb.AppendFormat("f {0}//{3} {1}//{3} {2}//{3}\n", vi[0], vi[0] + 1, vi[0] + 2, ni);
        vi[0] += 3;
    }
    // vn은 쓴 순서대로 번호가 붙는다
    static int NextVn(int[] vi) { return ++vnCount; }
}
}
