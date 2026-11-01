using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Obj2x.UI;

// 개발용 명령줄 (BusDepot --selftest, --shots 폴더, ...). 처리했으면 결과 코드, 아니면 -1
public static class DevTools {
    public static int Run(string[] args, int i, string here) {
        string A(int k) => i + k < args.Length ? args[i + k] : null;
        switch (args[i]) {
            // --selftest [결과 파일]: 결과 파일을 주면 기본 위치 대신 거기에 쓴다 (개발자 옵션의 '자체 점검 실행')
            case "--selftest": case "/selftest":
                return SelfTest.Run(here, A(1) != null && !A(1).StartsWith("-") ? A(1) : Path.Combine(Platform.DataDir, "BusDepot_selftest.txt"));
            case "--shots": return A(1) != null ? Shots.Run(A(1)) : -1;
            case "--readme-shots": return A(1) != null ? Shots.Readme(A(1), A(2) ?? "ko") : -1;
            case "--make-icon": if (A(1) == null) return -1; Look.Apply("light", "indigo", "nanum"); Art.WriteIco(A(1)); return 0;
            case "--make-icns": if (A(1) == null) return -1; Look.Apply("light", "indigo", "nanum"); Art.WriteIcns(A(1)); return 0;
            case "--video-frames": return A(2) != null ? Video.Run(A(1), A(2), A(3)) : -1;
            case "--cafe-shots": return A(2) != null ? Shots.Cafe(A(1), A(2)) : -1;
            case "--map-shot": return A(2) != null ? Shots.MapShot(A(1), A(2), A(3)) : -1;
            case "--save-model": return A(2) != null ? Shots.SaveModel(A(1), A(2), A(3)) : -1;
            case "--view-shot": return A(2) != null ? Shots.ViewShot(A(1), A(2), A(3) ?? "light", A(4) ?? "ext") : -1;
            case "--welcome-frames": return A(1) != null ? Shots.WelcomeFrames(A(1), A(2) ?? "light", A(3) ?? "ko") : -1;
            case "--fonttest": return A(1) != null ? FontTest(A(1)) : -1;
            // --update-test <로그>: 앱 안의 업데이트와 똑같이 최신 릴리스를 이 exe 폴더에 깔고 새 exe를 다시 띄운다 (버전이 같아도). 복사한 폴더에서만 쓴다
            case "--update-test": return A(1) != null ? UpdateTest(A(1)) : -1;
            case "--update-test-ran": if (A(1) == null) return -1; File.AppendAllText(A(1), "restarted v" + AppInfo.Version + "\r\n"); return 0;
        }
        return -1;
    }

    static int UpdateTest(string log) {
        string exe = Environment.ProcessPath, here = Path.GetDirectoryName(exe);
        File.WriteAllText(log, "from v" + AppInfo.Version + "\r\n");
        UpdateInfo u = Updater.Latest();
        if (u == null) { File.AppendAllText(log, "no release\r\n"); return 1; }
        string err = Updater.Install(u, here, exe);
        File.AppendAllText(log, "install " + (err ?? "ok") + "\r\n");
        try { MainViewModel.Restart(exe, "--update-test-ran \"" + log + "\""); File.AppendAllText(log, "start ok\r\n"); }
        catch (Exception ex) { File.AppendAllText(log, "start failed: " + ex.GetType().Name + " " + ex.Message + "\r\n"); }
        return 0;
    }

    // 글꼴이 실제로 무엇으로 잡히는지 (보통/굵게)
    static int FontTest(string path) {
        StringBuilder sb = new StringBuilder();
        foreach (string font in new[] { "nanum", "noto", "system", "misans" })
            foreach (FontWeight w in new[] { FontWeight.Normal, FontWeight.Bold }) {
                FontFamily fam = Look.MakeFont(font);
                bool ok = FontManager.Current.TryGetGlyphTypeface(new Typeface(fam, FontStyle.Normal, w), out IGlyphTypeface gt);
                sb.AppendLine(font + " " + w + " (" + fam + ") -> " + ok + " " + (gt != null ? gt.FamilyName + " w=" + gt.Weight : ""));
            }
        sb.AppendLine("fonts 폴더: " + string.Join(" | ", FileFonts.Dirs()));
        foreach (string d in FileFonts.Describe()) sb.AppendLine("  " + d);
        File.WriteAllText(path, sb.ToString());
        return 0;
    }
}
