using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Obj2x;

namespace BusDepotMac;

// 맥 버전의 환경: 설정 파일 위치, OMSI 2 / openOMSI Mods 폴더 찾기, OMSI 파일 인코딩
public static class MacEnv {
    public static Settings S;

    // 앱 번들 안은 쓸 수 없으므로 설정은 ~/Library/Application Support/BusDepot (Windows에서 시험할 때는 %APPDATA%\BusDepot)
    public static string DataDir {
        get {
            string home = Environment.GetEnvironmentVariable("HOME") ?? "";
            string d = OperatingSystem.IsMacOS() && home.Length > 0
                ? Path.Combine(home, "Library", "Application Support", "BusDepot")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BusDepot");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static void Init() {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        S = Settings.Load(Path.Combine(DataDir, "BusDepot.ini"));
        if (string.IsNullOrEmpty(S.Lang)) S.Lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
            "ko" => "ko", "de" => "de", "ja" => "ja", "zh" => "zh", _ => "en" };
        L.Lang = S.Lang;
        ApplyEncoding();
        if (!Omsi.IsInstall(S.OmsiDir)) { string d = FindOmsi(); if (d.Length > 0) S.OmsiDir = d; }
        if (string.IsNullOrEmpty(S.ModsDir) || !Directory.Exists(S.ModsDir)) S.ModsDir = FindMods();
        ApplyMods();
        S.Save();
    }

    // OMSI 파일은 대개 만든 사람의 Windows ANSI(한국어 949, 독일어 1252 ...). 맥에는 '시스템 ANSI'가 없으므로 고른다
    public static readonly int[] CodePages = { 949, 1252, 1250, 1251, 932, 936 };
    public static void ApplyEncoding() {
        int cp = S.AnsiCp;
        if (cp == 0) cp = S.Lang switch { "ko" => 949, "ja" => 932, "zh" => 936, _ => 1252 };
        try { Omsi.Ansi = Encoding.GetEncoding(cp); } catch (Exception) { Omsi.Ansi = Encoding.GetEncoding(1252); }
    }

    // 맵의 없는 파일 검사: OMSI 폴더 + Mods 폴더 + 그 안의 모드 폴더들
    public static void ApplyMods() {
        MapInfo.ExtraRoots = new List<string>();
        if (string.IsNullOrEmpty(S.ModsDir) || !Directory.Exists(S.ModsDir)) return;
        MapInfo.ExtraRoots.Add(S.ModsDir);
        try { MapInfo.ExtraRoots.AddRange(Directory.GetDirectories(S.ModsDir)); } catch (Exception) { }
    }

    // OMSI 2: Steam(맥은 ~/Library/Application Support/Steam), 그다음 openOMSI 설정(~/.openomsi)에 적힌 경로
    public static string FindOmsi() {
        string d = Omsi.FindInstall();
        if (d.Length > 0) return d;
        foreach (string p in OpenOmsiPaths()) if (Omsi.IsInstall(p)) return p;
        return "";
    }

    // openOMSI는 설정을 ~/.openomsi에 둔다. 형식은 정해져 있지 않다고 보고, 글자 파일 속의 'OMSI 2' 폴더 경로를 모두 모은다
    public static List<string> OpenOmsiPaths() {
        List<string> r = new List<string>();
        string home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(home, ".openomsi");
        if (!Directory.Exists(dir)) return r;
        try {
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)) {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".toml" && ext != ".json" && ext != ".ini" && ext != ".cfg" && ext != ".txt" && ext != ".ron" && ext != ".yaml") continue;
                string t; try { t = File.ReadAllText(f); } catch (Exception) { continue; }
                foreach (Match m in Regex.Matches(t, "[\"']((?:/|[A-Za-z]:\\\\)[^\"'\\r\\n]*OMSI 2[^\"'\\r\\n]*)[\"']"))
                    r.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
        } catch (Exception) { }
        return r;
    }

    // openOMSI의 Mods 폴더는 게임이 설치된 곳 옆 (openOMSI.app이 있는 폴더의 Mods).
    // openOMSI.app을 흔한 자리에서 찾고, 그 옆 Mods를 쓴다 (아직 없으면 처음 저장할 때 만든다). 못 찾으면 설정에서 고른다
    public static string FindMods() {
        string app = FindOpenOmsi();
        if (app != null) return Path.Combine(Path.GetDirectoryName(app), "Mods");
        return "";
    }
    public static string FindOpenOmsi() {
        string home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        List<string> dirs = new List<string> { "/Applications", Path.Combine(home, "Applications"), Path.Combine(home, "Desktop"), Path.Combine(home, "Downloads"), Path.Combine(home, "openOMSI") };
        foreach (string d in dirs) {
            try {
                if (!Directory.Exists(d)) continue;
                foreach (string a in Directory.GetDirectories(d, "*.app"))
                    if (Path.GetFileName(a).StartsWith("openOMSI", StringComparison.OrdinalIgnoreCase)) return a;
                // 한 단계 아래 폴더 (예: ~/Downloads/openOMSI-0.2.38/openOMSI.app)
                foreach (string sub in Directory.GetDirectories(d))
                    if (Path.GetFileName(sub).StartsWith("openOMSI", StringComparison.OrdinalIgnoreCase))
                        foreach (string a in Directory.GetDirectories(sub, "*.app"))
                            if (Path.GetFileName(a).StartsWith("openOMSI", StringComparison.OrdinalIgnoreCase)) return a;
            } catch (Exception) { }
        }
        return null;
    }

    // 만든 오브젝트/스플라인을 넣을 곳: Mods 폴더가 있으면 <Mods>/Bus Depot (OMSI 폴더 구조 그대로), 없으면 OMSI 2 폴더
    public static string MakeRoot {
        get {
            if (!string.IsNullOrEmpty(S.ModsDir) && (Directory.Exists(S.ModsDir) || Directory.Exists(Path.GetDirectoryName(S.ModsDir)))) return Path.Combine(S.ModsDir, "Bus Depot");
            return S.OmsiDir ?? "";
        }
    }
    public static bool OmsiOk { get { return Omsi.IsInstall(S.OmsiDir); } }
}
