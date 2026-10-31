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
    public Action<NumEdit> Before;
    string text;
    public string Text {
        get { return text ?? CfgDoc.Fmt(get()); }
        set { text = value; double v; if (Enabled && CfgDoc.Num(value, out v)) { if (Before != null) Before(this); set(v); } }
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
        GizmoCmd = new Cmd(delegate(object p) { GizmoMove(p as GizmoDrag); });
        UndoCmd = new Cmd(delegate { Undo(); }, delegate(object p) { return undoSteps.Count > 0 && page == "bus"; });
        RedoCmd = new Cmd(delegate { Redo(); }, delegate(object p) { return redoSteps.Count > 0 && page == "bus"; });
        SaveCfgCmd = new Cmd(delegate { SaveDoc(viewCfg != null ? viewCfg.Doc : null); }, delegate(object p) { return viewCfg != null; });
        ResetVarsCmd = new Cmd(delegate { foreach (VarItem v in Vars) { v.Playing = false; v.Value = 0; } });
        AddPointCmd = new Cmd(delegate {
            if (busPaths == null) return;
            double[] l = busPaths.Points.Count > 0 ? busPaths.Points[busPaths.Points.Count - 1].P : new double[3];
            UndoMark(busPaths.Doc, null); busPaths.AddPoint(l[0], l[1] - 0.5, l[2]); PathsChanged();
        }, delegate(object p) { return busPaths != null; });
        AddLinkCmd = new Cmd(delegate {
            if (busPaths == null) return;
            int a = NewLinkA, b = NewLinkB;
            if (a == b || a < 0 || b < 0 || a >= busPaths.Points.Count || b >= busPaths.Points.Count) { ui.Msg(L.T("bus_err_link", busPaths.Points.Count - 1)); return; }
            UndoMark(busPaths.Doc, null); busPaths.AddLink(a, b); PathsChanged();
        }, delegate(object p) { return busPaths != null; });
        SavePathsCmd = new Cmd(delegate { SaveDoc(busPaths != null ? busPaths.Doc : null); }, delegate(object p) { return busPaths != null; });
        AddSeatCmd = new Cmd(delegate {
            if (busCabin == null) return;
            double[] l = busCabin.Seats.Count > 0 ? busCabin.Seats[busCabin.Seats.Count - 1].V : new double[5];
            UndoMark(busCabin.Doc, null); busCabin.AddSeat(l[0], l[1] - 0.8, l[2]); CabinChanged();
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
        busFile = null; busPaths = null; busCabin = null; UndoClear();
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
        foreach (PartItem p in Parts) if (p.Model != null && p.Visual == null) {
            p.Visual = new Model3DGroup(); p.Visual.Children.Add(p.Model);
            if (p.ModelAlpha != null) { p.VisualAlpha = new Model3DGroup(); p.VisualAlpha.Children.Add(p.ModelAlpha); }
        }
        BuildVars();
        selPart = null; Blocks.Clear();
        PathsChanged(); CabinChanged();
        Raise("BusTitle", "BusFiles", "HasBusTitle", "HasPaths", "HasCabin", "HasVars", "HasBlocks", "SelPartText");
        Repose();
    }

    // cfg의 anim_rot / anim_trans에 쓰인 변수의 범위. OMSI의 문(door_*), 와이퍼(wiperpos), 스위치는 0~1이라 factor가 크면(각도) 0~1.
    // 조향·변속 레버처럼 양쪽으로 움직이는 이름만 -1~1, 라디안(factor 57.3)은 한 바퀴(0~2π), factor가 작으면 0~(180/factor)
    static readonly System.Text.RegularExpressions.Regex SignedVar = new System.Text.RegularExpressions.Regex("steer|lenk|shifter|joystick|_posx|_posy|winkel_x|winkel_y", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
            bool big = a >= 20 || a == 0, signed = SignedVar.IsMatch(n), rad = Math.Abs(a - 180 / Math.PI) < 0.01;
            VarItem v = new VarItem(delegate { if (!playingAny) Repose(); });
            v.Name = n;
            v.Max = rad ? (signed ? 0.7 : Math.Round(2 * Math.PI, 2)) : big ? 1 : Math.Ceiling(180 / a);
            v.Min = signed ? -v.Max : 0;
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
            if (p.VisualAlpha != null) p.VisualAlpha.Transform = p.Visual.Transform;
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
                    be.Pivot.Add(Undoable(doc, new NumEdit(ax[c], delegate { return bb.Trans[cc]; }, delegate(double v) { bb.Trans[cc] = v; doc.Set(bb.TransLine[cc], v); Edited(); }, b.TransLine[c] >= 0)));
                    be.Rot.Add(Undoable(doc, new NumEdit(ax[c], delegate { return bb.Rot[cc]; }, delegate(double v) { bb.Rot[cc] = v; doc.Set(bb.RotLine[cc], v); Edited(); }, b.RotLine[c] >= 0)));
                }
                foreach (AnimEntry e in b.Entries) {
                    AnimEntry ee = e;
                    be.Factors.Add(Undoable(doc, new NumEdit(e.Kind + " " + e.Var, delegate { return ee.Factor; }, delegate(double v) { ee.Factor = v; doc.Set(ee.Line, v); Edited(); }, true)));
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
                for (int c = 0; c < 3; c++) { int cc = c; r.Nums.Add(Undoable(busPaths.Doc, new NumEdit(ax[c], delegate { return p.P[cc]; }, delegate(double v) { busPaths.SetPoint(p, cc, v); RebuildOverlay(); }, true))); }
                PathRows.Add(r);
            }
            foreach (PathLink lk in busPaths.Links) {
                PathLink l = lk; RowEdit r = new RowEdit(); r.Title = l.A + " → " + l.B; r.Nums = new List<NumEdit>();
                r.RemoveCmd = new Cmd(delegate { UndoMark(busPaths.Doc, null); busPaths.RemoveLink(l); PathsChanged(); });
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
                for (int c = 0; c < 5; c++) { int cc = c; r.Nums.Add(Undoable(busCabin.Doc, new NumEdit(lab[c], delegate { return s.V[cc]; }, delegate(double v) { busCabin.SetSeat(s, cc, v); RebuildOverlay(); }, true))); }
                if (!s.Driver) r.RemoveCmd = new Cmd(delegate { UndoMark(busCabin.Doc, null); busCabin.RemoveSeat(s); CabinChanged(); });
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
            for (int i = 0; i < busPaths.Points.Count; i++) {
                PathPoint p = busPaths.Points[i]; string h = "p:" + i; bool sel = h == gizmoSel;
                double s = (en.Contains(p.Index) || ex.Contains(p.Index) ? 0.1 : 0.06) * (sel ? 1.6 : 1);
                o.Box(AnimMath.P(p.P), s, s, s, sel ? CSel : en.Contains(p.Index) ? CEntry : ex.Contains(p.Index) ? CExit : CPath, h);
            }
        }
        if (showSeats && busCabin != null) {
            for (int i = 0; i < busCabin.Seats.Count; i++) {
                Seat s = busCabin.Seats[i]; string h = "s:" + i;
                Color col = h == gizmoSel ? CSel : s.Driver ? CDriver : CSeat;
                Point3D c = AnimMath.P(s.V);
                o.Box(c, 0.36, h == gizmoSel ? 0.09 : 0.05, 0.36, col, h);
                double r = s.V[4] * Math.PI / 180;   // 0 = 진행 방향(+y)
                o.Rod(c, AnimMath.P(new double[] { s.V[0] - Math.Sin(r) * 0.3, s.V[1] + Math.Cos(r) * 0.3, s.V[2] }), 0.03, col, h);
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
        // 고른 점/좌석에 축 화살표 (Blender처럼 x 빨강, y 초록, z 파랑). 화살표를 끌면 그 축으로만 움직인다
        string selName; double[] sp = GizmoTarget(gizmoSel, out selName);
        if (sp != null && (gizmoSel[0] == 'p' ? showPaths : showSeats)) {
            Point3D c = AnimMath.P(sp);
            // cfg x, y(앞), z(위) → 화면 (1,0,0), (0,0,-1), (0,1,0)
            Vector3D[] ax = { new Vector3D(1, 0, 0), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0) };
            Color[] col = { Color.FromRgb(232, 64, 64), Color.FromRgb(90, 190, 60), Color.FromRgb(60, 120, 240) };
            for (int i = 0; i < 3; i++) {
                GizmoAxis h = new GizmoAxis { Target = gizmoSel, Axis = ax[i] };
                o.Rod(c + ax[i] * 0.06, c + ax[i] * 0.5, 0.025, col[i], h);
                o.Box(c + ax[i] * 0.53, 0.07, 0.07, 0.07, col[i], h);
            }
        }
        busOverlay = o.Group; Raise("BusOverlay");
    }

    // ---- 되돌리기 / 다시 실행 (Ctrl+Z / Ctrl+Y, Ctrl+Shift+Z): 바꾸기 직전 파일 줄을 통째로 남겨 둔다.
    // 숫자 칸은 같은 칸을 이어서 고치는 동안 한 번만 남긴다 (글자마다 남지 않게)
    class UndoStep { public CfgDoc Doc; public List<string> Lines; public object Tag; }
    readonly List<UndoStep> undoSteps = new List<UndoStep>(), redoSteps = new List<UndoStep>();
    public ICommand UndoCmd { get; private set; }
    public ICommand RedoCmd { get; private set; }

    NumEdit Undoable(CfgDoc doc, NumEdit n) { n.Before = delegate(NumEdit x) { UndoMark(doc, x); }; return n; }
    void UndoMark(CfgDoc doc, object tag) {
        if (doc == null) return;
        if (tag != null && undoSteps.Count > 0) { UndoStep last = undoSteps[undoSteps.Count - 1]; if (last.Doc == doc && last.Tag == tag) return; }
        undoSteps.Add(new UndoStep { Doc = doc, Lines = new List<string>(doc.Lines), Tag = tag });
        if (undoSteps.Count > 300) undoSteps.RemoveAt(0);
        redoSteps.Clear();
        CommandManager.InvalidateRequerySuggested();
    }
    void UndoClear() { undoSteps.Clear(); redoSteps.Clear(); CommandManager.InvalidateRequerySuggested(); }
    public void Undo() { Swap(undoSteps, redoSteps); }
    public void Redo() { Swap(redoSteps, undoSteps); }
    void Swap(List<UndoStep> from, List<UndoStep> to) {
        if (from.Count == 0) return;
        UndoStep st = from[from.Count - 1]; from.RemoveAt(from.Count - 1);
        to.Add(new UndoStep { Doc = st.Doc, Lines = new List<string>(st.Doc.Lines) });
        st.Doc.Lines.Clear(); st.Doc.Lines.AddRange(st.Lines); st.Doc.Dirty = true;
        DocRestored(st.Doc);
        ui.Toast(L.T(from == undoSteps || to == redoSteps ? "bus_undone" : "bus_redone"));
        CommandManager.InvalidateRequerySuggested();
    }
    // 줄을 되돌린 파일을 다시 읽어 화면에 반영
    void DocRestored(CfgDoc doc) {
        if (busPaths != null && doc == busPaths.Doc) { busPaths.Parse(); PathsChanged(); }
        if (busCabin != null && doc == busCabin.Doc) { busCabin.Parse(); CabinChanged(); }
        if (viewCfg != null && doc == viewCfg.Doc) {
            // [newanim] 블록은 줄 번호가 그대로라서 값만 다시 읽는다 (편집 칸이 같은 블록 객체를 쓰므로)
            foreach (CfgMesh m in viewCfg.Meshes) foreach (AnimBlock b in m.Anims) {
                for (int c = 0; c < 3; c++) { if (b.TransLine[c] >= 0) b.Trans[c] = doc.NumAt(b.TransLine[c]); if (b.RotLine[c] >= 0) b.Rot[c] = doc.NumAt(b.RotLine[c]); }
                foreach (AnimEntry e in b.Entries) e.Factor = doc.NumAt(e.Line);
            }
            SelectPart(selPart); Repose();
        }
        string n; if (GizmoTarget(gizmoSel, out n) == null) gizmoSel = null;
        RebuildOverlay();
        Raise("BusDirty");
    }

    // 모델을 모두 숨기고 사람 길·좌석만 보기 (켜면 길·좌석 표시도 켠다)
    bool busHideModel;
    public bool BusHideModel {
        get { return busHideModel; }
        set {
            busHideModel = value; Raise("BusHideModel");
            if (value) { showPaths = true; showSeats = true; Raise("ShowPaths", "ShowSeats"); RebuildOverlay(); }
        }
    }

    // ---- 3D에서 사람 길 점 / 좌석을 끌어서 옮기기. 그냥 끌면 자유 이동, Shift를 누르면 움직인 양을 1 m 단위로
    public ICommand GizmoCmd { get; private set; }
    static readonly Color CSel = Color.FromRgb(250, 204, 21);
    string gizmoSel, gizmoText = "";
    bool gizmoMarked;
    double[] gizmoOrig;
    public string GizmoText { get { return gizmoText; } }
    public const double GizmoSnap = 1;

    double[] GizmoTarget(string h, out string name) {
        name = null;
        if (h == null || h.Length < 3) return null;
        int i; if (!int.TryParse(h.Substring(2), out i)) return null;
        if (h[0] == 'p' && busPaths != null && i < busPaths.Points.Count) { name = L.T("bus_point", busPaths.Points[i].Index); return busPaths.Points[i].P; }
        if (h[0] == 's' && busCabin != null && i < busCabin.Seats.Count) {
            Seat s = busCabin.Seats[i];
            int n = 0; for (int k = 0; k <= i; k++) if (!busCabin.Seats[k].Driver) n++;
            name = s.Driver ? L.T("bus_driver") : L.T("bus_seat", n);
            return s.V;
        }
        return null;
    }

    public void GizmoMove(GizmoDrag g) {
        if (g == null) return;
        string h = g.Handle as string, name;
        double[] cur = GizmoTarget(h, out name);
        if (cur == null) return;
        if (g.Start) { gizmoSel = h; gizmoOrig = new double[] { cur[0], cur[1], cur[2] }; gizmoMarked = false; }
        else if (gizmoOrig != null && h == gizmoSel) {
            // 화면 (x, y위, -앞) → cfg (x, 앞, 위)
            double[] d = { g.Delta.X, -g.Delta.Z, g.Delta.Y };
            for (int c = 0; c < 3; c++) {
                double v = d[c];
                if (g.Snap) v = Math.Round(v / GizmoSnap) * GizmoSnap;
                v = Math.Round(gizmoOrig[c] + v, 3);
                if (v == cur[c]) continue;
                // 실제로 움직였을 때만 되돌리기에 남긴다 (누르기만 한 것은 기록하지 않음)
                if (!gizmoMarked) { UndoMark(h[0] == 'p' ? busPaths.Doc : busCabin.Doc, null); gizmoMarked = true; }
                if (h[0] == 'p') busPaths.SetPoint(busPaths.Points[int.Parse(h.Substring(2))], c, v);
                else busCabin.SetSeat(busCabin.Seats[int.Parse(h.Substring(2))], c, v);
            }
        }
        gizmoText = L.T("bus_gizmo", name, CfgDoc.Fmt(cur[0]), CfgDoc.Fmt(cur[1]), CfgDoc.Fmt(cur[2]));
        Raise("GizmoText");
        RebuildOverlay();
        // 놓으면 목록의 숫자도 새로 (끄는 동안은 3D만)
        if (g.Done) { if (h[0] == 'p') PathsChanged(); else CabinChanged(); Raise("BusDirty"); }
    }
}
}
