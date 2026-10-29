using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Obj2x {
// obj2x.ini (PowerShell 버전과 같은 형식) 읽고 쓰기
public class Settings {
    public string Blender = @"C:\Program Files\Blender Foundation\Blender\blender.exe";
    public string Conv = "";
    public string Inp = "";
    public string Out = "";
    public bool DoConv = true;
    public int Par = Math.Max(1, Math.Min(20, Environment.ProcessorCount - 1));
    public bool SameDir = true;
    public bool UpdCheck = true;
    public bool Inc = false;
    public bool DelX = false;
    public bool Fast = false;
    public bool Sub = false;
    public string Theme = "light";   // system | light | dark
    public string Accent = "indigo"; // indigo | teal | rose
    public string Lang = "";
    public bool SysFont = false;
    public bool Agreed = false;
    public string AgreedVer = "";
    public List<string> Recent = new List<string>();
    public string ExOpt = "";        // 여러 줄 (줄바꿈)
    public Dictionary<string, int> Copt = new Dictionary<string, int>();
    public static readonly string[] CoptKeys = new string[] { "zup", "sep", "det", "spec", "mat", "vert", "nrm", "sub" };

    public const string DiscMinVer = "2.0";
    public string Path;

    public Settings() {
        Copt["zup"] = 0; Copt["sep"] = 0; Copt["det"] = 0; Copt["spec"] = 1;
        Copt["mat"] = 1; Copt["vert"] = 1; Copt["nrm"] = 0; Copt["sub"] = 0;
    }

    static bool B(string v, bool def) { if (v == null || v.Length == 0) return def; return v == "1"; }

    public static Settings Load(string path) {
        Settings s = new Settings();
        s.Path = path;
        Dictionary<string, string> kv = new Dictionary<string, string>();
        try {
            if (File.Exists(path)) {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8)) {
                    int i = line.IndexOf('=');
                    if (i > 0) kv[line.Substring(0, i)] = line.Substring(i + 1);
                }
            }
        } catch (Exception) { }
        string v;
        if (kv.TryGetValue("blender", out v)) s.Blender = v;
        if (kv.TryGetValue("conv", out v)) s.Conv = v;
        if (kv.TryGetValue("inp", out v)) s.Inp = v;
        if (kv.TryGetValue("out", out v)) s.Out = v;
        if (kv.TryGetValue("doconv", out v)) s.DoConv = B(v, true);
        if (kv.TryGetValue("par", out v)) { int n; if (int.TryParse(v, out n)) s.Par = Math.Max(1, Math.Min(20, n)); }
        if (kv.TryGetValue("samedir", out v)) s.SameDir = B(v, true);
        if (kv.TryGetValue("updcheck", out v)) s.UpdCheck = B(v, true);
        if (kv.TryGetValue("inc", out v)) s.Inc = B(v, false);
        if (kv.TryGetValue("delx", out v)) s.DelX = B(v, false);
        if (kv.TryGetValue("fast", out v)) s.Fast = B(v, false);
        if (kv.TryGetValue("sub", out v)) s.Sub = B(v, false);
        if (kv.TryGetValue("theme", out v) && (v == "light" || v == "dark" || v == "system")) s.Theme = v;
        if (kv.TryGetValue("accent", out v) && (v == "indigo" || v == "teal" || v == "rose")) s.Accent = v;
        if (kv.TryGetValue("lang", out v)) s.Lang = v;
        if (kv.TryGetValue("sysfont", out v)) s.SysFont = B(v, false);
        if (kv.TryGetValue("agreed", out v)) s.Agreed = B(v, false);
        if (kv.TryGetValue("agreedver", out v)) s.AgreedVer = v;
        if (kv.TryGetValue("recent", out v)) {
            foreach (string p in v.Split('|')) { if (p.Length > 0 && s.Recent.Count < 5) s.Recent.Add(p); }
        }
        if (kv.TryGetValue("exopt", out v)) s.ExOpt = v.Replace("|", "\r\n");
        if (kv.TryGetValue("copt", out v)) {
            foreach (string pair in v.Split(',')) {
                string[] p = pair.Split(':');
                int n;
                if (p.Length == 2 && s.Copt.ContainsKey(p[0]) && int.TryParse(p[1], out n)) s.Copt[p[0]] = (n != 0) ? 1 : 0;
            }
        }
        // 언어: ini에 없으면 시스템 UI 언어가 한국어가 아닐 때 영어
        if (s.Lang != "ko" && s.Lang != "en") {
            string cn = "";
            try { cn = System.Globalization.CultureInfo.CurrentUICulture.Name; } catch (Exception) { }
            s.Lang = cn.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? "ko" : "en";
        }
        return s;
    }

    public void Save() {
        try {
            List<string> o = new List<string>();
            o.Add("[settings]");
            o.Add("blender=" + Blender);
            o.Add("conv=" + Conv);
            o.Add("inp=" + Inp.TrimEnd('\\'));
            o.Add("out=" + Out.TrimEnd('\\'));
            o.Add("doconv=" + (DoConv ? "1" : "0"));
            o.Add("par=" + Par);
            o.Add("samedir=" + (SameDir ? "1" : "0"));
            o.Add("updcheck=" + (UpdCheck ? "1" : "0"));
            o.Add("inc=" + (Inc ? "1" : "0"));
            List<string> cp = new List<string>();
            foreach (string k in CoptKeys) cp.Add(k + ":" + Copt[k]);
            o.Add("copt=" + string.Join(",", cp.ToArray()));
            o.Add("delx=" + (DelX ? "1" : "0"));
            o.Add("fast=" + (Fast ? "1" : "0"));
            o.Add("sub=" + (Sub ? "1" : "0"));
            o.Add("theme=" + Theme);
            o.Add("accent=" + Accent);
            o.Add("lang=" + Lang);
            o.Add("sysfont=" + (SysFont ? "1" : "0"));
            o.Add("agreed=" + (Agreed ? "1" : "0"));
            o.Add("agreedver=" + AgreedVer);
            o.Add("recent=" + string.Join("|", Recent.ToArray()));
            List<string> ex = new List<string>();
            foreach (string ln in ExOpt.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) { if (ln.Trim().Length > 0) ex.Add(ln.Trim()); }
            o.Add("exopt=" + string.Join("|", ex.ToArray()));
            File.WriteAllLines(Path, o.ToArray(), new UTF8Encoding(true));
        } catch (Exception) { }
    }

    // 면책 조항 동의가 필요한가? 동의한 적이 없거나, 2.0 미만 버전에서 동의했으면 다시 받는다.
    public bool NeedDisclaimer() {
        if (!Agreed) return true;
        try { return new Version(AgreedVer).CompareTo(new Version(DiscMinVer)) < 0; }
        catch (Exception) { return true; }
    }

    // 최근 폴더: 맨 앞에 넣고 중복 제거, 최대 5개
    public void AddRecent(string p) {
        p = (p ?? "").Trim().TrimEnd('\\', '/');
        if (p.Length == 0) return;
        for (int i = Recent.Count - 1; i >= 0; i--) { if (string.Equals(Recent[i], p, StringComparison.OrdinalIgnoreCase)) Recent.RemoveAt(i); }
        Recent.Insert(0, p);
        while (Recent.Count > 5) Recent.RemoveAt(Recent.Count - 1);
    }
}
}
