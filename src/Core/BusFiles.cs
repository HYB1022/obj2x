using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Obj2x {

// OMSI 설정 파일(cfg 등)을 줄 그대로 들고 있다가 값이 적힌 줄만 바꿔 쓴다 (주석, 순서, 다른 내용은 그대로).
// OMSI처럼 ANSI로 읽고 쓰며, 처음 저장할 때 원래 파일을 <이름>.bak으로 남긴다.
public class CfgDoc {
    public string Path;
    public List<string> Lines = new List<string>();
    public bool Dirty;
    public event Action Changed;

    public static CfgDoc Load(string path) {
        CfgDoc d = new CfgDoc(); d.Path = path;
        d.Lines = new List<string>(Omsi.AnsiLines(path));
        return d;
    }

    public static string Fmt(double v) { return v.ToString("0.######", CultureInfo.InvariantCulture); }
    public static bool Num(string s, out double v) { return double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    public double NumAt(int line) { double v; return line >= 0 && line < Lines.Count && Num(Lines[line], out v) ? v : 0; }
    public string At(int line) { return line >= 0 && line < Lines.Count ? Lines[line].Trim() : ""; }

    // 줄 앞의 들여쓰기는 남기고 값만 바꾼다
    public void Set(int line, string value) {
        if (line < 0 || line >= Lines.Count) return;
        string old = Lines[line]; int k = 0; while (k < old.Length && (old[k] == ' ' || old[k] == '\t')) k++;
        string nw = old.Substring(0, k) + value;
        if (nw == old) return;
        Lines[line] = nw; Touch();
    }
    public void Set(int line, double v) { Set(line, Fmt(v)); }
    public void Append(params string[] lines) {
        if (Lines.Count > 0 && Lines[Lines.Count - 1].Trim().Length == 0) Lines.RemoveAt(Lines.Count - 1);
        Lines.Add(""); Lines.AddRange(lines); Lines.Add(""); Touch();
    }
    public void RemoveRange(int start, int count) { if (start < 0 || start + count > Lines.Count) return; Lines.RemoveRange(start, count); Touch(); }
    void Touch() { Dirty = true; if (Changed != null) Changed(); }

    public void Save() {
        string bak = Path + ".bak";
        if (File.Exists(Path) && !File.Exists(bak)) File.Copy(Path, bak);
        File.WriteAllText(Path, string.Join("\r\n", Lines.ToArray()), Encoding.Default);
        Dirty = false;
    }
}

// ---------------------------------------------------------------- 애니메이션 ([newanim])
public class AnimEntry { public string Kind, Var; public double Factor; public int Line; public bool Trans { get { return Kind == "anim_trans"; } } }
public class AnimBlock {
    public int Line;                                   // [newanim] 줄
    public double[] Trans = new double[3], Rot = new double[3];
    public int[] TransLine = new int[] { -1, -1, -1 }, RotLine = new int[] { -1, -1, -1 };
    public bool HasTrans, FromMesh;
    public List<AnimEntry> Entries = new List<AnimEntry>();
    public bool Moves { get { return Entries.Exists(delegate(AnimEntry e) { return Math.Abs(e.Factor) > 1e-4; }); } }

    // [newanim] 다음 줄부터 다음 [키] 앞까지
    public static AnimBlock Parse(List<string> L, int at) {
        AnimBlock b = new AnimBlock(); b.Line = at;
        for (int j = at + 1; j < L.Count; j++) {
            string k = L[j].Trim().ToLowerInvariant();
            if (k.StartsWith("[")) break;
            double v;
            if (k == "origin_trans" && j + 3 < L.Count) {
                for (int c = 0; c < 3; c++) { b.TransLine[c] = j + 1 + c; CfgDoc.Num(L[j + 1 + c], out v); b.Trans[c] = v; }
                b.HasTrans = true; j += 3;
            } else if (k == "origin_from_mesh") b.FromMesh = true;
            else if ((k == "origin_rot_x" || k == "origin_rot_y" || k == "origin_rot_z") && j + 1 < L.Count) {
                int c = k[k.Length - 1] - 'x'; b.RotLine[c] = j + 1; CfgDoc.Num(L[j + 1], out v); b.Rot[c] = v; j++;
            } else if ((k == "anim_rot" || k == "anim_trans") && j + 2 < L.Count) {
                AnimEntry e = new AnimEntry(); e.Kind = k; e.Var = L[j + 1].Trim(); e.Line = j + 2; CfgDoc.Num(L[j + 2], out v); e.Factor = v;
                b.Entries.Add(e); j += 2;
            }
        }
        return b;
    }
}

// ---------------------------------------------------------------- .bus / .ovh: 이름과 연결된 파일들
public class BusFile {
    public string Path, Dir, Name = "", Model = "", Paths = "", Cabin = "";
    public static BusFile Load(string path) {
        BusFile b = new BusFile(); b.Path = path; b.Dir = System.IO.Path.GetDirectoryName(path);
        string[] L = Omsi.AnsiLines(path);
        for (int i = 0; i + 1 < L.Length; i++) {
            string k = L[i].Trim().ToLowerInvariant(), a = L[i + 1].Trim();
            if (k == "[friendlyname]" && b.Name.Length == 0) {
                // [friendlyname] 제조사 / 모델 / 도색 순서. 앞 두 줄을 이름으로
                string a2 = i + 2 < L.Length ? L[i + 2].Trim() : "";
                b.Name = (a + " " + a2).Trim();
            }
            else if (k == "[model]" && b.Model.Length == 0) b.Model = System.IO.Path.Combine(b.Dir, a);
            else if (k == "[paths]" && b.Paths.Length == 0) b.Paths = System.IO.Path.Combine(b.Dir, a);
            else if (k == "[passengercabin]" && b.Cabin.Length == 0) b.Cabin = System.IO.Path.Combine(b.Dir, a);
        }
        if (b.Name.Length == 0) b.Name = System.IO.Path.GetFileNameWithoutExtension(path);
        return b;
    }
}

// ---------------------------------------------------------------- paths.cfg: [pathpnt] x y z, [pathlink] a b
public class PathPoint { public int Index, Line; public double[] P = new double[3]; }
public class PathLink { public int A, B, Line; }
public class PathsCfg {
    public CfgDoc Doc;
    public List<PathPoint> Points = new List<PathPoint>();
    public List<PathLink> Links = new List<PathLink>();
    public static PathsCfg Load(string path) { PathsCfg p = new PathsCfg(); p.Doc = CfgDoc.Load(path); p.Parse(); return p; }
    public void Parse() {
        Points.Clear(); Links.Clear();
        List<string> L = Doc.Lines;
        for (int i = 0; i < L.Count; i++) {
            string k = L[i].Trim().ToLowerInvariant();
            if (k == "[pathpnt]" && i + 3 < L.Count) {
                PathPoint p = new PathPoint(); p.Index = Points.Count; p.Line = i;
                for (int c = 0; c < 3; c++) p.P[c] = Doc.NumAt(i + 1 + c);
                Points.Add(p); i += 3;
            } else if (k == "[pathlink]" && i + 2 < L.Count) {
                PathLink l = new PathLink(); l.Line = i; l.A = (int)Doc.NumAt(i + 1); l.B = (int)Doc.NumAt(i + 2);
                Links.Add(l); i += 2;
            }
        }
    }
    public void SetPoint(PathPoint p, int c, double v) { p.P[c] = v; Doc.Set(p.Line + 1 + c, v); }
    public void AddPoint(double x, double y, double z) { Doc.Append("[pathpnt]", CfgDoc.Fmt(x), CfgDoc.Fmt(y), CfgDoc.Fmt(z)); Parse(); }
    public void AddLink(int a, int b) { Doc.Append("[pathlink]", a.ToString(), b.ToString()); Parse(); }
    public void RemoveLink(PathLink l) { Doc.RemoveRange(l.Line, 3); Parse(); }
}

// ---------------------------------------------------------------- passengercabin.cfg: 좌석, 입구/출구, 개표기
public class Seat { public bool Driver; public int Line; public double[] V = new double[5]; }   // x y z 높이 회전
public class CabinCfg {
    public CfgDoc Doc;
    public List<Seat> Seats = new List<Seat>();
    public List<int> Entries = new List<int>(), Exits = new List<int>();
    public static CabinCfg Load(string path) { CabinCfg c = new CabinCfg(); c.Doc = CfgDoc.Load(path); c.Parse(); return c; }
    public void Parse() {
        Seats.Clear(); Entries.Clear(); Exits.Clear();
        List<string> L = Doc.Lines;
        for (int i = 0; i < L.Count; i++) {
            string k = L[i].Trim().ToLowerInvariant();
            if ((k == "[passpos]" || k == "[drivpos]") && i + 5 < L.Count) {
                Seat s = new Seat(); s.Driver = k == "[drivpos]"; s.Line = i;
                for (int c = 0; c < 5; c++) s.V[c] = Doc.NumAt(i + 1 + c);
                Seats.Add(s); i += 5;
            } else if (k == "[entry]" && i + 1 < L.Count) Entries.Add((int)Doc.NumAt(i + 1));
            else if (k == "[exit]" && i + 1 < L.Count) Exits.Add((int)Doc.NumAt(i + 1));
        }
    }
    public void SetSeat(Seat s, int c, double v) { s.V[c] = v; Doc.Set(s.Line + 1 + c, v); }
    public void AddSeat(double x, double y, double z) { Doc.Append("[passpos]", CfgDoc.Fmt(x), CfgDoc.Fmt(y), CfgDoc.Fmt(z), "0.4", "0"); Parse(); }
    public void RemoveSeat(Seat s) { Doc.RemoveRange(s.Line, 6); Parse(); }
}
}
