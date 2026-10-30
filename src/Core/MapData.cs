using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Obj2x {

// ---------------------------------------------------------------- OMSI 맵 (maps\<이름>\global.cfg + tile_*.map) 읽기 — 맵 탭용 (읽기만)
// 좌표: 타일 하나 300 m, 세계 좌표 = 타일 번호 × 300 + 타일 안 위치. y는 북쪽(위)
public class MapTile { public int X, Y; public string File; public bool Exists; }
public class MapObj { public string Path; public double X, Y; public bool Missing; }
public class MapSpline { public string Path; public double X, Y, Dir, Len, Rad; public bool Missing; }
public class UseItem { public string Path; public int Count; }
public class AiGroup { public string Name, Kind; public List<UseItem> Vehicles = new List<UseItem>(); }

public class MapInfo {
    public string Dir, Root, Name = "", Friendly = "", Description = "", Depot = "", Picture;
    public int Version, YearFrom, YearTo, Lines, Trips;
    public List<MapTile> Tiles = new List<MapTile>();
    public List<MapObj> Objects = new List<MapObj>();
    public List<MapSpline> Splines = new List<MapSpline>();
    public List<UseItem> MissingObjects = new List<UseItem>(), MissingSplines = new List<UseItem>();
    public List<AiGroup> Ai = new List<AiGroup>();
    public List<UseItem> MissingAi = new List<UseItem>();
    public List<string> Humans = new List<string>(), Drivers = new List<string>(), MissingHumans = new List<string>();
    public int MissingTiles;

    static double D(string s) { double v; double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v); return v; }
    static int I(string s) { int v; int.TryParse((s ?? "").Trim(), out v); return v; }

    // root: OMSI 폴더 (비우면 maps\<이름>의 두 단계 위)
    public static MapInfo Load(string dir, string root, Action<int, int> progress) {
        MapInfo m = new MapInfo(); m.Dir = dir;
        string guess = Path.GetDirectoryName(Path.GetDirectoryName(dir.TrimEnd('\\')) ?? "") ?? "";
        m.Root = File.Exists(Path.Combine(guess, "Omsi.exe")) || string.IsNullOrEmpty(root) ? guess : root;
        string[] L = Omsi.AnsiLines(Path.Combine(dir, "global.cfg"));
        for (int i = 0; i < L.Length; i++) {
            string k = L[i].Trim().ToLowerInvariant();
            Func<int, string> a = delegate(int n) { return i + n < L.Length ? L[i + n].Trim() : ""; };
            if (k == "[name]") m.Name = a(1);
            else if (k == "[friendlyname]") m.Friendly = a(1);
            else if (k == "[description]") {
                List<string> d = new List<string>();
                for (int j = i + 1; j < L.Length && !L[j].Trim().StartsWith("[") && d.Count < 40; j++) d.Add(L[j].Trim());
                m.Description = string.Join("\n", d.ToArray()).Trim();
            }
            else if (k == "[version]") m.Version = I(a(1));
            else if (k == "[years]") { m.YearFrom = I(a(1)); m.YearTo = I(a(2)); }
            else if (k == "[standarddepot]") m.Depot = a(1);
            else if (k == "[map]") {
                MapTile t = new MapTile(); t.X = I(a(1)); t.Y = I(a(2)); t.File = a(3);
                t.Exists = File.Exists(Path.Combine(dir, t.File)); if (!t.Exists) m.MissingTiles++;
                m.Tiles.Add(t);
            }
        }
        string pic = Path.Combine(dir, "picture.jpg"); if (File.Exists(pic)) m.Picture = pic;

        // 타일마다 오브젝트와 스플라인
        Dictionary<string, bool> exists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> objUse = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), splUse = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Func<string, bool> has = delegate(string rel) {
            bool e; if (exists.TryGetValue(rel, out e)) return e;
            try { e = File.Exists(Path.Combine(m.Root, rel)); } catch (Exception) { e = false; }
            exists[rel] = e; return e;
        };
        for (int ti = 0; ti < m.Tiles.Count; ti++) {
            MapTile t = m.Tiles[ti];
            if (progress != null && ti % 4 == 0) progress(ti, m.Tiles.Count);
            if (!t.Exists) continue;
            string[] T;
            try { T = Omsi.AnsiLines(Path.Combine(dir, t.File)); } catch (Exception) { continue; }
            double ox = t.X * 300.0, oy = t.Y * 300.0;
            for (int i = 0; i < T.Length; i++) {
                string k = T[i].Trim().ToLowerInvariant();
                if (k == "[object]" && i + 6 < T.Length) {
                    // 0, 경로, ID, x, y, 높이, 회전 ... (i+1부터)
                    MapObj o = new MapObj(); o.Path = T[i + 2].Trim(); o.X = ox + D(T[i + 4]); o.Y = oy + D(T[i + 5]);
                    o.Missing = !has(o.Path); m.Objects.Add(o);
                    int c; objUse.TryGetValue(o.Path, out c); objUse[o.Path] = c + 1;
                } else if ((k == "[spline]" || k == "[spline_h]") && i + 10 < T.Length) {
                    // 0, 경로, ID, 앞 ID, 뒤 ID, x, 높이, y, 방향(도, +y에서 +x 쪽), 길이, 반지름 ...
                    MapSpline s = new MapSpline(); s.Path = T[i + 2].Trim(); s.X = ox + D(T[i + 6]); s.Y = oy + D(T[i + 8]);
                    s.Dir = D(T[i + 9]); s.Len = D(T[i + 10]); s.Rad = i + 11 < T.Length ? D(T[i + 11]) : 0;
                    s.Missing = !has(s.Path); m.Splines.Add(s);
                    int c; splUse.TryGetValue(s.Path, out c); splUse[s.Path] = c + 1;
                }
            }
        }
        foreach (KeyValuePair<string, int> kv in objUse) if (!has(kv.Key)) { UseItem u = new UseItem(); u.Path = kv.Key; u.Count = kv.Value; m.MissingObjects.Add(u); }
        foreach (KeyValuePair<string, int> kv in splUse) if (!has(kv.Key)) { UseItem u = new UseItem(); u.Path = kv.Key; u.Count = kv.Value; m.MissingSplines.Add(u); }
        Comparison<UseItem> byUse = delegate(UseItem a1, UseItem b1) { int c = b1.Count.CompareTo(a1.Count); return c != 0 ? c : string.Compare(a1.Path, b1.Path, StringComparison.OrdinalIgnoreCase); };
        m.MissingObjects.Sort(byUse); m.MissingSplines.Sort(byUse);

        // AI 목록: [aigroup_2] 이름, 그다음 '경로<탭>대수' 줄들 ... [end]
        string ai = Path.Combine(dir, "ailists.cfg");
        if (File.Exists(ai)) {
            string[] A = Omsi.AnsiLines(ai);
            Dictionary<string, int> miss = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < A.Length; i++) {
                string k = A[i].Trim().ToLowerInvariant();
                if (!k.StartsWith("[aigroup")) continue;
                AiGroup g = new AiGroup(); g.Kind = k.Trim('[', ']'); g.Name = i + 1 < A.Length ? A[i + 1].Trim() : "";
                for (int j = i + 2; j < A.Length; j++) {
                    string l = A[j].Trim();
                    if (l.StartsWith("[")) break;
                    string[] p = l.Split('\t');
                    string path = p[0].Trim(); string pl = path.ToLowerInvariant();
                    if (!(pl.EndsWith(".bus") || pl.EndsWith(".ovh") || pl.EndsWith(".zug") || pl.EndsWith(".sco"))) continue;
                    UseItem v = new UseItem(); v.Path = path; v.Count = p.Length > 1 ? Math.Max(1, I(p[p.Length - 1])) : 1;
                    g.Vehicles.Add(v);
                    if (!has(path)) { int c; miss.TryGetValue(path, out c); miss[path] = c + v.Count; }
                }
                m.Ai.Add(g);
            }
            foreach (KeyValuePair<string, int> kv in miss) { UseItem u = new UseItem(); u.Path = kv.Key; u.Count = kv.Value; m.MissingAi.Add(u); }
            m.MissingAi.Sort(byUse);
        }
        // 사람 (humans.txt: 승객, drivers.txt: 기사)
        foreach (string[] f in new string[][] { new string[] { "humans.txt", "h" }, new string[] { "drivers.txt", "d" } }) {
            string p = Path.Combine(dir, f[0]); if (!File.Exists(p)) continue;
            foreach (string l0 in Omsi.AnsiLines(p)) {
                string l = l0.Trim(); if (!l.ToLowerInvariant().EndsWith(".hum")) continue;
                (f[1] == "h" ? m.Humans : m.Drivers).Add(l);
                if (!has(l) && !m.MissingHumans.Contains(l)) m.MissingHumans.Add(l);
            }
        }
        // 시간표 (TTData: .ttl 노선, .ttp 운행)
        string tt = Path.Combine(dir, "TTData");
        if (Directory.Exists(tt)) {
            try { m.Lines = Directory.GetFiles(tt, "*.ttl").Length; m.Trips = Directory.GetFiles(tt, "*.ttp").Length; } catch (Exception) { }
        }
        if (progress != null) progress(m.Tiles.Count, m.Tiles.Count);
        return m;
    }
}

// ---------------------------------------------------------------- HOF (차량 폴더의 <이름>.hof): 종점, 정류장, 운행
public class HofTrip { public string Code = "", Route = "", Terminus = "", Line = ""; public List<string> Stops = new List<string>(); }
public class HofFile {
    public string Path, Name = "", ServiceTrip = "";
    public List<string[]> Termini = new List<string[]>(), Busstops = new List<string[]>();
    public List<HofTrip> Trips = new List<HofTrip>();

    // [name]만 빨리 (목록용)
    public static string ReadName(string path) {
        try { string[] L = Omsi.AnsiLines(path); for (int i = 0; i + 1 < L.Length; i++) if (L[i].Trim().ToLowerInvariant() == "[name]") return L[i + 1].Trim(); } catch (Exception) { }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public static HofFile Load(string path) {
        HofFile h = new HofFile(); h.Path = path;
        string[] L = Omsi.AnsiLines(path);
        HofTrip cur = null;
        for (int i = 0; i < L.Length; i++) {
            string k = L[i].Trim().ToLowerInvariant();
            if (k == "[name]" && i + 1 < L.Length) h.Name = L[i + 1].Trim();
            else if (k == "[servicetrip]" && i + 1 < L.Length) h.ServiceTrip = L[i + 1].Trim();
            else if (k == "[addterminus_list]" || k == "[addbusstop_list]") {
                List<string[]> dst = k == "[addterminus_list]" ? h.Termini : h.Busstops;
                for (int j = i + 1; j < L.Length; j++) {
                    string l = L[j].Trim(); if (l.StartsWith("[")) break; if (l.Length == 0) continue;
                    dst.Add(L[j].Split('\t'));
                }
            } else if (k == "[infosystem_trip]" && i + 4 < L.Length) {
                cur = new HofTrip(); cur.Code = L[i + 1].Trim(); cur.Route = L[i + 2].Trim(); cur.Terminus = L[i + 3].Trim(); cur.Line = L[i + 4].Trim();
                h.Trips.Add(cur);
            } else if (k == "[infosystem_busstop_list]" && cur != null && i + 1 < L.Length) {
                int n; int.TryParse(L[i + 1].Trim(), out n);
                for (int j = 0; j < n && i + 2 + j < L.Length; j++) cur.Stops.Add(L[i + 2 + j].Trim());
            }
        }
        // 설명용 예시 블록({routecode} 같은 것)은 뺀다
        h.Trips.RemoveAll(delegate(HofTrip t) { return t.Code.StartsWith("{"); });
        return h;
    }
}
}
