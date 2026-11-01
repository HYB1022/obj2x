using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows.Input;
using Avalonia.Media;

namespace Obj2x.UI {

// 오브젝트 탭의 '.sco 열어 보기'와 스플라인 탭의 '.sli 열어 보기'
public partial class MainViewModel {
    // ------------------------------------------------------------ .sco: 버스 탭과 같은 방법으로 부품을 읽어 3D로
    public ObservableCollection<PartItem> ScoParts { get; private set; }
    public ICommand OpenScoCmd { get; private set; }
    public ICommand ScoToBusCmd { get; private set; }
    string scoPath = "", scoStatus = "";
    int scoScene, scoGen;
    bool scoBusy;

    // ------------------------------------------------------------ .sli: 40 m 직선 도로로 만들어 3D로, 차/사람 길은 덧그림
    public ObservableCollection<PartItem> SliParts { get; private set; }
    public ICommand OpenSliCmd { get; private set; }
    string sliPath = "", sliInfo = "";
    int sliScene;
    Model3D sliOverlay;
    public const double SliLength = 40;

    void InitPreview() {
        ScoParts = new ObservableCollection<PartItem>(); SliParts = new ObservableCollection<PartItem>();
        OpenScoCmd = new Cmd(delegate {
            string start = scoPath.Length > 0 ? scoPath : (OmsiOk ? Path.Combine(Path.Combine(S.OmsiDir, "Sceneryobjects"), "x") : "");
            ui.PickFile(L.T("obj_view_file"), "OMSI (*.sco)|*.sco", start, delegate(string f) { if (f != null) LoadSco(f); });
        });
        ScoToBusCmd = new Cmd(delegate { if (scoPath.Length == 0) return; Page = "bus"; LoadModel(scoPath); }, delegate(object p) { return scoPath.Length > 0 && !scoBusy; });
        OpenSliCmd = new Cmd(delegate {
            string start = sliPath.Length > 0 ? sliPath : (OmsiOk ? Path.Combine(Path.Combine(S.OmsiDir, "Splines"), "x") : "");
            ui.PickFile(L.T("spl_view_file"), "OMSI (*.sli)|*.sli", start, delegate(string f) { if (f != null) LoadSli(f); });
        });
    }

    public string ScoPathText { get { return scoPath.Length > 0 ? scoPath : L.T("view_none"); } }
    public int ScoScene { get { return scoScene; } }
    public bool ScoEmpty { get { return ScoParts.Count == 0 && !scoBusy; } }
    public bool ScoBusy { get { return scoBusy; } }
    public string ScoStatus { get { return scoStatus; } }

    public void LoadSco(string path) {
        ModelCfg cfg;
        try { cfg = ModelCfg.Load(path); } catch (Exception ex) { ui.Msg(ex.Message); return; }
        if (cfg.Meshes.Count == 0) { ui.Msg(L.T("view_err_nomesh")); return; }
        List<Repaint> paints = cfg.Repaints();
        cfg.Paint = paints.Count > 0 ? paints[0] : null;
        int gen = ++scoGen;
        scoPath = path; scoBusy = true; ScoParts.Clear(); scoScene++;
        scoStatus = L.T("view_loading", 0, cfg.Meshes.Count);
        Raise("ScoPathText", "ScoScene", "ScoEmpty", "ScoBusy", "ScoStatus");
        Cmd.Requery();
        Thread th = new Thread(delegate() {
            List<string> missing = new List<string>();
            Dictionary<string, Material> cache = new Dictionary<string, Material>();
            PartItem[] arr = new PartItem[cfg.Meshes.Count];   // 여러 코어로 함께 읽는다 (버스 탭과 같이)
            System.Threading.Tasks.Parallel.For(0, arr.Length, (i, st) => {
                if (gen != scoGen) { st.Stop(); return; }
                arr[i] = ModelScene.Build(cfg, cfg.Meshes[i], cache, missing, false);
            });
            if (gen != scoGen) return;
            List<PartItem> items = new List<PartItem>(arr);
            for (int i = 0; i < items.Count; i++) items[i].Checked = items[i].Model != null && cfg.DefaultOn(cfg.Meshes[i]);
            ModelScene.LoadTextures(cache.Values, delegate { return gen != scoGen; });
            ui.Post(delegate {
                if (gen != scoGen) return;
                scoBusy = false;
                int on = 0, tris = 0, loaded = 0, encrypted = 0;
                foreach (PartItem it in items) { ScoParts.Add(it); if (it.Model != null) loaded++; else if (it.Sub == L.T("o3d_err_encrypted")) encrypted++; if (it.Checked) { on++; tris += it.Tris; } }
                scoStatus = L.T("view_stat", on, items.Count, tris.ToString("N0")) + (missing.Count > 0 ? L.T("view_stat_miss", missing.Count) : "");
                scoScene++;
                Raise("ScoScene", "ScoEmpty", "ScoBusy", "ScoStatus");
                Cmd.Requery();
                if (loaded == 0) ui.Msg(encrypted > 0 ? L.T("view_err_encrypted_all") : L.T("view_err_noparts", items.Count, cfg.VehicleDir));
            });
        });
        th.IsBackground = true; th.Start();
    }

    public string SliPathText { get { return sliPath.Length > 0 ? sliPath : L.T("view_none"); } }
    public int SliScene { get { return sliScene; } }
    public bool SliEmpty { get { return SliParts.Count == 0; } }
    public string SliInfo { get { return sliInfo; } }
    public Model3D SliOverlay { get { return sliOverlay; } }

    public void LoadSli(string path) {
        SliFile f;
        try { f = SliFile.Load(path); } catch (Exception ex) { ui.Msg(ex.Message); return; }
        if (f.Profiles.Count == 0 && f.Paths.Count == 0) { ui.Msg(L.T("spl_view_err")); return; }
        int missing;
        PartItem it = SliModel(f, out missing);
        sliPath = path; SliParts.Clear(); SliParts.Add(it); sliScene++;
        sliOverlay = SliPaths(f);
        int car = 0, walk = 0; foreach (SliFile.SliPath p in f.Paths) if (p.Kind == 0) car++; else walk++;
        sliInfo = L.T("spl_view_info", f.Width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), f.Profiles.Count, f.Textures.Count, car, walk)
            + (missing > 0 ? L.T("spl_view_miss", missing) : "");
        Raise("SliPathText", "SliScene", "SliEmpty", "SliInfo", "SliOverlay");
    }

    // 단면 점들을 길이 방향으로 늘인 띠. sli의 x(오른쪽), y(위), 길이(앞) → 화면 (x, y, -길이). OMSI처럼 앞면만 (왼쪽→오른쪽 점이 위를 본다)
    static PartItem SliModel(SliFile f, out int missing) {
        missing = 0;
        PartItem it = new PartItem(); it.Name = Path.GetFileName(f.Path); it.Checked = true;
        Model3DGroup g = new Model3DGroup();
        Dictionary<int, Material> mats = new Dictionary<int, Material>();
        HashSet<int> miss = new HashSet<int>();
        foreach (SliFile.Profile pr in f.Profiles) {
            if (pr.Pts.Count < 2) continue;
            Material mat;
            if (!mats.TryGetValue(pr.Tex, out mat)) { mat = SliMaterial(f, pr.Tex, miss); mats[pr.Tex] = mat; }
            List<float> pos = new List<float>(), nor = new List<float>(), uv = new List<float>(); List<int> idx = new List<int>();
            for (int i = 0; i + 1 < pr.Pts.Count; i++) {
                SliFile.Pnt a = pr.Pts[i], b = pr.Pts[i + 1];
                int n = pos.Count / 3;
                // 면 노멀: 길이 방향(-z) × 단면 방향(a→b)
                Vector3D nv = Vector3D.CrossProduct(new Vector3D(b.X - a.X, b.Y - a.Y, 0), new Vector3D(0, 0, -1)); nv.Normalize();
                double[][] q = { new double[] { a.X, a.Y, 0, a.U, 0 }, new double[] { b.X, b.Y, 0, b.U, 0 }, new double[] { b.X, b.Y, -SliLength, b.U, b.V * SliLength }, new double[] { a.X, a.Y, -SliLength, a.U, a.V * SliLength } };
                foreach (double[] v in q) { pos.Add((float)v[0]); pos.Add((float)v[1]); pos.Add((float)v[2]); nor.Add((float)nv.X); nor.Add((float)nv.Y); nor.Add((float)nv.Z); uv.Add((float)v[3]); uv.Add((float)v[4]); }
                foreach (int k in new int[] { 0, 1, 2, 0, 2, 3 }) idx.Add(n + k);
                it.Tris += 2;
            }
            Mesh3D geo = new Mesh3D { Pos = pos.ToArray(), Nor = nor.ToArray(), Uv = uv.ToArray(), Idx = idx.ToArray() };
            geo.ComputeBounds();
            g.Children.Add(new GeometryModel3D(geo, mat));
        }
        it.Model = g; it.Bounds = g.Bounds;
        if (it.Bounds.IsEmpty) it.Bounds = new Rect3D(f.MinX, 0, -SliLength, Math.Max(1, f.Width), 0.1, SliLength);
        missing = miss.Count;
        return it;
    }

    static Material SliMaterial(SliFile f, int tex, HashSet<int> miss) {
        string file = f.FindTex(tex);
        Material m = new Material { Color = Color.FromRgb(150, 150, 155) };
        if (file != null) {
            try {
                int alpha; f.Alpha.TryGetValue(f.Textures[tex], out alpha);
                m.Texture = TexLoad.Load(file, 1024);
                m.Alpha = alpha != 0;
            } catch (Exception) { }
        } else if (tex >= 0 && tex < f.Textures.Count) miss.Add(tex);
        return m;
    }

    // 차 길: 정방향 초록, 역방향 주황 (화살표가 가는 쪽), 양방향/사람 길: 파랑
    static readonly Color SliFwd = Color.FromRgb(52, 199, 89), SliBack = Color.FromRgb(255, 149, 0), SliWalk = Color.FromRgb(10, 132, 255);
    static Model3D SliPaths(SliFile f) {
        Overlay3D o = new Overlay3D();
        foreach (SliFile.SliPath p in f.Paths) {
            Color c = p.Kind == 0 && p.Dir == 0 ? SliFwd : p.Kind == 0 && p.Dir == 1 ? SliBack : SliWalk;
            double y = p.Y + 0.08;
            o.Rod(new Point3D(p.X, y, 0), new Point3D(p.X, y, -SliLength), 0.08, c);
            if (p.Dir != 0 && p.Dir != 1) continue;
            double s = p.Dir == 0 ? -1 : 1, w = Math.Min(0.8, Math.Max(0.3, p.W * 0.2));
            for (double z = 4; z < SliLength; z += 8) {
                Point3D tip = new Point3D(p.X, y, -z + s * 0.6), back = new Point3D(p.X, y, -z - s * 0.6);
                o.Rod(new Point3D(p.X - w, y, back.Z), tip, 0.08, c);
                o.Rod(new Point3D(p.X + w, y, back.Z), tip, 0.08, c);
            }
        }
        return o.Group;
    }
}
}
