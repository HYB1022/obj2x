using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Obj2x.UI;

// 운영체제마다 다른 것: 설정 파일 위치, OMSI 파일 인코딩, openOMSI content 폴더(맥)
public static class Platform {
    public static bool Mac => OperatingSystem.IsMacOS();

    // 맥은 앱 번들 안에 쓸 수 없어서 ~/Library/Application Support/BusDepot, Windows는 exe 옆 (예전과 같다)
    public static string DataDir {
        get {
            if (!Mac) return Program.Here;
            string home = Environment.GetEnvironmentVariable("HOME") ?? "";
            string d = Path.Combine(home, "Library", "Application Support", "BusDepot");
            try { Directory.CreateDirectory(d); } catch (Exception) { }
            return d;
        }
    }

    public static Settings LoadSettings(string here) {
        if (!Mac) {
            try { Updater.CleanupOld(Environment.ProcessPath); } catch (Exception) { }
            // 설정: BusDepot.ini. 이름을 바꾸기 전(obj2x.ini)에서 올라왔으면 한 번 옮겨 온다 (원래 파일은 남겨 둠)
            string ini = Path.Combine(here, AppInfo.Exe + ".ini"), oldIni = Path.Combine(here, "obj2x.ini");
            try { if (!File.Exists(ini) && File.Exists(oldIni)) File.Copy(oldIni, ini); } catch (Exception) { }
            try { Updater.CleanupObj2x(here); } catch (Exception) { }
            return Settings.Load(ini);
        }
        Settings s = Settings.Load(Path.Combine(DataDir, "BusDepot.ini"));
        if (string.IsNullOrEmpty(s.Lang)) s.Lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
            "ko" => "ko", "de" => "de", "ja" => "ja", "zh" => "zh", _ => "en" };
        if (string.IsNullOrEmpty(s.ModsDir) || !Directory.Exists(s.ModsDir)) s.ModsDir = FindContent();
        // 기본값(C:\Program Files\...\blender.exe)이나 없는 경로면 응용 프로그램 폴더의 Blender 2.7x.app을 찾고, 없으면 비워 둔다
        if (s.Blender.Contains('\\') || !File.Exists(s.Blender)) {
            var b = Omsi.FindBlenders(null).Find(x => x.Ver != null && x.Ver.Major == 2 && x.Ver.Minor < 80);
            s.Blender = b != null ? b.Path : "";
        }
        ApplyMods(s);
        return s;
    }

    // OMSI 파일은 대개 만든 사람의 Windows ANSI(한국어 949, 독일어 1252 ...). 맥에는 '시스템 ANSI'가 없으므로 설정에서 고른다 (0 = 언어에 맞춰)
    public static readonly int[] CodePages = { 949, 1252, 1250, 1251, 932, 936 };
    public static void ApplyEncoding(Settings s) {
        if (!Mac) return;
        int cp = s.AnsiCp;
        if (cp == 0) cp = s.Lang switch { "ko" => 949, "ja" => 932, "zh" => 936, _ => 1252 };
        try { Omsi.Ansi = Encoding.GetEncoding(cp); } catch (Exception) { Omsi.Ansi = Encoding.GetEncoding(1252); }
    }

    // openOMSI 모드 설치기는 모드를 ~/.openomsi/content에 OMSI 폴더 구조 그대로 풀어 넣는다
    public static string FindContent() {
        string home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string d = Path.Combine(home, ".openomsi", "content");
        return Directory.Exists(d) ? d : "";
    }
    // 맵의 없는 파일 검사·HOF 찾기: OMSI 폴더 + content 폴더
    public static void ApplyMods(Settings s) {
        MapInfo.ExtraRoots = new List<string>();
        if (Mac && !string.IsNullOrEmpty(s.ModsDir) && Directory.Exists(s.ModsDir)) MapInfo.ExtraRoots.Add(s.ModsDir);
    }
    // 만든 오브젝트/스플라인을 넣을 OMSI 구조의 뿌리: 맥은 content 폴더가 있으면 거기 (openOMSI는 OMSI 2 폴더에 쓰지 않는다)
    public static string MakeRoot(Settings s) {
        if (Mac && !string.IsNullOrEmpty(s.ModsDir) && Directory.Exists(s.ModsDir)) return s.ModsDir;
        return s.OmsiDir ?? "";
    }
    // 맥으로 옮겨 온 OMSI 폴더는 Omsi.exe가 없을 수도 있어서 Vehicles나 maps가 있어도 인정한다
    public static bool IsOmsi(string d) {
        if (string.IsNullOrEmpty(d)) return false;
        if (Omsi.IsInstall(d)) return true;
        return Mac && (Directory.Exists(Path.Combine(d, "Vehicles")) || Directory.Exists(Path.Combine(d, "maps")));
    }
}
