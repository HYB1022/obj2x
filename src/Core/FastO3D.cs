using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

namespace Obj2x {
public static class FastO3D {
    class Mat { public float[] Kd = new float[] { 0.8f, 0.8f, 0.8f }; public float[] Ks = new float[] { 0.5f, 0.5f, 0.5f }; public float[] Ke = new float[3]; public float Ns = 1000f; public bool AlphaMap; public string Tex = ""; }
    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
    static double D(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }
    static string BaseName(string p) { p = p.Replace('\\', '/'); int i = p.LastIndexOf('/'); return i >= 0 ? p.Substring(i + 1) : p; }
    static void Triple(string[] w, float[] dst) { for (int i = 0; i < 3 && i + 1 < w.Length; i++) dst[i] = F(w[i + 1]); }

    // obj(+mtl) -> o3d. 결과 요약 문자열을 돌려주고, 실패하면 예외를 던진다.
    public static string Convert(string objPath, string o3dPath) {
        int v, t, m; string w;
        ConvertEx(objPath, o3dPath, out v, out t, out m, out w);
        string res = "정점 " + v + ", 삼각형 " + t + ", 재질 " + m;
        if (w.Length > 0) res += "  [주의: " + w + "]";
        return res;
    }

    public static void ConvertEx(string objPath, string o3dPath, out int nVerts, out int nTris, out int nMats, out string warn) {
        string dir = Path.GetDirectoryName(objPath);
        string[] lines = File.ReadAllLines(objPath, Encoding.GetEncoding(28591));
        string mtlName = null;
        foreach (string ln in lines) { if (ln.StartsWith("mtllib ")) { mtlName = ln.Substring(7).Trim(); break; } }
        string mtlPath = mtlName != null ? Path.Combine(dir, mtlName) : null;
        if (mtlPath == null || !File.Exists(mtlPath)) mtlPath = Path.ChangeExtension(objPath, ".mtl");
        List<string> order = new List<string>(); Dictionary<string, Mat> mats = new Dictionary<string, Mat>();
        if (File.Exists(mtlPath)) {
            Mat cur = null;
            foreach (string ln0 in File.ReadAllLines(mtlPath, Encoding.GetEncoding(28591))) {
                string ln = ln0.Trim(); if (ln.Length == 0) continue;
                string[] w = ln.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (w[0] == "newmtl" && w.Length > 1) { cur = new Mat(); string nm = ln.Substring(6).Trim(); if (!mats.ContainsKey(nm)) { mats[nm] = cur; order.Add(nm); } else cur = mats[nm]; }
                else if (cur == null) continue;
                else if (w[0] == "Kd") { Triple(w, cur.Kd); }
                else if (w[0] == "Ks") { Triple(w, cur.Ks); }
                else if (w[0] == "Ke") { Triple(w, cur.Ke); }
                else if (w[0] == "Ns" && w.Length > 1) cur.Ns = F(w[1]);
                else if (w[0] == "map_d") cur.AlphaMap = true;
                else if (w[0] == "map_Kd") { int i = ln.IndexOf(' '); if (i > 0) cur.Tex = BaseName(ln.Substring(i + 1).Trim()); }
            }
        }
        List<double[]> P = new List<double[]>(), N = new List<double[]>(), T = new List<double[]>();
        Dictionary<string, int> vmap = new Dictionary<string, int>();
        List<float[]> verts = new List<float[]>();
        List<int[]> tris = new List<int[]>();
        int curMat = 0; int objCount = 0; List<string> warns = new List<string>();
        if (!File.Exists(mtlPath)) warns.Add("mtl 파일 없음");
        char[] sp = new char[] { ' ', '\t' };
        foreach (string ln in lines) {
            if (ln.Length < 2) continue;
            if (ln[0] == 'v' && ln[1] == ' ') { string[] w = ln.Split(sp, StringSplitOptions.RemoveEmptyEntries); P.Add(new double[] { D(w[1]), D(w[2]), D(w[3]) }); }
            else if (ln[0] == 'v' && ln[1] == 't') { string[] w = ln.Split(sp, StringSplitOptions.RemoveEmptyEntries); T.Add(new double[] { D(w[1]), D(w[2]) }); }
            else if (ln[0] == 'v' && ln[1] == 'n') { string[] w = ln.Split(sp, StringSplitOptions.RemoveEmptyEntries); N.Add(new double[] { D(w[1]), D(w[2]), D(w[3]) }); }
            else if (ln[0] == 'o' && ln[1] == ' ') objCount++;
            else if (ln.StartsWith("usemtl ")) {
                string nm = ln.Substring(7).Trim();
                int k = order.IndexOf(nm);
                if (k < 0) { order.Add(nm); mats[nm] = new Mat(); k = order.Count - 1; warns.Add("재질 '" + nm + "'이 mtl에 없음"); }
                curMat = k;
            }
            else if (ln[0] == 'f' && ln[1] == ' ') {
                string[] w = ln.Split(sp, StringSplitOptions.RemoveEmptyEntries);
                int n = w.Length - 1; if (n < 3) continue;
                int[] ids = new int[n];
                for (int c = 0; c < n; c++) {
                    string[] a = w[n - c].Split('/');   // 면의 꼭짓점 순서를 뒤집는다
                    int pi = int.Parse(a[0]); pi = pi > 0 ? pi - 1 : P.Count + pi;
                    double[] p = P[pi]; double[] t = new double[] { 0, 0 }; double[] nn = new double[] { 0, 0, 1 };
                    if (a.Length > 1 && a[1].Length > 0) { int ti = int.Parse(a[1]); ti = ti > 0 ? ti - 1 : T.Count + ti; t = T[ti]; }
                    if (a.Length > 2 && a[2].Length > 0) { int ni = int.Parse(a[2]); ni = ni > 0 ? ni - 1 : N.Count + ni; nn = N[ni]; }
                    string key = string.Format(CultureInfo.InvariantCulture, "{0:R} {1:R} {2:R} {3:R} {4:R} {5:R} {6:R} {7:R}", p[0], p[1], p[2], nn[0], nn[1], nn[2], t[0], t[1]);
                    int id;
                    if (!vmap.TryGetValue(key, out id)) {
                        id = verts.Count; vmap[key] = id;
                        double l = Math.Sqrt(nn[0] * nn[0] + nn[1] * nn[1] + nn[2] * nn[2]); if (l == 0) l = 1;
                        verts.Add(new float[] { (float)p[0], (float)p[1], (float)(-p[2]), (float)(nn[0] / l), (float)(nn[1] / l), (float)(-nn[2] / l), (float)t[0], (float)(1.0 - t[1]) });
                    }
                    ids[c] = id;
                }
                for (int i = 1; i < n - 1; i++) tris.Add(new int[] { ids[0], ids[i], ids[i + 1], curMat });
            }
        }
        if (tris.Count == 0) throw new Exception("면(f)이 없음");
        if (verts.Count > 65535) throw new Exception("정점 " + verts.Count + "개: 65535개를 넘어 지원하지 않음");
        if (tris.Count > 65535) throw new Exception("삼각형 " + tris.Count + "개: 65535개를 넘어 지원하지 않음");
        if (order.Count == 0) { order.Add("default"); mats["default"] = new Mat(); }
        using (BinaryWriter bw = new BinaryWriter(new FileStream(o3dPath, FileMode.Create, FileAccess.Write))) {
            bw.Write(new byte[] { 0x84, 0x19, 0x01, 0x17 });
            bw.Write((ushort)verts.Count);
            foreach (float[] v in verts) foreach (float f in v) bw.Write(f);
            bw.Write((byte)'I'); bw.Write((ushort)tris.Count);
            foreach (int[] t in tris) { bw.Write((ushort)t[0]); bw.Write((ushort)t[1]); bw.Write((ushort)t[2]); bw.Write((ushort)t[3]); }
            bw.Write((byte)0x26); bw.Write((ushort)order.Count);
            foreach (string nm in order) {
                Mat m = mats[nm];
                bw.Write((float)(m.Kd[0] * 0.8)); bw.Write((float)(m.Kd[1] * 0.8)); bw.Write((float)(m.Kd[2] * 0.8)); bw.Write(m.AlphaMap ? 0f : 1f);
                foreach (float f in m.Ks) bw.Write(f);
                foreach (float f in m.Ke) bw.Write(f);
                bw.Write(m.Ns);
                byte[] tx = Encoding.GetEncoding(28591).GetBytes(m.Tex); bw.Write((byte)tx.Length); bw.Write(tx);
            }
            bw.Write((byte)0x79);
            float[] mtx = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1 };
            foreach (float f in mtx) bw.Write(f);
        }
        if (objCount > 1) warns.Add("오브젝트 " + objCount + "개가 하나로 합쳐짐");
        nVerts = verts.Count; nTris = tris.Count; nMats = order.Count; warn = string.Join("; ", warns.ToArray());
    }
}
}
