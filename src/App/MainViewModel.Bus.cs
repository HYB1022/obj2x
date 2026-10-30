using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Obj2x.UI {

// ---------------------------------------------------------------- 버스 탭의 줄들 (값은 글자로 주고받아 입력 중인 글자를 지우지 않는다)
public class VarItem : Observable {
    readonly Action changed;
    double val, min, max; bool playing;
    public string Name { get; set; }
    public string Info { get; set; }
    public ICommand PlayCmd { get; set; }
    public VarItem(Action changed) { this.changed = changed; }
    public double Min { get { return min; } set { min = value; Raise("Min"); } }
    public double Max { get { return max; } set { max = value; Raise("Max"); } }
    public double Value { get { return val; } set { if (val == value) return; val = value; Raise("Value", "ValueText"); changed(); } }
    public string ValueText { get { return CfgDoc.Fmt(Math.Round(val, 4)); } set { double v; if (CfgDoc.Num(value, out v)) Value = v; } }
    public bool Playing { get { return playing; } set { playing = value; Raise("Playing", "PlayText"); } }
    public string PlayText { get { return L.T(playing ? "bus_stop" : "bus_play"); } }
}

public class NumEdit : Observable {
    readonly Func<double> get; readonly Action<double> set;
    public string Label { get; set; }
    public bool Enabled { get; set; }
    public NumEdit(string label, Func<double> get, Action<double> set, bool enabled) { Label = label; this.get = get; this.set = set; Enabled = enabled; }
    string text;
    public string Text {
        get { return text ?? CfgDoc.Fmt(get()); }
        set { text = value; double v; if (Enabled && CfgDoc.Num(value, out v)) set(v); }
    }
}

public class BlockEdit {
    public string Title { get; set; }
    public List<NumEdit> Pivot { get; set; }
    public List<NumEdit> Rot { get; set; }
    public List<NumEdit> Factors { get; set; }
    public string Note { get; set; }
}

public class RowEdit {
    public string Title { get; set; }
    public List<NumEdit> Nums { get; set; }
    public ICommand RemoveCmd { get; set; }
    public bool CanRemove { get { return RemoveCmd != null; } }
}

public partial class MainViewModel {
    BusFile busFile; PathsCfg busPaths; CabinCfg busCabin;
    public ObservableCollection<VarItem> Vars { get; private set; }
    public ObservableCollection<BlockEdit> Blocks { get; private set; }
    public ObservableCollection<RowEdit> PathRows { get; private set; }
    public ObservableCollection<RowEdit> LinkRows { get; private set; }
    public ObservableCollection<RowEdit> SeatRows { get; private set; }
    public ICommand PickPartCmd { get; private set; }
    public ICommand SaveCfgCmd { get; private set; }
    public ICommand ResetVarsCmd { get; private set; }
    public ICommand AddPointCmd { get; private set; }
    public ICommand AddLinkCmd { get; private set; }
    public ICommand SavePathsCmd { get; private set; }
    public ICommand AddSeatCmd { get; private set; }
    public ICommand SaveCabinCmd { get; private set; }
    PartItem selPart;
    Model3D busOverlay;
    bool showPaths = true, showSeats = true, showPivots = true;

    void InitBus() {
        Vars = new ObservableCollection<VarItem>(); Blocks = new ObservableCollection<BlockEdit>();
        PathRows = new ObservableCollection<RowEdit>(); LinkRows = new ObservableCollection<RowEdit>(); SeatRows = new ObservableCollection<RowEdit>();
        PickPartCmd = new Cmd(delegate(object p) { SelectPart(p as PartItem); });
        SaveCfgCmd = new Cmd(delegate { SaveDoc(viewCfg != null ? viewCfg.Doc : null); }, delegate(object p) { return viewCfg != null; });
        ResetVarsCmd = new Cmd(delegate { foreach (VarItem v in Vars) { v.Playing = false; v.Value = 0; } });
        AddPointCmd = new Cmd(delegate {
            if (busPaths == null) return;
            double[] l = busPaths.Points.Count > 0 ? busPaths.Points[busPaths.Points.Count - 1].P : new double[3];
            busPaths.AddPoint(l[0], l[1] - 0.5, l[2]); PathsChanged();
        }, delegate(object p) { return busPaths != null; });
        AddLinkCmd = new Cmd(delegate {
            if (busPaths == null) return;
            int a = NewLinkA, b = NewLinkB;
            if (a == b || a < 0 || b < 0 || a >= busPaths.Points.Count || b >= busPaths.Points.Count) { ui.Msg(L.T("bus_err_link", busPaths.Points.Count - 1)); return; }
            busPaths.AddLink(a, b); PathsChanged();
        }, delegate(object p) { return busPaths != null; });
        SavePathsCmd = new Cmd(delegate { SaveDoc(busPaths != null ? busPaths.Doc : null); }, delegate(object p) { return busPaths != null; });
        AddSeatCmd = new Cmd(delegate {
            if (busCabin == null) return;
            double[] l = busCabin.Seats.Count > 0 ? busCabin.Seats[busCabin.Seats.Count - 1].V : new double[5];
            busCabin.AddSeat(l[0], l[1] - 0.8, l[2]); CabinChanged();
        }, delegate(object p) { return busCabin != null; });
        SaveCabinCmd = new Cmd(delegate { SaveDoc(busCabin != null ? busCabin.Doc : null); }, delegate(object p) { return busCabin != null; });
    }

    void SaveDoc(CfgDoc d) {
        if (d == null) return;
        try { d.Save(); ui.Toast(L.T("bus_saved", Path.GetFileName(d.Path))); } catch (Exception ex) { ui.Msg(ex.Message); }
        Raise("BusDirty");
    }

    // .bus면 거기에 적힌 model / paths / passengercabin을 읽는다 (같은 파일을 도색만 바꿔 다시 열 때는 편집 중인 길/좌석을 그대로 둔다)
    string BusModelPath(string path) {
        if (busFile != null && string.Equals(busFile.Path, path, StringComparison.OrdinalIgnoreCase)) return busFile.Model;
        busFile = null; busPaths = null; busCabin = null;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".bus" && ext != ".ovh") return path;
        try { busFile = BusFile.Load(path); } catch (Exception ex) { ui.Msg(ex.Message); return null; }
        try { if (File.Exists(busFile.Paths)) busPaths = PathsCfg.Load(busFile.Paths); } catch (Exception) { }
        try { if (File.Exists(busFile.Cabin)) busCabin = CabinCfg.Load(busFile.Cabin); } catch (Exception) { }
        return busFile.Model;
    }

    public string BusTitle { get { return busFile != null ? busFile.Name : (viewCfg != null ? Path.GetFileName(viewCfg.CfgPath) : ""); } }
    public string BusFiles {
        get {
            if (busFile == null) return viewCfg != null ? viewCfg.CfgPath : "";
            return L.T("bus_files", Rel(busFile.Model), Rel(busFile.Paths), Rel(busFile.Cabin));
        }
    }
    string Rel(string p) { if (string.IsNullOrEmpty(p)) return "-"; return busFile != null && p.StartsWith(busFile.Dir, StringComparison.OrdinalIgnoreCase) ? p.Substring(busFile.Dir.Length).TrimStart('\\') : p; }
    public bool HasBusTitle { get { return viewCfg != null; } }
    public bool HasPaths { get { return busPaths != null; } }
    public bool HasCabin { get { return busCabin != null; } }
    public bool HasVars { get { return Vars.Count > 0; } }
    public bool HasBlocks { get { return Blocks.Count > 0; } }
    public string SelPartText { get { return selPart == null ? L.T("bus_sel_none") : selPart.Name + (selPart.Mesh.Animated ? "" : " · " + L.T("bus_sel_static")); } }
    public string PathsText { get { return busPaths == null ? "" : L.T("bus_paths_stat", busPaths.Points.Count, busPaths.Links.Count); } }
    public string CabinText {
        get {
            if (busCabin == null) return "";
            int drv = busCabin.Seats.FindAll(delegate(Seat s) { return s.Driver; }).Count;
            return L.T("bus_cabin_stat", busCabin.Seats.Count - drv, drv, string.Join(", ", busCabin.Entries.ConvertAll(delegate(int i) { return i.ToString(); }).ToArray()),
                string.Join(", ", busCabin.Exits.ConvertAll(delegate(int i) { return i.ToString(); }).ToArray()));
        }
    }
    int newA, newB = 1;
    public int NewLinkA { get { return newA; } set { newA = value; Raise("NewLinkA"); } }
    public int NewLinkB { get { return newB; } set { newB = value; Raise("NewLinkB"); } }

    public Model3D BusOverlay { get { return busOverlay; } }
    public bool ShowPaths { get { return showPaths; } set { showPaths = value; Raise("ShowPaths"); RebuildOverlay(); } }
    public bool ShowSeats { get { return showSeats; } set { showSeats = value; Raise("ShowSeats"); RebuildOverlay(); } }
    public bool ShowPivots { get { return showPivots; } set { showPivots = value; Raise("ShowPivots"); RebuildOverlay(); } }
    public bool AnimFlip { get { return AnimMath.Flip; } set { AnimMath.Flip = value; Raise("AnimFlip"); Repose(); } }
    public string AnimOrder { get { return AnimMath.Inner ? "inner" : "outer"; } set { AnimMath.Inner = value != "outer"; Raise("AnimOrder"); Repose(); } }

    // 모델을 다 읽은 뒤 (화면 스레드): 부품 껍데기, 변수, 길/좌석 목록
    void BusLoaded() {
        foreach (PartItem p in Parts) if (p.Model != null && p.Visual == null) { p.Visual = new Model3DGroup(); p.Visual.Children.Add(p.Model); }
        BuildVars();
        selPart = null; Blocks.Clear();
        PathsChanged(); CabinChanged();
        Raise("BusTitle", "BusFiles", "HasBusTitle", "HasPaths", "HasCabin", "HasVars", "HasBlocks", "SelPartText");
        Repose();
    }

    // cfg의 anim_rot / anim_trans에 쓰인 변수. 범위는 시뮬레이터처럼: factor가 크면(각도) -1~1, 작으면 0~(180/factor)
    void BuildVars() {
        StopPlay();
        Vars.Clear();
        if (viewCfg == null) return;
        Dictionary<string, List<double>> fs = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> parts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (CfgMesh m in viewCfg.Meshes) foreach (AnimBlock b in m.Anims) foreach (AnimEntry e in b.Entries) {
            if (Math.Abs(e.Factor) < 1e-4 || e.Var.Length == 0) continue;
            List<double> l; if (!fs.TryGetValue(e.Var, out l)) { fs[e.Var] = l = new List<double>(); parts[e.Var] = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
            l.Add(Math.Abs(e.Factor)); parts[e.Var].Add(m.File);
        }
        List<string> names = new List<string>(fs.Keys);
        names.Sort(delegate(string a, string b) { int c = parts[b].Count.CompareTo(parts[a].Count); return c != 0 ? c : string.Compare(a, b, StringComparison.OrdinalIgnoreCase); });
        foreach (string n in names) {
            double a = 0; foreach (double f in fs[n]) a = Math.Max(a, f);
            bool big = a >= 20 || a == 0;
            VarItem v = new VarItem(delegate { if (!playingAny) Repose(); });
            v.Name = n; v.Min = big ? -1 : 0; v.Max = big ? 1 : Math.Ceiling(180 / a);
            v.Info = L.T("bus_var_info", parts[n].Count);
            VarItem me = v;
            v.PlayCmd = new Cmd(delegate { me.Playing = !me.Playing; if (me.Playing) StartPlay(); });
            Vars.Add(v);
        }
        Raise("HasVars");
    }

    double VarVal(string n) { foreach (VarItem v in Vars) if (string.Equals(v.Name, n, StringComparison.OrdinalIgnoreCase)) return v.Value; return 0; }

    // 애니메이션 자세를 부품에 건다
    void Repose() {
        if (viewCfg == null) return;
        Func<string, double> val = VarVal;
        foreach (PartItem p in Parts) {
            if (p.Visual == null || p.Mesh.Anims.Count == 0) continue;
            p.Visual.Transform = new MatrixTransform3D(AnimMath.ToWpf(AnimMath.Chain(p.Mesh, val, -1)));
        }
        RebuildOverlay();
    }

    // 재생: 켜진 변수들을 최소~최대로 부드럽게 왕복 (프레임마다)
    bool playingAny; DateTime playT0;
    void StartPlay() {
        if (playingAny) return;
        playingAny = true; playT0 = DateTime.Now;
        CompositionTarget.Rendering += PlayFrame;
    }
    void StopPlay() { if (!playingAny) return; playingAny = false; CompositionTarget.Rendering -= PlayFrame; }
    void PlayFrame(object s, EventArgs e) {
        bool any = false;
        double t = (DateTime.Now - playT0).TotalSeconds;
        foreach (VarItem v in Vars) if (v.Playing) { any = true; v.Value = v.Min + (v.Max - v.Min) * (0.5 - 0.5 * Math.Cos(t * Math.PI / 1.1)); }
        if (!any) { StopPlay(); return; }
        Repose();
    }

    // ---- 부품 고르기 → 블록 편집
    void SelectPart(PartItem p) {
        selPart = p; Blocks.Clear();
        if (p != null && viewCfg != null) {
            CfgMesh m = p.Mesh; CfgDoc doc = viewCfg.Doc;
            for (int i = 0; i < m.Anims.Count; i++) {
                AnimBlock b = m.Anims[i];
                BlockEdit be = new BlockEdit();
                be.Title = L.T("bus_block", i + 1) + (b.FromMesh ? " · origin_from_mesh" : "");
                be.Pivot = new List<NumEdit>(); be.Rot = new List<NumEdit>(); be.Factors = new List<NumEdit>();
                string[] ax = { "x", "y", "z" };
                for (int c = 0; c < 3; c++) {
                    int cc = c; AnimBlock bb = b;
                    be.Pivot.Add(new NumEdit(ax[c], delegate { return bb.Trans[cc]; }, delegate(double v) { bb.Trans[cc] = v; doc.Set(bb.TransLine[cc], v); Edited(); }, b.TransLine[c] >= 0));
                    be.Rot.Add(new NumEdit(ax[c], delegate { return bb.Rot[cc]; }, delegate(double v) { bb.Rot[cc] = v; doc.Set(bb.RotLine[cc], v); Edited(); }, b.RotLine[c] >= 0));
                }
                foreach (AnimEntry e in b.Entries) {
                    AnimEntry ee = e;
                    be.Factors.Add(new NumEdit(e.Kind + " " + e.Var, delegate { return ee.Factor; }, delegate(double v) { ee.Factor = v; doc.Set(ee.Line, v); Edited(); }, true));
                }
                if (b.RotLine[0] < 0 && b.RotLine[1] < 0 && b.RotLine[2] < 0) be.Note = L.T("bus_block_norot");
                Blocks.Add(be);
            }
        }
        Raise("HasBlocks", "SelPartText");
        RebuildOverlay();
    }
    void Edited() { Raise("BusDirty"); Repose(); }

    // ---- 사람 길 / 좌석 목록
    void PathsChanged() {
        PathRows.Clear(); LinkRows.Clear();
        if (busPaths != null) {
            HashSet<int> en = new HashSet<int>(busCabin != null ? busCabin.Entries : new List<int>()), ex = new HashSet<int>(busCabin != null ? busCabin.Exits : new List<int>());
            foreach (PathPoint pt in busPaths.Points) {
                PathPoint p = pt; RowEdit r = new RowEdit();
                r.Title = p.Index + (en.Contains(p.Index) ? " · " + L.T("bus_entry") : "") + (ex.Contains(p.Index) ? " · " + L.T("bus_exit") : "");
                r.Nums = new List<NumEdit>();
                string[] ax = { "x", "y", "z" };
                for (int c = 0; c < 3; c++) { int cc = c; r.Nums.Add(new NumEdit(ax[c], delegate { return p.P[cc]; }, delegate(double v) { busPaths.SetPoint(p, cc, v); RebuildOverlay(); }, true)); }
                PathRows.Add(r);
            }
            foreach (PathLink lk in busPaths.Links) {
                PathLink l = lk; RowEdit r = new RowEdit(); r.Title = l.A + " → " + l.B; r.Nums = new List<NumEdit>();
                r.RemoveCmd = new Cmd(delegate { busPaths.RemoveLink(l); PathsChanged(); });
                LinkRows.Add(r);
            }
        }
        Raise("PathsText", "HasPaths"); RebuildOverlay();
    }
    void CabinChanged() {
        SeatRows.Clear();
        if (busCabin != null) {
            int n = 0;
            foreach (Seat st in busCabin.Seats) {
                Seat s = st; RowEdit r = new RowEdit();
                r.Title = s.Driver ? L.T("bus_driver") : L.T("bus_seat", ++n);
                r.Nums = new List<NumEdit>();
                string[] lab = { "x", "y", "z", L.T("bus_seat_h"), L.T("bus_seat_rot") };
                for (int c = 0; c < 5; c++) { int cc = c; r.Nums.Add(new NumEdit(lab[c], delegate { return s.V[cc]; }, delegate(double v) { busCabin.SetSeat(s, cc, v); RebuildOverlay(); }, true)); }
                if (!s.Driver) r.RemoveCmd = new Cmd(delegate { busCabin.RemoveSeat(s); CabinChanged(); });
                SeatRows.Add(r);
            }
        }
        Raise("CabinText", "HasCabin"); PathsChanged();
    }

    // ---- 덧그림
    static readonly Color CPath = Color.FromRgb(20, 184, 166), CEntry = Color.FromRgb(250, 160, 30), CExit = Color.FromRgb(239, 68, 68),
        CSeat = Color.FromRgb(139, 92, 246), CDriver = Color.FromRgb(236, 72, 153);
    static readonly Color[] CBlock = { Color.FromRgb(74, 120, 201), Color.FromRgb(217, 131, 28), Color.FromRgb(47, 158, 110), Color.FromRgb(180, 80, 158), Color.FromRgb(168, 139, 12) };
    void RebuildOverlay() {
        Overlay3D o = new Overlay3D();
        if (showPaths && busPaths != null) {
            HashSet<int> en = new HashSet<int>(busCabin != null ? busCabin.Entries : new List<int>()), ex = new HashSet<int>(busCabin != null ? busCabin.Exits : new List<int>());
            foreach (PathLink l in busPaths.Links)
                if (l.A >= 0 && l.B >= 0 && l.A < busPaths.Points.Count && l.B < busPaths.Points.Count) o.Rod(AnimMath.P(busPaths.Points[l.A].P), AnimMath.P(busPaths.Points[l.B].P), 0.02, CPath);
            foreach (PathPoint p in busPaths.Points) { double s = en.Contains(p.Index) || ex.Contains(p.Index) ? 0.1 : 0.06; o.Box(AnimMath.P(p.P), s, s, s, en.Contains(p.Index) ? CEntry : ex.Contains(p.Index) ? CExit : CPath); }
        }
        if (showSeats && busCabin != null) {
            foreach (Seat s in busCabin.Seats) {
                Point3D c = AnimMath.P(s.V);
                o.Box(c, 0.36, 0.05, 0.36, s.Driver ? CDriver : CSeat);
                double r = s.V[4] * Math.PI / 180;   // 0 = 진행 방향(+y)
                o.Rod(c, AnimMath.P(new double[] { s.V[0] - Math.Sin(r) * 0.3, s.V[1] + Math.Cos(r) * 0.3, s.V[2] }), 0.03, s.Driver ? CDriver : CSeat);
            }
        }
        if (showPivots && selPart != null && viewCfg != null) {
            CfgMesh m = selPart.Mesh;
            for (int i = 0; i < m.Anims.Count; i++) {
                AnimBlock b = m.Anims[i]; if (!b.Moves) continue;
                double[] pre = AnimMath.Chain(m, VarVal, i);
                Point3D pv = AnimMath.Apply(pre, AnimMath.Pivot(b, m));
                double[] a = AnimMath.Dir(pre, AnimMath.Axis(b, m));
                Vector3D av = new Vector3D(a[0], a[2], -a[1]) * 0.35;
                Color col = CBlock[i % CBlock.Length];
                o.Box(pv, 0.05, 0.05, 0.05, col);
                o.Rod(pv - av, pv + av, 0.012, col);
            }
        }
        busOverlay = o.Group; Raise("BusOverlay");
    }
}
}
