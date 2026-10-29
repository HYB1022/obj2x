using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Obj2x {
// 개발용: 화면을 PNG로 저장한다 (obj2x.exe --shots 폴더)
public static class Shots {
    static void Pump(int ms) { DateTime e = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < e) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static void Save(Form f, string path) {
        Pump(150);
        Rectangle r = f.RectangleToScreen(f.ClientRectangle);
        Bitmap b = new Bitmap(r.Width, r.Height);
        using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        b.Save(path, System.Drawing.Imaging.ImageFormat.Png); b.Dispose();
    }
    static void SaveNow(Form f, string path) {
        Rectangle r = f.RectangleToScreen(f.ClientRectangle);
        using (Bitmap b = new Bitmap(r.Width, r.Height)) { using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); b.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
    }
    public static int Run(string dir) {
        Directory.CreateDirectory(dir);
        Anim.Off = true;
        string sample = Path.Combine(dir, "sample_obj"); Directory.CreateDirectory(sample);
        File.WriteAllText(Path.Combine(dir, "x.obj"), "v 0 0 0\nv 1 0 0\nv 1 1 0\nf 1 2 3\n");
        foreach (string[] v in new string[][] { new string[] { "light", "ko", "indigo" }, new string[] { "dark", "en", "teal" } }) {
            Settings s = new Settings(); s.Path = Path.Combine(dir, "shot.ini");
            s.Theme = v[0]; s.Lang = v[1]; s.Accent = v[2]; s.Inp = sample;
            s.AddRecent(sample); s.AddRecent(@"C:\Models\Bus\ELE19"); s.AddRecent(@"D:\OMSI\Vehicles\Regs_SD77"); s.AddRecent(@"E:\gone\folder");
            L.Lang = s.Lang; Theme.Apply(s.Theme, s.Accent); Ui.BuildFonts(false);
            MainForm f = new MainForm(s);
            f.WindowState = FormWindowState.Normal; f.ClientSize = new Size(1040, 686); f.Show(); Pump(200);
            string tag = v[0] + "_" + v[1];
            f.SwitchPage("dashboard"); Save(f, Path.Combine(dir, tag + "_dash.png"));
            f.SwitchPage("wizard"); Save(f, Path.Combine(dir, tag + "_wiz1.png"));
            for (int i = 2; i <= 4; i++) { f.WizGo(i); Save(f, Path.Combine(dir, tag + "_wiz" + i + ".png")); }
            f.SwitchPage("advanced"); Save(f, Path.Combine(dir, tag + "_adv.png"));
            f.SwitchPage("settings"); Save(f, Path.Combine(dir, tag + "_set.png"));
            f.SwitchPage("about"); Save(f, Path.Combine(dir, tag + "_about.png"));
            f.FakeUpdate("9.9"); Save(f, Path.Combine(dir, tag + "_about_upd.png"));
            f.SwitchPage("dashboard"); f.ShowDisclaimer(false); Save(f, Path.Combine(dir, tag + "_disc.png"));
            for (int i = f.Controls.Count - 1; i >= 0; i--) if (f.Controls[i] is DialogOverlay) f.Controls.RemoveAt(i);
            // 큰 창: 배율이 커져야 한다
            f.Location = new Point(0, 0); f.ClientSize = new Size(1664, 1050); Pump(100); f.UpdateZoom(); Pump(100);
            f.SwitchPage("dashboard"); Save(f, Path.Combine(dir, tag + "_z_dash.png"));
            f.SwitchPage("wizard"); f.WizGo(1); Save(f, Path.Combine(dir, tag + "_z_wiz1.png"));
            f.WizGo(2); Save(f, Path.Combine(dir, tag + "_z_wiz2.png"));
            f.SwitchPage("advanced"); Save(f, Path.Combine(dir, tag + "_z_adv.png"));
            f.SwitchPage("settings"); Save(f, Path.Combine(dir, tag + "_z_set.png"));
            f.SwitchPage("dashboard"); Dialogs.Msg(f, L.T("m_log_saved")); Save(f, Path.Combine(dir, tag + "_z_msg.png"));
            for (int i = f.Controls.Count - 1; i >= 0; i--) if (f.Controls[i] is DialogOverlay) f.Controls.RemoveAt(i);
            f.ClientSize = new Size(1040, 686); Pump(100); f.UpdateZoom(); Pump(100);
            if (v[0] == "dark") {
                Anim.Off = false;
                f.SwitchPage("dashboard"); Pump(300);
                f.SwitchPage("about");
                DateTime t0 = DateTime.Now;
                foreach (int ms in new int[] { 60, 250, 700 }) { while ((DateTime.Now - t0).TotalMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } SaveNow(f, Path.Combine(dir, "anim_about_" + ms + ".png")); }
                Dialogs.Msg(f, "Animation test"); t0 = DateTime.Now;
                foreach (int ms in new int[] { 60, 300 }) { while ((DateTime.Now - t0).TotalMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } SaveNow(f, Path.Combine(dir, "anim_dlg_" + ms + ".png")); }
                for (int i = f.Controls.Count - 1; i >= 0; i--) if (f.Controls[i] is DialogOverlay) f.Controls.RemoveAt(i);
                Anim.Off = true;
            }
            f.SwitchPage("advanced"); Save(f, Path.Combine(dir, tag + "_back_adv.png"));
            f.Close();
            WelcomeForm w = new WelcomeForm(s); w.WindowState = FormWindowState.Normal; w.ClientSize = new Size(1000, 660); w.Show(); Pump(200);
            Save(w, Path.Combine(dir, tag + "_welcome.png")); w.Close();
        }
        return 0;
    }

    // 개발용: README에 넣을 화면 (obj2x.exe --readme-shots 폴더)
    // 가짜 blender.exe/OmsiXConv.exe로 경로 경고를 없애고, 샘플 obj를 빠른 변환으로 실제 변환해 결과 표를 채운다.
    // 사용자 경로가 보이는 화면(마법사 경로 단계, 고급의 프로그램 경로, 설정의 백업 폴더)은 찍지 않는다.
    public static int Readme(string dir) {
        Directory.CreateDirectory(dir);
        Anim.Off = true;
        string work = Path.Combine(Path.GetTempPath(), "obj2x_readme");
        string models = Path.Combine(work, "ELE19");
        try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch (Exception) { }
        Directory.CreateDirectory(models);
        string bl = Path.Combine(work, "blender.exe"), cv = Path.Combine(work, "OmsiXConv.exe");
        File.WriteAllText(bl, ""); File.WriteAllText(cv, "");
        SampleModels(models);

        foreach (string th in new string[] { "light", "dark" }) {
            Ui.Z = 1f;   // 앞 창의 배율이 남지 않게
            Settings s = new Settings(); s.Path = Path.Combine(work, "shot.ini");
            s.Theme = th; s.Lang = "ko"; s.Accent = "indigo"; s.UpdCheck = false; s.Agreed = true; s.AgreedVer = Program.AppVersion;
            s.Inp = models; s.Blender = bl; s.Conv = cv; s.SameDir = true;
            L.Lang = s.Lang; Theme.Apply(s.Theme, s.Accent); Ui.BuildFonts(false);
            if (th == "light") {
                using (Bitmap b = new Bitmap(256, 256)) {
                    using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.Clear(Color.Transparent); LogoArt.Draw(g, 28, 28, 200); }
                    b.Save(Path.Combine(dir, "logo.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            MainForm f = new MainForm(s);
            f.WindowState = FormWindowState.Normal; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            f.ClientSize = new Size((int)(1040 * Ui.D), (int)(686 * Ui.D)); f.Show(); f.Activate(); Pump(300);
            f.SwitchPage("wizard"); Save(f, Path.Combine(dir, th + "_wizard.png"));
            f.ShotRun("fast");
            DateTime t0 = DateTime.Now;
            while (f.IsRunning && (DateTime.Now - t0).TotalSeconds < 30) Pump(50);
            Pump(200);
            f.SwitchPage("dashboard"); Save(f, Path.Combine(dir, th + "_dashboard.png"));
            f.FakeUpdate(NextVersion());
            f.SwitchPage("about"); Save(f, Path.Combine(dir, th + "_about.png"));
            f.Close();
        }
        try { Directory.Delete(work, true); } catch (Exception) { }
        return 0;
    }

    static string NextVersion() {
        try { Version v = new Version(Program.AppVersion); return v.Major + "." + (v.Minor + 1); } catch (Exception) { return Program.AppVersion; }
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
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("mtllib parts.mtl\no ").Append(name).Append("\n");
        System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;
        int vi = 1;
        // 면마다 sx x sz 격자로 나눠서 정점/삼각형 수가 그럴듯하게
        float[][] n = new float[][] { new float[] { 1, 0, 0 }, new float[] { -1, 0, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, -1, 0 }, new float[] { 0, 0, 1 }, new float[] { 0, 0, -1 } };
        sb.Append("usemtl ").Append(mat).Append("\n");
        foreach (float[] nn in n) {
            sb.AppendFormat(ic, "vn {0} {1} {2}\n", nn[0], nn[1], nn[2]);
        }
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
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        System.Globalization.CultureInfo ic = System.Globalization.CultureInfo.InvariantCulture;
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
