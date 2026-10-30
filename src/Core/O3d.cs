using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Obj2x {

public class O3dMaterial {
    public float[] Diffuse = new float[] { 0.8f, 0.8f, 0.8f, 1 };
    public float[] Spec = new float[3], Emit = new float[3];
    public float Power;
    public string Tex = "";
}

// o3d 읽기. 버전 1(OmsiXConv/FastO3D), 3, 5, 7(Blender O3D 내보내기)을 읽는다. 암호화된 파일은 읽지 않는다.
// 좌표는 DirectX식 왼손 좌표계(x 오른쪽, y 위, z 앞)이고, 정점은 이미 모델 전체 기준 위치다 (끝의 변환 행렬은 적용하지 않음)
public class O3dModel {
    public int Version;
    public float[] V = new float[0];   // 정점마다 8개: 위치 3, 노멀 3, UV 2
    public int VCount;
    public int[] T = new int[0];       // 삼각형마다 4개: 정점 3, 재질
    public int TCount;
    public List<O3dMaterial> Mats = new List<O3dMaterial>();

    public static O3dModel Load(string path) {
        O3dModel m = new O3dModel();
        byte[] d = File.ReadAllBytes(path);
        if (d.Length < 4 || d[0] != 0x84 || d[1] != 0x19) throw new Exception(L.T("o3d_err_format"));
        m.Version = d[2];
        int p = 3, opt = 0;
        if (m.Version >= 3) opt = d[p++];
        if (m.Version >= 4) {
            uint key = BitConverter.ToUInt32(d, p); p += 4;
            if (key != 0xFFFFFFFF) throw new Exception(L.T("o3d_err_encrypted"));   // 0xFFFFFFFF만 평문 (공식 차량의 0 등은 암호화)
        }
        bool longCount = m.Version >= 3, longIdx = (opt & 1) != 0;
        Encoding enc = Encoding.Default;
        while (p < d.Length) {
            int sec = d[p++];
            if (sec == 0x17) {
                int n = longCount ? BitConverter.ToInt32(d, p) : BitConverter.ToUInt16(d, p); p += longCount ? 4 : 2;
                if (n < 0 || p + n * 32L > d.Length) throw new Exception(L.T("o3d_err_format"));
                m.VCount = n; m.V = new float[n * 8];
                Buffer.BlockCopy(d, p, m.V, 0, n * 32); p += n * 32;
            } else if (sec == 0x49) {
                int n = longCount ? BitConverter.ToInt32(d, p) : BitConverter.ToUInt16(d, p); p += longCount ? 4 : 2;
                int sz = (longIdx ? 12 : 6) + 2;
                if (n < 0 || p + (long)n * sz > d.Length) throw new Exception(L.T("o3d_err_format"));
                m.TCount = n; m.T = new int[n * 4];
                for (int i = 0; i < n; i++) {
                    if (longIdx) { m.T[i * 4] = BitConverter.ToInt32(d, p); m.T[i * 4 + 1] = BitConverter.ToInt32(d, p + 4); m.T[i * 4 + 2] = BitConverter.ToInt32(d, p + 8); p += 12; }
                    else { m.T[i * 4] = BitConverter.ToUInt16(d, p); m.T[i * 4 + 1] = BitConverter.ToUInt16(d, p + 2); m.T[i * 4 + 2] = BitConverter.ToUInt16(d, p + 4); p += 6; }
                    m.T[i * 4 + 3] = BitConverter.ToUInt16(d, p); p += 2;
                }
            } else if (sec == 0x26) {
                int n = BitConverter.ToUInt16(d, p); p += 2;
                for (int i = 0; i < n; i++) {
                    O3dMaterial mt = new O3dMaterial();
                    for (int k = 0; k < 4; k++) mt.Diffuse[k] = BitConverter.ToSingle(d, p + k * 4);
                    for (int k = 0; k < 3; k++) { mt.Spec[k] = BitConverter.ToSingle(d, p + 16 + k * 4); mt.Emit[k] = BitConverter.ToSingle(d, p + 28 + k * 4); }
                    mt.Power = BitConverter.ToSingle(d, p + 40); p += 44;
                    int len = d[p++];
                    mt.Tex = enc.GetString(d, p, len).Trim(); p += len;
                    m.Mats.Add(mt);
                }
            } else if (sec == 0x54) {   // 뼈대(가중치): 미리보기/내보내기에는 쓰지 않으니 건너뛴다
                int n = BitConverter.ToUInt16(d, p); p += 2;
                for (int i = 0; i < n; i++) {
                    int len = d[p++]; p += len;
                    int c = longCount ? BitConverter.ToInt32(d, p) : BitConverter.ToUInt16(d, p); p += longCount ? 4 : 2;
                    p += c * ((longIdx ? 4 : 2) + 4);
                }
            } else if (sec == 0x79) { p += 64; }
            else break;
        }
        // 잘못된 정점 번호가 있으면 버린다 (깨진 파일에서 화면이 죽지 않게)
        for (int i = 0; i < m.TCount; i++)
            for (int k = 0; k < 3; k++) if (m.T[i * 4 + k] < 0 || m.T[i * 4 + k] >= m.VCount) { m.T[i * 4] = m.T[i * 4 + 1] = m.T[i * 4 + 2] = 0; break; }
        return m;
    }
}

// ---------------------------------------------------------------- OMSI 모델 설정 (버스 model.cfg / 오브젝트 .sco)
public class CfgMesh {
    public string File = "", Path = "";
    public int ViewPoint;              // 0 항상, 1 밖, 2 안, 4 AI (합으로 묶음)
    public int Lod;                    // [LOD] 묶음 번호 (0 = [LOD] 앞)
    public double LodValue;
    public string VisVar, VisVal;      // [visible] 조건 (없으면 null)
    public Dictionary<string, int> Alpha = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // 텍스처 → [matl_alpha]
    public bool Exists { get { return System.IO.File.Exists(Path); } }
    public bool UsesAlpha(string tex) { int a; return Alpha.TryGetValue(tex ?? "", out a) && a > 0; }
    public bool Exterior { get { return ViewPoint == 0 || (ViewPoint & 1) != 0; } }
}

public class ModelCfg {
    public string CfgPath, BaseDir, TexDir;
    public List<CfgMesh> Meshes = new List<CfgMesh>();
    public int BestLod;                // 가장 자세한 LOD 묶음 (값이 가장 큰 것)
    public Dictionary<string, string> FirstVis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // [visible] 변수마다 처음 나온 값
    public Dictionary<string, List<string>> VisValues = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);   // [visible] 변수마다 나온 값들

    public static ModelCfg Load(string path) {
        ModelCfg c = new ModelCfg();
        c.CfgPath = path;
        string dir = System.IO.Path.GetDirectoryName(path);
        bool sco = path.EndsWith(".sco", StringComparison.OrdinalIgnoreCase);
        // 오브젝트(.sco)는 model\, texture\ 아래.
        // 버스는 차량 폴더(.bus/.ovh가 있는 곳)를 찾아 그 아래 Model, Texture를 쓴다. cfg가 Model\cfg\ 처럼 한 단계 더 들어가 있어도
        // [mesh] 경로는 Model 기준인 경우가 많아서, 부품마다 cfg 폴더 → 위 폴더들 → 차량\Model 순서로 실제 있는 곳을 찾는다
        c.VehicleDir = sco ? dir : VehicleRoot(dir);
        c.BaseDir = sco ? System.IO.Path.Combine(dir, "model") : dir;
        if (sco) c.Bases.Add(c.BaseDir);
        else {
            for (string d = dir; d != null; d = System.IO.Path.GetDirectoryName(d)) {
                c.Bases.Add(d);
                if (string.Equals(d.TrimEnd('\\'), c.VehicleDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) break;
            }
            string vm = System.IO.Path.Combine(c.VehicleDir, "Model");
            if (Directory.Exists(vm) && !c.Bases.Exists(delegate(string b) { return string.Equals(b.TrimEnd('\\'), vm, StringComparison.OrdinalIgnoreCase); })) c.Bases.Insert(1, vm);
        }
        c.TexDir = sco ? System.IO.Path.Combine(dir, "texture") : System.IO.Path.Combine(c.VehicleDir, "Texture");
        string[] lines = Omsi.AnsiLines(path);
        CfgMesh cur = null; string curTex = null;
        int lod = 0; double lodVal = 0, bestVal = double.MinValue;
        for (int i = 0; i < lines.Length; i++) {
            string k = lines[i].Trim();
            if (k.Length < 3 || k[0] != '[') continue;
            Func<int, string> arg = delegate(int n) { return i + n < lines.Length ? lines[i + n].Trim() : ""; };
            switch (k.ToLowerInvariant()) {
                case "[mesh]":
                    cur = new CfgMesh(); cur.File = arg(1); cur.Path = c.Resolve(cur.File);
                    cur.Lod = lod; cur.LodValue = lodVal; curTex = null;
                    c.Meshes.Add(cur); i++;
                    break;
                case "[viewpoint]":
                    if (cur != null) { int v; if (int.TryParse(arg(1), out v)) cur.ViewPoint = v; }
                    break;
                case "[visible]":
                    if (cur != null) {
                        cur.VisVar = arg(1); cur.VisVal = arg(2);
                        if (!c.FirstVis.ContainsKey(cur.VisVar)) c.FirstVis[cur.VisVar] = cur.VisVal;
                        List<string> vs; if (!c.VisValues.TryGetValue(cur.VisVar, out vs)) c.VisValues[cur.VisVar] = vs = new List<string>();
                        if (!vs.Contains(cur.VisVal)) vs.Add(cur.VisVal);
                    }
                    break;
                case "[lod]": {
                    double v; double.TryParse(arg(1).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                    lod++; lodVal = v; cur = null;
                    if (v > bestVal) { bestVal = v; c.BestLod = lod; }
                    break;
                }
                case "[matl]":
                    curTex = arg(1);
                    break;
                case "[matl_alpha]":
                    if (cur != null && curTex != null) { int v; if (int.TryParse(arg(1), out v)) cur.Alpha[curTex] = v; }
                    break;
                case "[ctctexture]":   // 도색이 바꾸는 텍스처 자리: 자리 이름 → 원래 텍스처 파일
                    if (arg(1).Length > 0 && arg(2).Length > 0) c.CtcSlots[arg(1)] = arg(2);
                    break;
                case "[ctc]": {   // 도색(리페인트) 폴더: 차량 폴더 기준. 기본 텍스처 대신 그 안의 '<이름>_<도색>' 파일을 쓴다
                    string d = System.IO.Path.Combine(c.VehicleDir, arg(2));
                    if (Directory.Exists(d)) c.CtcDir = d;
                    break;
                }
            }
        }
        return c;
    }

    public string CtcDir;              // [CTC] 도색 폴더 (없으면 null)
    public Dictionary<string, string> CtcSlots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // [CTCTexture] 자리 이름 → 원래 텍스처
    public Repaint Paint;              // 고른 도색 (null이면 기본 텍스처, 도색 파일이 없을 때는 이름 규칙으로 찾음)

    // 도색 폴더의 .cti 목록 (이름순)
    public List<Repaint> Repaints() {
        List<Repaint> r = new List<Repaint>();
        if (CtcDir == null) return r;
        string[] files;
        try { files = Directory.GetFiles(CtcDir, "*.cti"); } catch (Exception) { return r; }
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string f in files) r.AddRange(Repaint.LoadAll(f));
        return r;
    }
    public string VehicleDir;          // 차량 폴더 (.bus/.ovh가 있는 곳, 못 찾으면 cfg 폴더의 위)
    public List<string> Bases = new List<string>();   // [mesh] 경로를 찾아볼 기준 폴더들 (앞에서부터)

    // 부품 경로: 기준 폴더들 중 파일이 실제로 있는 첫 곳. 어디에도 없으면 첫 기준 폴더로 (목록에 '파일 없음'으로 보인다)
    public string Resolve(string file) {
        foreach (string b in Bases) { string p = System.IO.Path.Combine(b, file); if (File.Exists(p)) return p; }
        return System.IO.Path.Combine(Bases.Count > 0 ? Bases[0] : BaseDir, file);
    }

    // cfg 폴더부터 위로 네 단계까지 .bus/.ovh 파일이나 Texture 폴더가 있는 곳
    static string VehicleRoot(string dir) {
        string d = dir;
        for (int i = 0; i < 5 && d != null; i++, d = System.IO.Path.GetDirectoryName(d)) {
            try {
                if (Directory.GetFiles(d, "*.bus").Length > 0 || Directory.GetFiles(d, "*.ovh").Length > 0) return d;
            } catch (Exception) { }
        }
        d = dir;
        for (int i = 0; i < 5 && d != null; i++, d = System.IO.Path.GetDirectoryName(d))
            if (Directory.Exists(System.IO.Path.Combine(d, "Texture")) && i > 0) return d;
        return System.IO.Path.GetDirectoryName(dir) ?? dir;
    }
    string[] ctcFiles;

    public string FindTex(string tex, CfgMesh m) {
        if (string.IsNullOrEmpty(tex)) return null;
        string r = Paint != null ? Paint.TexFor(tex, CtcSlots) : (CtcHasCti ? null : RepaintByName(tex));
        return r ?? Omsi.FindTexture(tex, TexDir, System.IO.Path.GetDirectoryName(m.Path), BaseDir, System.IO.Path.GetDirectoryName(CfgPath), VehicleDir);
    }

    // 도색 폴더에서 '<텍스처 이름>_*' 중 이름순 첫 번째 (예: SD77_01.tga → Werbung_SD77\SD77_01_BFB.dds)
    string RepaintByName(string tex) {
        if (CtcDir == null) return null;
        if (ctcFiles == null) {
            try { ctcFiles = Directory.GetFiles(CtcDir); Array.Sort(ctcFiles, StringComparer.OrdinalIgnoreCase); } catch (Exception) { ctcFiles = new string[0]; }
        }
        string bn = System.IO.Path.GetFileNameWithoutExtension(tex.Replace('/', '\\')) + "_";
        foreach (string f in ctcFiles) {
            string n = System.IO.Path.GetFileName(f), e = System.IO.Path.GetExtension(f).ToLowerInvariant();
            if (n.StartsWith(bn, StringComparison.OrdinalIgnoreCase) && (e == ".dds" || e == ".tga" || e == ".bmp" || e == ".png" || e == ".jpg")) return f;
        }
        return null;
    }

    // 처음 보여 줄 부품: 가장 자세한 LOD(또는 LOD 앞)의 바깥에서 보이는 부품. 조건부([visible])는 변수마다 처음 나온 값 하나만 (바퀴 종류 등이 하나씩 보이게)
    public bool DefaultOn(CfgMesh m) {
        return (m.Lod == 0 || m.Lod == BestLod) && m.Exterior && VisOn(m);
    }
    // 조건부 부품의 변수 값: 고른 도색의 [setvar]가 있으면 그것.
    // 없으면 OMSI처럼 0으로 본다 (정하지 않은 변수는 0). 다만 값이 여러 개인데 0이 없으면(바퀴 종류처럼 스크립트가 고르는 것) 처음 나온 값
    public string VarValue(string var) {
        string v;
        // 1) 차량 스크립트가 (도색 값을 넣고 돌렸을 때) 정한 값 — 예: 도색의 연식(sely)으로 앞문 모양(6es89/6es01)을 고름
        OmsiScript sc = Script();
        if (sc != null && sc.Assigned.Contains(var)) return sc.Vars[var].ToString(CultureInfo.InvariantCulture);
        // 2) 도색의 [setvar]
        if (Paint != null && Paint.Vars.TryGetValue(var, out v)) return v;
        List<string> vs;
        if (!VisValues.TryGetValue(var, out vs) || vs.Count == 0) return "0";
        if (vs.Contains("0") || vs.Count == 1) return "0";
        return vs[0];
    }
    public bool VisOn(CfgMesh m) {
        if (m.VisVar == null) return true;
        string v = VarValue(m.VisVar);
        double a, b;
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a) && double.TryParse(m.VisVal, NumberStyles.Float, CultureInfo.InvariantCulture, out b)) return a == b;
        return v == m.VisVal;
    }

    // 차량 스크립트 (버스만, 처음 쓸 때 읽음). 도색이 바뀌면 다시 돌린다
    OmsiScript script; bool scriptLoaded, scriptRan; Repaint scriptPaint;
    public bool HasScript { get { return Script() != null; } }
    OmsiScript Script() {
        if (!scriptLoaded) {
            scriptLoaded = true;
            if (!CfgPath.EndsWith(".sco", StringComparison.OrdinalIgnoreCase) && VehicleDir != null) script = OmsiScript.ForModel(VehicleDir, CfgPath);
        }
        if (script != null && (!scriptRan || scriptPaint != Paint)) { script.Run(Paint != null ? Paint.Vars : null); scriptRan = true; scriptPaint = Paint; }
        return script;
    }
    bool CtcHasCti { get { if (ctcHasCti == null) ctcHasCti = Repaints().Count > 0; return ctcHasCti.Value; } }
    bool? ctcHasCti;
}

// ---------------------------------------------------------------- 도색 파일(.cti): [item] 도색 이름/텍스처 자리/파일, [setvar] 변수/값
public class Repaint {
    public string File, Name, Id, Label;   // Id = 파일|이름 (한 파일에 도색이 여러 개일 수 있다), Label = 목록에 보일 이름
    public Dictionary<string, string> Items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // 자리 이름 → 파일 전체 경로
    public Dictionary<string, string> Vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // 한 .cti에 도색이 여러 개 들어 있을 수 있다: [item]의 첫 줄(도색 이름)이 바뀌면 새 도색.
    // [setvar]는 바로 앞 도색의 것, 첫 [item]보다 앞에 있으면 파일 안 모든 도색에 쓴다.
    // OMSI는 cti를 ANSI로 읽는다 (UTF-8 BOM이 붙어 있어도). 그래서 파일 이름은 ANSI로 읽고, 그 파일이 없으면 UTF-8로도 찾아본다
    public static List<Repaint> LoadAll(string path) {
        List<Repaint> list = new List<Repaint>();
        string[] lines, utf;
        try { lines = Omsi.AnsiLines(path); utf = Omsi.Utf8Lines(path); } catch (Exception) { return list; }
        string dir = System.IO.Path.GetDirectoryName(path), stem = System.IO.Path.GetFileNameWithoutExtension(path);
        Dictionary<string, string> fileVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Repaint cur = null;
        for (int i = 0; i < lines.Length; i++) {
            string k = lines[i].Trim().ToLowerInvariant();
            string a1 = i + 1 < lines.Length ? lines[i + 1].Trim() : "", a2 = i + 2 < lines.Length ? lines[i + 2].Trim() : "", a3 = i + 3 < lines.Length ? lines[i + 3].Trim() : "";
            if (k == "[item]" && a2.Length > 0 && a3.Length > 0) {
                string name = a1.Length > 0 ? a1 : stem;
                cur = list.Find(delegate(Repaint x) { return x.Name == name; });
                if (cur == null) { cur = new Repaint(); cur.File = path; cur.Name = name; cur.Id = path + "|" + name; list.Add(cur); }
                string f = System.IO.Path.Combine(dir, a3);
                if (!System.IO.File.Exists(f) && i + 3 < utf.Length) { string g = System.IO.Path.Combine(dir, utf[i + 3].Trim()); if (System.IO.File.Exists(g)) f = g; }
                cur.Items[a2] = f;
            } else if (k == "[setvar]" && a1.Length > 0) {
                if (cur != null) cur.Vars[a1] = a2; else fileVars[a1] = a2;
            }
        }
        // [item] 없이 [setvar]만 있는 파일도 도색 하나 (파일 이름)
        if (list.Count == 0 && fileVars.Count > 0) { Repaint only = new Repaint(); only.File = path; only.Name = stem; only.Id = path + "|" + stem; list.Add(only); }
        foreach (Repaint r in list) {
            foreach (KeyValuePair<string, string> kv in fileVars) if (!r.Vars.ContainsKey(kv.Key)) r.Vars[kv.Key] = kv.Value;
            r.Label = list.Count > 1 ? stem + " · " + r.Name : r.Name;
        }
        return list;
    }

    // 텍스처 이름이 [CTCTexture]의 원래 텍스처이고 이 도색에 그 자리가 있으면 도색 파일
    public string TexFor(string tex, Dictionary<string, string> slots) {
        string bn = System.IO.Path.GetFileName(tex.Replace('/', '\\'));
        foreach (KeyValuePair<string, string> kv in slots) {
            if (!string.Equals(System.IO.Path.GetFileName(kv.Value.Replace('/', '\\')), bn, StringComparison.OrdinalIgnoreCase)) continue;
            string f;
            if (Items.TryGetValue(kv.Key, out f) && System.IO.File.Exists(f)) return f;
        }
        return null;
    }
    public override string ToString() { return Label ?? Name; }
}

// ---------------------------------------------------------------- obj + mtl로 내보내기 (Blender에서 원래 방향 그대로 열리게: FastO3D의 반대)
public static class ObjExport {
    // 돌려주는 값: 못 찾은 텍스처 목록. errors에는 읽지 못한 메시
    public static List<string> Write(ModelCfg cfg, List<CfgMesh> meshes, string objPath, List<string> errors) {
        List<string> missing = new List<string>();
        string mtlPath = Path.ChangeExtension(objPath, ".mtl");
        CultureInfo ci = CultureInfo.InvariantCulture;
        HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (StreamWriter ow = new StreamWriter(objPath, false, new UTF8Encoding(false)))
        using (StreamWriter mw = new StreamWriter(mtlPath, false, new UTF8Encoding(false))) {
            ow.WriteLine("# " + AppInfo.Name + " " + AppInfo.Version + " - " + Path.GetFileName(cfg.CfgPath));
            ow.WriteLine("mtllib " + Path.GetFileName(mtlPath));
            int baseV = 1;
            foreach (CfgMesh cm in meshes) {
                O3dModel m;
                try { m = O3dModel.Load(cm.Path); } catch (Exception ex) { errors.Add(cm.File + ": " + ex.Message); continue; }
                string name = Uniq(Path.GetFileNameWithoutExtension(cm.File), usedNames);
                ow.WriteLine("o " + name);
                for (int i = 0; i < m.VCount; i++) {
                    int o = i * 8;
                    ow.WriteLine(string.Format(ci, "v {0:0.######} {1:0.######} {2:0.######}", m.V[o], m.V[o + 1], -m.V[o + 2]));
                }
                for (int i = 0; i < m.VCount; i++) { int o = i * 8; ow.WriteLine(string.Format(ci, "vt {0:0.######} {1:0.######}", m.V[o + 6], 1 - m.V[o + 7])); }
                for (int i = 0; i < m.VCount; i++) { int o = i * 8; ow.WriteLine(string.Format(ci, "vn {0:0.####} {1:0.####} {2:0.####}", m.V[o + 3], m.V[o + 4], -m.V[o + 5])); }
                // 재질마다 묶어서 면을 쓴다
                for (int mi = 0; mi < Math.Max(1, m.Mats.Count); mi++) {
                    bool any = false;
                    for (int t = 0; t < m.TCount; t++) {
                        if (m.T[t * 4 + 3] != mi && !(m.Mats.Count == 0)) continue;
                        if (m.T[t * 4] == m.T[t * 4 + 1] && m.T[t * 4] == m.T[t * 4 + 2]) continue;
                        if (!any) {
                            any = true;
                            string mn = name + "_" + mi;
                            ow.WriteLine("usemtl " + mn);
                            WriteMtl(mw, mn, mi < m.Mats.Count ? m.Mats[mi] : new O3dMaterial(), cfg, cm, missing);
                        }
                        int a = m.T[t * 4] + baseV, b = m.T[t * 4 + 1] + baseV, c = m.T[t * 4 + 2] + baseV;
                        // z를 뒤집었으니 면 방향도 뒤집는다
                        ow.WriteLine("f " + a + "/" + a + "/" + a + " " + c + "/" + c + "/" + c + " " + b + "/" + b + "/" + b);
                    }
                }
                baseV += m.VCount;
            }
        }
        return missing;
    }

    static string Uniq(string n, HashSet<string> used) {
        string r = n; int k = 2;
        while (!used.Add(r)) r = n + "." + (k++);
        return r;
    }

    static void WriteMtl(StreamWriter mw, string name, O3dMaterial mt, ModelCfg cfg, CfgMesh cm, List<string> missing) {
        CultureInfo ci = CultureInfo.InvariantCulture;
        mw.WriteLine("newmtl " + name);
        mw.WriteLine(string.Format(ci, "Kd {0:0.###} {1:0.###} {2:0.###}", mt.Diffuse[0], mt.Diffuse[1], mt.Diffuse[2]));
        mw.WriteLine(string.Format(ci, "Ks {0:0.###} {1:0.###} {2:0.###}", mt.Spec[0], mt.Spec[1], mt.Spec[2]));
        mw.WriteLine(string.Format(ci, "Ke {0:0.###} {1:0.###} {2:0.###}", mt.Emit[0], mt.Emit[1], mt.Emit[2]));
        mw.WriteLine(string.Format(ci, "Ns {0:0.###}", Math.Min(1000, mt.Power)));
        if (mt.Tex.Length > 0) {
            string f = cfg.FindTex(mt.Tex, cm);
            if (f == null) { if (!missing.Contains(mt.Tex)) missing.Add(mt.Tex); }
            else {
                mw.WriteLine("map_Kd " + f);
                // OMSI는 [matl_alpha]가 있을 때만 알파 채널을 투명으로 쓴다 (없으면 반사 마스크 등 다른 용도)
                if (cm.UsesAlpha(mt.Tex) && Omsi.HasAlpha(f)) mw.WriteLine("map_d " + f);
            }
        }
        mw.WriteLine();
    }
}
}
