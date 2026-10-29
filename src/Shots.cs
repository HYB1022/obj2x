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
}
}
