using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Input;

namespace Obj2x.UI {

// 모델 보기: 버스 model.cfg / 오브젝트 .sco를 3D로 보고, 고른 부품을 .blend(또는 .obj)로 저장
public partial class MainViewModel {
    public ObservableCollection<PartItem> Parts { get; private set; }
    public ICommand BrowseCfgCmd { get; private set; }
    public ICommand SaveBlendCmd { get; private set; }
    public ICommand SaveObjCmd { get; private set; }
    ModelCfg viewCfg;
    int viewScene, viewShow, loadGen;
    bool viewBusy;
    string viewPath = "", viewStatus = "";
    List<string> viewMissing = new List<string>();

    void InitViewer() {
        Parts = new ObservableCollection<PartItem>();
        BrowseCfgCmd = new Cmd(delegate {
            string start = viewPath.Length > 0 ? viewPath : (OmsiOk ? Path.Combine(Path.Combine(S.OmsiDir, "Vehicles"), "x") : "");
            string f = ui.PickFile(L.T("view_file"), "OMSI model (*.cfg;*.sco)|*.cfg;*.sco", start);
            if (f != null) LoadModel(f);
        });
        SaveBlendCmd = new Cmd(delegate { Export(true); }, delegate(object p) { return viewCfg != null && !viewBusy; });
        SaveObjCmd = new Cmd(delegate { Export(false); }, delegate(object p) { return viewCfg != null && !viewBusy; });
    }

    public string ViewPath { get { return viewPath.Length > 0 ? viewPath : L.T("view_none"); } }
    public int ViewScene { get { return viewScene; } }
    public int ViewShow { get { return viewShow; } }
    public bool ViewEmpty { get { return viewCfg == null && !viewBusy; } }
    public bool ViewBusy { get { return viewBusy; } }
    public string ViewStatus { get { return viewStatus; } }

    // 부품을 다른 스레드에서 읽고(텍스처 포함) 다 되면 한꺼번에 화면에 넘긴다
    public void LoadModel(string path) {
        ModelCfg cfg;
        try { cfg = ModelCfg.Load(path); } catch (Exception ex) { ui.Msg(ex.Message); return; }
        if (cfg.Meshes.Count == 0) { ui.Msg(L.T("view_err_nomesh")); return; }
        int gen = ++loadGen;
        viewPath = path; viewCfg = null; viewBusy = true;
        Parts.Clear(); viewScene++;
        viewStatus = L.T("view_loading", 0, cfg.Meshes.Count);
        Raise("ViewPath", "ViewScene", "ViewEmpty", "ViewBusy", "ViewStatus");
        CommandManager.InvalidateRequerySuggested();
        Thread th = new Thread(delegate() {
            List<PartItem> items = new List<PartItem>();
            List<string> missing = new List<string>();
            Dictionary<string, System.Windows.Media.Media3D.Material> cache = new Dictionary<string, System.Windows.Media.Media3D.Material>();
            for (int i = 0; i < cfg.Meshes.Count; i++) {
                if (gen != loadGen) return;
                PartItem it = ModelScene.Build(cfg, cfg.Meshes[i], cache, missing);
                it.Checked = it.Model != null && cfg.DefaultOn(cfg.Meshes[i]);
                items.Add(it);
                if (i % 8 == 0) { int n = i; ui.Post(delegate { if (gen == loadGen) { viewStatus = L.T("view_loading", n, cfg.Meshes.Count); Raise("ViewStatus"); } }); }
            }
            HideStrays(items);
            ui.Post(delegate {
                if (gen != loadGen) return;
                viewCfg = cfg; viewBusy = false; viewMissing = missing;
                foreach (PartItem it in items) { it.PropertyChanged += delegate { viewShow++; Raise("ViewShow"); ViewStat(); }; Parts.Add(it); }
                viewScene++; viewPreset = "ext";
                Raise("ViewScene", "ViewEmpty", "ViewBusy", "ViewPreset");
                ViewStat();
                int loaded = 0, encrypted = 0;
                foreach (PartItem it in items) { if (it.Model != null) loaded++; else if (it.Sub == L.T("o3d_err_encrypted")) encrypted++; }
                if (loaded == 0) ui.Msg(encrypted > 0 ? L.T("view_err_encrypted_all") : L.T("view_err_noparts", items.Count, cfg.VehicleDir));
                CommandManager.InvalidateRequerySuggested();
            });
        });
        th.IsBackground = true;
        th.Start();
    }

    // 애니메이션으로 제자리에 옮겨지는 부품(꽂는 표지판 등)은 파일 속 위치가 차체 밖이라 처음에는 끈다.
    // 차체 = 켜진 부품 중 삼각형의 95%를 차지하는 큰 부품들의 상자. 중심이 그 상자에서 1 m 넘게 벗어난 부품을 끈다
    static void HideStrays(List<PartItem> items) {
        List<PartItem> on = items.FindAll(delegate(PartItem p) { return p.Checked && p.Model != null; });
        int total = 0; foreach (PartItem p in on) total += p.Tris;
        on.Sort(delegate(PartItem a, PartItem b) { return b.Tris.CompareTo(a.Tris); });
        System.Windows.Media.Media3D.Rect3D box = System.Windows.Media.Media3D.Rect3D.Empty;
        int acc = 0;
        foreach (PartItem p in on) { if (acc >= total * 0.95 && !box.IsEmpty) break; box.Union(p.Bounds); acc += p.Tris; }
        if (box.IsEmpty) return;
        box = new System.Windows.Media.Media3D.Rect3D(box.X - 1, box.Y - 1, box.Z - 1, box.SizeX + 2, box.SizeY + 2, box.SizeZ + 2);
        foreach (PartItem p in on) {
            System.Windows.Media.Media3D.Rect3D b = p.Bounds;
            System.Windows.Media.Media3D.Point3D c = new System.Windows.Media.Media3D.Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
            if (!box.Contains(c)) { p.Checked = false; p.Sub += L.T("view_part_stray"); }
        }
    }

    void ViewStat() {
        int on = 0, tris = 0;
        foreach (PartItem p in Parts) if (p.Checked) { on++; tris += p.Tris; }
        viewStatus = L.T("view_stat", on, Parts.Count, tris.ToString("N0")) + (viewMissing.Count > 0 ? L.T("view_stat_miss", viewMissing.Count) : "");
        Raise("ViewStatus");
    }

    // 보기 묶음: 밖(외부에서 보이는 것), 안(실내), 전체(가장 자세한 LOD의 모든 부품, 조건부 포함)
    string viewPreset = "ext";
    public string ViewPreset {
        get { return viewPreset; }
        set {
            viewPreset = value ?? "ext"; Raise("ViewPreset");
            if (viewCfg == null) return;
            foreach (PartItem p in Parts) {
                CfgMesh m = p.Mesh;
                bool lod = m.Lod == 0 || m.Lod == viewCfg.BestLod;
                bool on = viewPreset == "all" ? lod : viewPreset == "int" ? lod && viewCfg.VisOn(m) && (m.ViewPoint == 0 || (m.ViewPoint & 2) != 0) : viewCfg.DefaultOn(m);
                p.Checked = on && p.Model != null;
            }
        }
    }

    // ------------------------------------------------------------ 저장: 켜 둔 부품만. .blend는 obj + mtl을 만든 뒤 Blender로 불러와 텍스처를 넣고 저장
    void Export(bool blend) {
        if (viewCfg == null || viewBusy) return;
        List<CfgMesh> sel = new List<CfgMesh>();
        foreach (PartItem p in Parts) if (p.Checked && p.Model != null) sel.Add(p.Mesh);
        if (sel.Count == 0) { ui.Msg(L.T("view_err_none")); return; }
        if (blend && !Conversion.BlenderOk(S)) { ui.Msg(L.T("msg_err_bl")); return; }
        if (blend && !File.Exists(PyPath)) { ui.Msg(L.T("msg_err_py")); return; }
        string name = Path.GetFileNameWithoutExtension(viewCfg.CfgPath);
        string f = ui.SaveFile(name + (blend ? ".blend" : ".obj"), blend ? "Blender (*.blend)|*.blend" : "Wavefront OBJ (*.obj)|*.obj");
        if (f == null) return;
        ExportTo(blend, f, sel);
    }

    public string LastExportError;   // 개발용(--save-model)
    public void ExportTo(bool blend, string f, List<CfgMesh> sel) {
        if (sel == null) { sel = new List<CfgMesh>(); foreach (PartItem p in Parts) if (p.Checked && p.Model != null) sel.Add(p.Mesh); }
        ModelCfg cfg = viewCfg;
        string blender = Conversion.Clean(S.Blender), py = PyPath;
        viewBusy = true; Raise("ViewBusy"); CommandManager.InvalidateRequerySuggested();
        ui.Toast(L.T(blend ? "view_saving_blend" : "view_saving_obj"));
        Thread th = new Thread(delegate() {
            string err = null; List<string> errors = new List<string>(), missing = null;
            try {
                string obj = f;
                if (blend) {
                    string tmp = Path.Combine(Path.GetTempPath(), "BusDepot_blend");
                    Directory.CreateDirectory(tmp);
                    obj = Path.Combine(tmp, "model.obj");
                }
                missing = ObjExport.Write(cfg, sel, obj, errors);
                if (blend) {
                    try { if (File.Exists(f)) File.Delete(f); } catch (Exception) { }
                    StringBuilder log = new StringBuilder();
                    ProcessStartInfo psi = new ProcessStartInfo(blender, "-b -P \"" + py + "\" -- --blend \"" + obj + "\" \"" + f + "\"");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                    using (Process p = new Process()) {
                        p.StartInfo = psi;
                        p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                        p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                        if (!p.WaitForExit(600000)) { try { p.Kill(); } catch (Exception) { } }
                        p.WaitForExit(2000);
                    }
                    if (!File.Exists(f)) {
                        string l = log.ToString();
                        int i = l.IndexOf("Error", StringComparison.Ordinal);
                        err = L.T("view_err_blend") + (i >= 0 ? "\n\n" + l.Substring(i, Math.Min(300, l.Length - i)) : "");
                    }
                }
            } catch (Exception ex) { err = ex.Message; }
            ui.Post(delegate {
                viewBusy = false; Raise("ViewBusy"); CommandManager.InvalidateRequerySuggested();
                LastExportError = err;
                if (err != null) { ui.Msg(err); return; }
                string body = L.T("view_saved_body", sel.Count, f);
                if (missing != null && missing.Count > 0) body += "\n\n" + L.T("view_saved_missing", missing.Count) + "\n" + string.Join(", ", missing.GetRange(0, Math.Min(8, missing.Count)).ToArray());
                if (errors.Count > 0) body += "\n\n" + string.Join("\n", errors.GetRange(0, Math.Min(5, errors.Count)).ToArray());
                ui.Confirm(L.T("view_saved_title"), body, L.T("btn_open_folder"), delegate(bool ok) { if (ok) ui.OpenFolder(Path.GetDirectoryName(f)); });
            });
        });
        th.IsBackground = true;
        th.Start();
    }
}
}
