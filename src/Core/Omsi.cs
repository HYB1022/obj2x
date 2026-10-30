using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Obj2x {

// OMSI 2 관련: 설치 폴더 찾기, o3d/mtl에서 텍스처 이름 읽기, 텍스처 투명 여부
public static class Omsi {
    // Steam 설치 경로(레지스트리) + libraryfolders.vdf의 라이브러리마다 steamapps\common\OMSI 2를 찾는다
    public static string FindInstall() {
        foreach (string l in SteamLibraries()) {
            string d = Path.Combine(l, @"steamapps\common\OMSI 2");
            if (File.Exists(Path.Combine(d, "Omsi.exe"))) return d;
        }
        return "";
    }

    // Steam 설치 폴더(레지스트리) + libraryfolders.vdf에 적힌 라이브러리들
    public static List<string> SteamLibraries() {
        List<string> roots = new List<string>();
        foreach (string key in new string[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" }) {
            try {
                string p = Microsoft.Win32.Registry.GetValue(key, key.StartsWith("HKEY_CURRENT") ? "SteamPath" : "InstallPath", null) as string;
                if (!string.IsNullOrEmpty(p)) roots.Add(p.Replace('/', '\\'));
            } catch (Exception) { }
        }
        List<string> libs = new List<string>(roots);
        foreach (string r in roots) {
            try {
                string vdf = Path.Combine(r, @"steamapps\libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            } catch (Exception) { }
        }
        return libs;
    }

    // ---- Blender 찾기: Program Files의 Blender Foundation, Steam의 Blender. 가장 새 버전을 .blend 저장에 쓴다
    public class BlenderExe { public string Path; public Version Ver; public override string ToString() { return Ver != null ? Ver.ToString() : "?"; } }

    public static List<BlenderExe> FindBlenders(string extra) {
        List<string> cand = new List<string>();
        foreach (string pf in new string[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramW6432") }) {
            if (string.IsNullOrEmpty(pf)) continue;
            string bf = System.IO.Path.Combine(pf, "Blender Foundation");
            try { foreach (string d in Directory.GetDirectories(bf)) cand.Add(System.IO.Path.Combine(d, "blender.exe")); } catch (Exception) { }
        }
        foreach (string l in SteamLibraries()) cand.Add(System.IO.Path.Combine(l, @"steamapps\common\Blender\blender.exe"));
        if (!string.IsNullOrEmpty(extra)) cand.Add(extra);
        List<BlenderExe> r = new List<BlenderExe>();
        foreach (string c in cand) {
            string full; try { full = System.IO.Path.GetFullPath(c); } catch (Exception) { continue; }
            if (!File.Exists(full) || r.Exists(delegate(BlenderExe x) { return string.Equals(x.Path, full, StringComparison.OrdinalIgnoreCase); })) continue;
            BlenderExe b = new BlenderExe(); b.Path = full; b.Ver = BlenderVersion(full);
            r.Add(b);
        }
        r.Sort(delegate(BlenderExe a1, BlenderExe b1) { return (b1.Ver ?? new Version(0, 0)).CompareTo(a1.Ver ?? new Version(0, 0)); });
        return r;
    }

    // 파일 정보의 제품 버전, 없으면 'blender -b --version'의 첫 줄 (Steam판은 파일 정보가 비어 있다)
    public static Version BlenderVersion(string exe) {
        try {
            string pv = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).ProductVersion;
            Match m = Regex.Match(pv ?? "", @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (m.Success) return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
        } catch (Exception) { }
        try {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe, "-b --factory-startup --version");
            psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true;
            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi)) {
                string o = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(30000)) { try { p.Kill(); } catch (Exception) { } }
                Match m = Regex.Match(o, @"Blender (\d+)\.(\d+)(?:\.(\d+))?");
                if (m.Success) return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
            }
        } catch (Exception) { }
        return null;
    }

    // OMSI처럼 ANSI(시스템 코드 페이지)로 읽는다. 맨 앞의 UTF-8 BOM은 버린다 (File.ReadAllLines는 BOM을 보면 UTF-8로 바꿔 읽는다)
    public static string[] AnsiLines(string path) { return Lines(path, Encoding.Default); }
    public static string[] Utf8Lines(string path) { return Lines(path, new UTF8Encoding(false)); }
    static string[] Lines(string path, Encoding enc) {
        byte[] b = File.ReadAllBytes(path);
        int o = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
        return enc.GetString(b, o, b.Length - o).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    public static bool IsInstall(string dir) {
        try { return dir.Length > 0 && File.Exists(Path.Combine(dir, "Omsi.exe")); } catch (Exception) { return false; }
    }

    // o3d 안의 재질 텍스처 이름 (읽지 못하는 파일이면 빈 목록)
    public static List<string> O3dTextures(string path) {
        List<string> r = new List<string>();
        try {
            foreach (O3dMaterial m in O3dModel.Load(path).Mats) {
                string t = m.Tex;
                if (t.Length > 0 && !r.Exists(delegate(string x) { return string.Equals(x, t, StringComparison.OrdinalIgnoreCase); })) r.Add(t);
            }
        } catch (Exception) { }
        return r;
    }

    // mtl의 map_Kd/map_d (적힌 그대로의 경로)
    public static List<string> MtlTextures(string mtlPath) {
        List<string> r = new List<string>();
        try {
            foreach (string ln0 in File.ReadAllLines(mtlPath, Encoding.Default)) {
                string ln = ln0.Trim();
                if (!ln.StartsWith("map_Kd ") && !ln.StartsWith("map_d ")) continue;
                string t = ln.Substring(ln.IndexOf(' ') + 1).Trim();
                // 옵션(-s 1 1 1 등)이 앞에 붙은 경우: 마지막 낱말을 파일로 본다
                if (t.StartsWith("-")) { string[] w = t.Split(' '); t = w[w.Length - 1]; }
                if (t.Length > 0 && !r.Contains(t)) r.Add(t);
            }
        } catch (Exception) { }
        return r;
    }

    static string MtlOf(string objPath) {
        try {
            foreach (string ln in File.ReadLines(objPath, Encoding.Default)) {
                if (ln.StartsWith("mtllib ")) { string p = Path.Combine(Path.GetDirectoryName(objPath), ln.Substring(7).Trim()); if (File.Exists(p)) return p; break; }
                if (ln.StartsWith("v ")) break;   // mtllib은 맨 앞에 있다. 꼭짓점이 나오면 그만 읽는다
            }
        } catch (Exception) { }
        string m = Path.ChangeExtension(objPath, ".mtl");
        return File.Exists(m) ? m : null;
    }

    // 텍스처 파일 찾기: 적힌 경로, 기준 폴더, 그 안/옆의 texture 폴더
    public static string FindTexture(string name, params string[] dirs) {
        try { if (Path.IsPathRooted(name) && File.Exists(name)) return name; } catch (Exception) { return null; }
        string bn = Path.GetFileName(name.Replace('/', '\\'));
        foreach (string d in dirs) {
            if (string.IsNullOrEmpty(d)) continue;
            foreach (string c in new string[] { Path.Combine(d, name), Path.Combine(d, bn), Path.Combine(d, "texture", bn), Path.Combine(d, "textures", bn),
                                                Path.Combine(d, @"..\texture", bn) }) {
                try { if (File.Exists(c)) return Path.GetFullPath(c); } catch (Exception) { }
            }
        }
        return null;
    }

    // 변환 전 검사: obj의 mtl에 적힌 텍스처 중 없는 것. "모델 이름: 텍스처" 목록
    public static List<string> MissingTextures(List<ObjItem> objs) {
        List<string> r = new List<string>();
        foreach (ObjItem o in objs) {
            string mtl = MtlOf(o.Path);
            if (mtl == null) continue;
            string dir = Path.GetDirectoryName(mtl);
            foreach (string t in MtlTextures(mtl)) {
                if (FindTexture(t, dir) == null) r.Add(o.Rel + ": " + Path.GetFileName(t.Replace('/', '\\')));
            }
        }
        return r;
    }

    // 텍스처에 알파 채널이 있나 (tga/dds/png를 머리 부분만 읽어서). bmp/jpg는 없음
    public static bool HasAlpha(string path) {
        try {
            byte[] b = new byte[128];
            int n;
            using (FileStream fs = File.OpenRead(path)) n = fs.Read(b, 0, b.Length);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".tga" && n >= 18) return (b[17] & 0x0F) > 0 || b[16] == 32;
            if (ext == ".png" && n >= 26) return b[25] == 4 || b[25] == 6;
            if (ext == ".dds" && n >= 88) {
                uint flags = BitConverter.ToUInt32(b, 80);
                string four = Encoding.ASCII.GetString(b, 84, 4);
                return (flags & 0x1) != 0 || four == "DXT3" || four == "DXT5" || four == "DXT2" || four == "DXT4";
            }
        } catch (Exception) { }
        return false;
    }

    // 24비트 BMP 쓰기 (기본 스플라인 텍스처용). px(x, y) = 0xRRGGBB, y는 위에서부터
    public static void WriteBmp(string path, int w, int h, Func<int, int, int> px) {
        int row = (w * 3 + 3) & ~3, size = 54 + row * h;
        byte[] d = new byte[size];
        d[0] = (byte)'B'; d[1] = (byte)'M';
        BitConverter.GetBytes(size).CopyTo(d, 2); BitConverter.GetBytes(54).CopyTo(d, 10); BitConverter.GetBytes(40).CopyTo(d, 14);
        BitConverter.GetBytes(w).CopyTo(d, 18); BitConverter.GetBytes(h).CopyTo(d, 22);
        d[26] = 1; d[28] = 24; BitConverter.GetBytes(row * h).CopyTo(d, 34);
        for (int y = 0; y < h; y++) {
            int o = 54 + (h - 1 - y) * row;
            for (int x = 0; x < w; x++) { int c = px(x, y); d[o + x * 3] = (byte)c; d[o + x * 3 + 1] = (byte)(c >> 8); d[o + x * 3 + 2] = (byte)(c >> 16); }
        }
        File.WriteAllBytes(path, d);
    }

    // 파일 이름에 쓸 수 없는 글자를 _로
    public static string SafeName(string s) {
        s = (s ?? "").Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    internal static string N(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    internal static void CopyTex(string src, string dstDir, List<string> copied) {
        string dst = Path.Combine(dstDir, Path.GetFileName(src));
        if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase)) File.Copy(src, dst, true);
        copied.Add(Path.GetFileName(src));
        // 같은 이름의 .cfg(텍스처 설정), 밤 텍스처 폴더는 함께 옮기지 않는다 (사용자가 직접 관리)
    }
}

// ---------------------------------------------------------------- 오브젝트(.sco) 만들기
public class ScoOptions {
    public bool Combine;              // true: 모든 모델을 오브젝트 하나로, false: 모델마다 하나
    public string Name = "";          // Combine일 때 이름 (파일 이름, friendlyname)
    public string Group = "Bus Depot";
    public bool Collision = true, Surface, Shadow = true, Fixed = true;
    public int Alpha = 1;             // 0 끔, 1 잘라내기, 2 반투명 (알파 채널이 있는 텍스처에)
}

public class ScoResult {
    public List<string> Sco = new List<string>();
    public List<string> Copied = new List<string>();
    public List<string> Missing = new List<string>();
}

public static class ScoMaker {
    // models: o3d 경로들. outDir에 model\, texture\, *.sco를 만든다
    public static ScoResult Make(List<string> models, string outDir, ScoOptions o) {
        ScoResult res = new ScoResult();
        if (models.Count == 0) return res;
        string mdir = Path.Combine(outDir, "model"), tdir = Path.Combine(outDir, "texture");
        Directory.CreateDirectory(mdir); Directory.CreateDirectory(tdir);
        Dictionary<string, bool> alphaOf = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (string m in models) {
            string dst = Path.Combine(mdir, Path.GetFileName(m));
            if (!string.Equals(Path.GetFullPath(m), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase)) File.Copy(m, dst, true);
            string src = Path.GetDirectoryName(m);
            foreach (string t in Omsi.O3dTextures(m)) {
                if (alphaOf.ContainsKey(t)) continue;
                string f = Omsi.FindTexture(t, src, Path.GetDirectoryName(src), tdir);
                if (f == null) { alphaOf[t] = false; res.Missing.Add(Path.GetFileNameWithoutExtension(m) + ": " + t); continue; }
                Omsi.CopyTex(f, tdir, res.Copied);
                alphaOf[t] = Omsi.HasAlpha(f);
            }
        }
        if (o.Combine) {
            string name = Omsi.SafeName(o.Name);
            if (name.Length == 0) name = Path.GetFileName(outDir.TrimEnd('\\'));
            // 충돌 모델은 하나만 넣을 수 있다: 이름에 coll이 들어간 모델, 없으면 가장 큰 모델
            string coll = models.Find(delegate(string m) { return Path.GetFileName(m).IndexOf("coll", StringComparison.OrdinalIgnoreCase) >= 0; });
            if (coll == null) { coll = models[0]; foreach (string m in models) if (new FileInfo(m).Length > new FileInfo(coll).Length) coll = m; }
            res.Sco.Add(Write(Path.Combine(outDir, name + ".sco"), o.Name.Trim().Length > 0 ? o.Name.Trim() : name, models, coll, o, alphaOf));
        } else {
            foreach (string m in models) {
                string name = Path.GetFileNameWithoutExtension(m);
                res.Sco.Add(Write(Path.Combine(outDir, name + ".sco"), name, new List<string> { m }, m, o, alphaOf));
            }
        }
        return res;
    }

    static string Write(string path, string friendly, List<string> models, string coll, ScoOptions o, Dictionary<string, bool> alphaOf) {
        StringBuilder sb = new StringBuilder();
        sb.Append("Made with " + AppInfo.Name + " ").Append(AppInfo.Version).Append("\r\n\r\n");
        sb.Append("[friendlyname]\r\n").Append(friendly).Append("\r\n\r\n");
        sb.Append("[groups]\r\n1\r\n").Append(o.Group.Trim().Length > 0 ? o.Group.Trim() : "Bus Depot").Append("\r\n\r\n");
        if (o.Fixed) sb.Append("[fixed]\r\n\r\n");
        if (o.Shadow) sb.Append("[shadow]\r\n\r\n");
        if (o.Surface) sb.Append("[surface]\r\n\r\n");
        if (o.Collision && coll != null) sb.Append("[collision_mesh]\r\n").Append(Path.GetFileName(coll)).Append("\r\n\r\n");
        foreach (string m in models) {
            sb.Append("[mesh]\r\n").Append(Path.GetFileName(m)).Append("\r\n\r\n");
            if (o.Alpha == 0) continue;
            foreach (string t in Omsi.O3dTextures(m)) {
                bool a; if (!alphaOf.TryGetValue(t, out a) || !a) continue;
                sb.Append("[matl]\r\n").Append(t).Append("\r\n0\r\n");
                sb.Append("[matl_alpha]\r\n").Append(o.Alpha).Append("\r\n\r\n");
            }
        }
        File.WriteAllText(path, sb.ToString(), Encoding.Default);
        return path;
    }
}

// ---------------------------------------------------------------- 스플라인(.sli) 만들기
public class SplineSpec {
    public int Lanes = 2;
    public double LaneW = 3.25;
    public bool OneWay;
    public bool PerLane;              // 노면 텍스처를 차선마다 한 번씩 (아니면 도로 전체에 한 번)
    public bool ExtraRight = true;    // 양방향 + 홀수 차선일 때 남는 한 차선을 오른쪽(정방향)에 둘지, 왼쪽(역방향)에 둘지
    // 역방향(왼쪽) 차선 수
    public int BackLanes { get { return OneWay ? 0 : (Lanes % 2 == 1 && !ExtraRight ? Lanes / 2 + 1 : Lanes / 2); } }
    public double WalkL = 2, WalkR = 2, Curb = 0.15;
    public double RoadRepeat = 5, WalkRepeat = 2;   // 텍스처 한 장이 덮는 길이(m)
    public string RoadTex = "", WalkTex = "";       // 비우면 기본 텍스처를 만든다

    // 설정 파일에 한 줄로 (텍스처 경로는 |가 들어갈 수 없으니 구분자로 쓴다)
    public string Pack() {
        return string.Join("|", new string[] { Lanes.ToString(), Omsi.N(LaneW), OneWay ? "1" : "0", PerLane ? "1" : "0", Omsi.N(WalkL), Omsi.N(WalkR),
                                                Omsi.N(Curb), Omsi.N(RoadRepeat), Omsi.N(WalkRepeat), RoadTex ?? "", WalkTex ?? "", ExtraRight ? "1" : "0" });
    }
    public static SplineSpec Unpack(string s) {
        SplineSpec r = new SplineSpec();
        string[] a = (s ?? "").Split('|');
        if (a.Length < 11) return r;
        try {
            CultureInfo c = CultureInfo.InvariantCulture;
            r.Lanes = int.Parse(a[0]); r.LaneW = double.Parse(a[1], c); r.OneWay = a[2] == "1"; r.PerLane = a[3] == "1";
            r.WalkL = double.Parse(a[4], c); r.WalkR = double.Parse(a[5], c); r.Curb = double.Parse(a[6], c);
            r.RoadRepeat = double.Parse(a[7], c); r.WalkRepeat = double.Parse(a[8], c); r.RoadTex = a[9]; r.WalkTex = a[10];
            if (a.Length > 11) r.ExtraRight = a[11] != "0";
        } catch (Exception) { return new SplineSpec(); }
        string err;
        return r.Valid(out err) ? r : new SplineSpec();
    }

    public double RoadW { get { return Lanes * LaneW; } }
    public double TotalW { get { return RoadW + WalkL + WalkR; } }

    // 프로필 점 (왼쪽 → 오른쪽으로 이어야 면이 위/도로 쪽을 본다)
    public class Prof { public int Tex; public double Repeat; public List<double[]> Pts = new List<double[]>(); }   // x, y, u
    public class PathDef { public int Kind, Dir; public double X, Y, W; }

    public bool Valid(out string err) {
        err = null;
        if (Lanes < 1 || Lanes > 12) err = "spl_err_lanes";
        else if (LaneW < 1 || LaneW > 10) err = "spl_err_lanew";
        else if (WalkL < 0 || WalkR < 0 || WalkL > 20 || WalkR > 20) err = "spl_err_walk";
        else if (Curb < 0 || Curb > 1) err = "spl_err_curb";
        else if (RoadRepeat <= 0 || WalkRepeat <= 0) err = "spl_err_repeat";
        return err == null;
    }

    public List<Prof> Profiles() {
        List<Prof> r = new List<Prof>();
        double h = RoadW / 2;
        if (WalkL > 0) {
            Prof p = new Prof(); p.Tex = 1; p.Repeat = WalkRepeat;
            p.Pts.Add(new double[] { -h - WalkL, Curb, 0 });
            p.Pts.Add(new double[] { -h, Curb, WalkL / WalkRepeat });
            if (Curb > 0) p.Pts.Add(new double[] { -h, 0, (WalkL + Curb) / WalkRepeat });
            r.Add(p);
        }
        if (PerLane) {
            for (int i = 0; i < Lanes; i++) {
                Prof p = new Prof(); p.Tex = 0; p.Repeat = RoadRepeat;
                p.Pts.Add(new double[] { -h + i * LaneW, 0, 0 });
                p.Pts.Add(new double[] { -h + (i + 1) * LaneW, 0, 1 });
                r.Add(p);
            }
        } else {
            Prof p = new Prof(); p.Tex = 0; p.Repeat = RoadRepeat;
            p.Pts.Add(new double[] { -h, 0, 0 });
            p.Pts.Add(new double[] { h, 0, 1 });
            r.Add(p);
        }
        if (WalkR > 0) {
            Prof p = new Prof(); p.Tex = 1; p.Repeat = WalkRepeat;
            if (Curb > 0) p.Pts.Add(new double[] { h, 0, 0 });
            p.Pts.Add(new double[] { h, Curb, Curb / WalkRepeat });
            p.Pts.Add(new double[] { h + WalkR, Curb, (Curb + WalkR) / WalkRepeat });
            r.Add(p);
        }
        return r;
    }

    // 차는 오른쪽 통행: 진행 방향 오른쪽(x > 0) 차선이 정방향(0), 왼쪽은 역방향(1). 사람 길은 양방향(2)
    public List<PathDef> Paths() {
        List<PathDef> r = new List<PathDef>();
        double h = RoadW / 2;
        int back = BackLanes;
        for (int i = 0; i < Lanes; i++) {
            PathDef p = new PathDef(); p.Kind = 0; p.X = -h + (i + 0.5) * LaneW; p.Y = 0; p.W = LaneW; p.Dir = i < back ? 1 : 0;
            r.Add(p);
        }
        if (WalkL > 0) { PathDef p = new PathDef(); p.Kind = 1; p.X = -h - WalkL / 2; p.Y = Curb; p.W = WalkL; p.Dir = 2; r.Add(p); }
        if (WalkR > 0) { PathDef p = new PathDef(); p.Kind = 1; p.X = h + WalkR / 2; p.Y = Curb; p.W = WalkR; p.Dir = 2; r.Add(p); }
        return r;
    }

    public string Text(string roadTexName, string walkTexName) {
        StringBuilder sb = new StringBuilder();
        sb.Append("Made with " + AppInfo.Name + " ").Append(AppInfo.Version).Append("\r\n\r\n");
        double h = RoadW / 2;
        // 높이 프로필: 바닥(차, 사람이 서는 면)
        if (WalkL > 0) HP(sb, -h - WalkL, -h, Curb);
        HP(sb, -h, h, 0);
        if (WalkR > 0) HP(sb, h, h + WalkR, Curb);
        sb.Append("[texture]\r\n").Append(roadTexName).Append("\r\n\r\n");
        bool walk = WalkL > 0 || WalkR > 0;
        if (walk) sb.Append("[texture]\r\n").Append(walkTexName).Append("\r\n\r\n");
        foreach (Prof p in Profiles()) {
            sb.Append("[profile]\r\n").Append(p.Tex).Append("\r\n\r\n");
            foreach (double[] pt in p.Pts)
                sb.Append("[profilepnt]\r\n").Append(Omsi.N(pt[0])).Append("\r\n").Append(Omsi.N(pt[1])).Append("\r\n")
                  .Append(Omsi.N(pt[2])).Append("\r\n").Append(Omsi.N(1 / p.Repeat)).Append("\r\n\r\n");
        }
        foreach (PathDef p in Paths())
            sb.Append("[path]\r\n").Append(p.Kind).Append("\r\n").Append(Omsi.N(p.X)).Append("\r\n").Append(Omsi.N(p.Y)).Append("\r\n")
              .Append(Omsi.N(p.W)).Append("\r\n").Append(p.Dir).Append("\r\n\r\n");
        return sb.ToString();
    }

    static void HP(StringBuilder sb, double x1, double x2, double y) {
        sb.Append("[heightprofile]\r\n").Append(Omsi.N(x1)).Append("\r\n").Append(Omsi.N(x2)).Append("\r\n").Append(Omsi.N(y)).Append("\r\n").Append(Omsi.N(y)).Append("\r\n\r\n");
    }

    // splineDir(Splines\<폴더>)에 이름.sli와 texture\ 를 만든다. 돌려주는 값: 만든 sli 경로
    public string Save(string splineDir, string name) {
        string tdir = Path.Combine(splineDir, "texture");
        Directory.CreateDirectory(tdir);
        List<string> copied = new List<string>();
        string road = TexFile(RoadTex, tdir, "busdepot_asphalt.bmp", false, copied);
        string walk = (WalkL > 0 || WalkR > 0) ? TexFile(WalkTex, tdir, "busdepot_pavement.bmp", true, copied) : "";
        string path = Path.Combine(splineDir, Omsi.SafeName(name) + ".sli");
        File.WriteAllText(path, Text(road, walk), Encoding.Default);
        return path;
    }

    static string TexFile(string src, string tdir, string def, bool tiles, List<string> copied) {
        if (!string.IsNullOrEmpty(src) && File.Exists(src)) { Omsi.CopyTex(src, tdir, copied); return Path.GetFileName(src); }
        string p = Path.Combine(tdir, def);
        if (!File.Exists(p)) {
            Random rnd = new Random(tiles ? 7 : 3);
            if (tiles) Omsi.WriteBmp(p, 256, 256, delegate(int x, int y) {
                // 보도블록: 64px 격자, 줄눈은 어둡게
                int g = (x % 64 < 3 || y % 64 < 3) ? 120 : 176 + rnd.Next(-10, 11);
                return (g << 16) | (g << 8) | (g - 4);
            });
            else Omsi.WriteBmp(p, 256, 256, delegate(int x, int y) {
                int g = 74 + rnd.Next(-12, 13);
                return (g << 16) | (g << 8) | (g + 2);
            });
        }
        return def;
    }
}
}
