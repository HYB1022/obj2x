using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Obj2x.UI {
// 개발용: 화면을 PNG로 저장한다 (obj2x.exe --shots 폴더, --readme-shots 폴더)
// 화면을 복사하지 않고 창 내용을 직접 그려서 저장하므로, 다른 창에 가려져도 같은 결과가 나온다.
public static class Shots {
    static void Save(MainWindow f, string path) { Save(f, f.Host, path); }
    static void Save(Window f, FrameworkElement c, string path) {
        Pump.Run(300);
        double sc = 1;
        PresentationSource ps = PresentationSource.FromVisual(f);
        if (ps != null && ps.CompositionTarget != null) sc = ps.CompositionTarget.TransformToDevice.M11;
        RenderTargetBitmap b = new RenderTargetBitmap((int)Math.Round(c.ActualWidth * sc), (int)Math.Round(c.ActualHeight * sc), 96 * sc, 96 * sc, PixelFormats.Pbgra32);
        b.Render(c);
        PngBitmapEncoder enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
        using (FileStream fs = File.Create(path)) enc.Save(fs);
    }

    static MainWindow Open(Settings s, string dir) {
        Loc.SetLang(s.Lang); Theme.Apply(s.Theme, s.Accent, s.SysFont);
        MainWindow f = new MainWindow(s, dir, false);
        f.FixClientSize(1040, 686);
        f.Left = 0; f.Top = 0;
        f.Show(); f.Activate(); Pump.Run(300);
        return f;
    }

    public static int Run(string dir) {
        Directory.CreateDirectory(dir);
        Motion.Off = true;
        string sample = Path.Combine(dir, "sample_obj"); Directory.CreateDirectory(sample);
        File.WriteAllText(Path.Combine(sample, "x.obj"), "v 0 0 0\nv 1 0 0\nv 1 1 0\nf 1 2 3\n");
        foreach (string[] v in new string[][] { new string[] { "light", "ko", "indigo" }, new string[] { "dark", "en", "teal" } }) {
            Settings s = new Settings(); s.Path = Path.Combine(dir, "shot.ini");
            s.Theme = v[0]; s.Lang = v[1]; s.Accent = v[2]; s.Inp = sample; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = AppInfo.Version;
            s.AddRecent(sample); s.AddRecent(@"C:\Models\Bus\ELE19"); s.AddRecent(@"D:\OMSI\Vehicles\Regs_SD77"); s.AddRecent(@"E:\gone\folder");
            MainWindow f = Open(s, dir);
            string tag = v[0] + "_" + v[1];
            f.VM.Page = "dashboard"; Save(f, Path.Combine(dir, tag + "_dash.png"));
            f.VM.Page = "wizard"; Save(f, Path.Combine(dir, tag + "_wiz1.png"));
            for (int i = 2; i <= 4; i++) { f.VM.WizStep = i; Save(f, Path.Combine(dir, tag + "_wiz" + i + ".png")); }
            f.VM.Page = "advanced"; Save(f, Path.Combine(dir, tag + "_adv.png"));
            f.VM.Page = "settings"; Save(f, Path.Combine(dir, tag + "_set.png"));
            f.VM.Page = "about"; Save(f, Path.Combine(dir, tag + "_about.png"));
            f.VM.FakeUpdate("9.9"); Save(f, Path.Combine(dir, tag + "_about_upd.png"));
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

    // 개발용: README에 넣을 화면 (obj2x.exe --readme-shots 폴더)
    // 가짜 blender.exe/OmsiXConv.exe로 경로 경고를 없애고, 샘플 obj를 빠른 변환으로 실제 변환해 결과 표를 채운다.
    // 사용자 경로가 보이는 화면(마법사 경로 단계, 고급의 프로그램 경로, 설정의 백업 폴더)은 찍지 않는다.
    public static int Readme(string dir) {
        Directory.CreateDirectory(dir);
        Motion.Off = true;
        string work = Path.Combine(Path.GetTempPath(), "obj2x_readme");
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
