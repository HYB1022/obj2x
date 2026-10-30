using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;

namespace Obj2x.UI {

// 오브젝트 만들기: 모델 폴더의 o3d 한 줄
public class ModelItem : Observable {
    bool chk = true;
    public string Path { get; set; }
    public string Name { get; set; }
    public string Sub { get; set; }
    public bool Checked { get { return chk; } set { Set(ref chk, value); } }
}

// OMSI 제작: 오브젝트(.sco)와 스플라인(.sli) 만들기
public partial class MainViewModel {
    public ObservableCollection<ModelItem> Models { get; private set; }
    public List<TrItem> AlphaChoices { get; private set; }
    public ICommand BrowseOmsiCmd { get; private set; }
    public ICommand BrowseModelsCmd { get; private set; }
    public ICommand MakeObjCmd { get; private set; }
    public ICommand PickRoadTexCmd { get; private set; }
    public ICommand PickWalkTexCmd { get; private set; }
    public ICommand ClearRoadTexCmd { get; private set; }
    public ICommand ClearWalkTexCmd { get; private set; }
    public ICommand MakeSplCmd { get; private set; }
    SplineSpec spl;

    void InitOmsi() {
        Models = new ObservableCollection<ModelItem>();
        AlphaChoices = new List<TrItem> {
            new TrItem("0", delegate { return L.T("obj_alpha_off"); }),
            new TrItem("1", delegate { return L.T("obj_alpha_cut"); }),
            new TrItem("2", delegate { return L.T("obj_alpha_blend"); }) };
        Loc.Changed += delegate { foreach (TrItem t in AlphaChoices) t.Refresh(); RefreshModels(); };
        if (!Omsi.IsInstall(S.OmsiDir)) { string f = Omsi.FindInstall(); if (f.Length > 0) S.OmsiDir = f; }
        BrowseOmsiCmd = new Cmd(delegate { string p = ui.BrowseFolder(S.OmsiDir); if (p != null) OmsiDir = p; });
        BrowseModelsCmd = new Cmd(delegate { string p = ui.BrowseFolder(ObjModels.Length > 0 ? ObjModels : S.Inp); if (p != null) ObjModels = p; });
        MakeObjCmd = new Cmd(delegate { MakeObjects(); });
        spl = SplineSpec.Unpack(S.Spl);
        string img = "Image (*.bmp;*.tga;*.dds;*.png;*.jpg)|*.bmp;*.tga;*.dds;*.png;*.jpg";
        PickRoadTexCmd = new Cmd(delegate { string f = ui.PickFile(L.T("spl_road_tex"), img, spl.RoadTex); if (f != null) { spl.RoadTex = f; SplChanged("SplRoadTex"); } });
        PickWalkTexCmd = new Cmd(delegate { string f = ui.PickFile(L.T("spl_walk_tex"), img, spl.WalkTex); if (f != null) { spl.WalkTex = f; SplChanged("SplWalkTex"); } });
        ClearRoadTexCmd = new Cmd(delegate { spl.RoadTex = ""; SplChanged("SplRoadTex"); });
        ClearWalkTexCmd = new Cmd(delegate { spl.WalkTex = ""; SplChanged("SplWalkTex"); });
        MakeSplCmd = new Cmd(delegate { MakeSpline(); });
        RefreshModels();
    }

    // ------------------------------------------------------------ 공통: OMSI 폴더
    public string OmsiDir {
        get { return S.OmsiDir; }
        set { S.OmsiDir = (value ?? "").Trim(); S.Save(); Raise("OmsiDir", "OmsiDirText", "ObjOutText", "SplOutText"); }
    }
    public string OmsiDirText { get { return Omsi.IsInstall(S.OmsiDir) ? S.OmsiDir : L.T("omsi_notfound"); } }
    bool OmsiOk { get { return Omsi.IsInstall(S.OmsiDir); } }

    // ------------------------------------------------------------ 오브젝트
    public string ObjModels {
        get { return S.ObjModels; }
        set {
            string v = (value ?? "").Trim();
            // 저장 폴더 이름을 직접 바꾼 적이 없으면 모델 폴더 이름을 따라간다
            if (S.ObjFolder.Length == 0 || S.ObjFolder == FolderName(S.ObjModels)) S.ObjFolder = FolderName(v);
            S.ObjModels = v; S.Save(); Raise("ObjModels", "ObjFolder", "ObjOutText"); RefreshModels();
        }
    }
    static string FolderName(string p) { try { return Omsi.SafeName(Path.GetFileName((p ?? "").TrimEnd('\\', '/'))); } catch (Exception) { return ""; } }
    public bool ModelsEmpty { get { return Models.Count == 0; } }
    public string ModelsText { get; private set; }

    void RefreshModels() {
        if (Models == null) return;
        Dictionary<string, bool> was = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (ModelItem m in Models) was[m.Path] = m.Checked;
        Models.Clear();
        string[] files = new string[0];
        try { if (Directory.Exists(S.ObjModels)) files = Directory.GetFiles(S.ObjModels, "*.o3d"); } catch (Exception) { }
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string f in files) {
            ModelItem it = new ModelItem(); it.Path = f; it.Name = Path.GetFileName(f);
            List<string> tex = Omsi.O3dTextures(f);
            int miss = 0, alpha = 0;
            foreach (string t in tex) {
                string tf = Omsi.FindTexture(t, S.ObjModels, Path.GetDirectoryName(S.ObjModels));
                if (tf == null) miss++; else if (Omsi.HasAlpha(tf)) alpha++;
            }
            it.Sub = L.T("obj_model_sub", tex.Count, alpha) + (miss > 0 ? L.T("obj_model_miss", miss) : "");
            bool c; if (was.TryGetValue(f, out c)) it.Checked = c;
            Models.Add(it);
        }
        ModelsText = !Directory.Exists(S.ObjModels) ? L.T("obj_models_none") : L.T("obj_models_count", files.Length);
        Raise("ModelsEmpty", "ModelsText");
    }

    public string ObjMode { get { return S.ObjCombine ? "one" : "each"; } set { S.ObjCombine = value == "one"; Raise("ObjMode", "ObjNameEnabled"); } }
    public bool ObjNameEnabled { get { return S.ObjCombine; } }
    public string ObjName { get { return S.ObjName; } set { S.ObjName = value ?? ""; Raise("ObjName"); } }
    public string ObjGroup { get { return S.ObjGroup; } set { S.ObjGroup = value ?? ""; Raise("ObjGroup"); } }
    public bool ObjColl { get { return S.ObjColl; } set { S.ObjColl = value; Raise("ObjColl"); } }
    public bool ObjSurf { get { return S.ObjSurf; } set { S.ObjSurf = value; Raise("ObjSurf"); } }
    public bool ObjShadow { get { return S.ObjShadow; } set { S.ObjShadow = value; Raise("ObjShadow"); } }
    public bool ObjFixed { get { return S.ObjFixed; } set { S.ObjFixed = value; Raise("ObjFixed"); } }
    public string ObjAlpha { get { return S.ObjAlpha.ToString(); } set { int n; if (int.TryParse(value, out n)) S.ObjAlpha = n; Raise("ObjAlpha"); } }
    public string ObjFolder { get { return S.ObjFolder; } set { S.ObjFolder = Omsi.SafeName(value); Raise("ObjFolder", "ObjOutText"); } }
    string ObjOut { get { return OmsiOk && S.ObjFolder.Length > 0 ? Path.Combine(S.OmsiDir, "Sceneryobjects", S.ObjFolder) : null; } }
    public string ObjOutText { get { return ObjOut ?? L.T(OmsiOk ? "obj_out_needname" : "omsi_notfound"); } }

    void MakeObjects() {
        List<string> sel = new List<string>();
        foreach (ModelItem m in Models) if (m.Checked) sel.Add(m.Path);
        if (sel.Count == 0) { ui.Msg(L.T("obj_err_none")); return; }
        string outDir = ObjOut;
        if (outDir == null) { ui.Msg(L.T(OmsiOk ? "obj_out_needname" : "omsi_err")); return; }
        ScoOptions o = new ScoOptions();
        o.Combine = S.ObjCombine; o.Name = S.ObjName; o.Group = S.ObjGroup;
        o.Collision = S.ObjColl; o.Surface = S.ObjSurf; o.Shadow = S.ObjShadow; o.Fixed = S.ObjFixed; o.Alpha = S.ObjAlpha;
        if (o.Combine && Omsi.SafeName(o.Name).Length == 0) o.Name = S.ObjFolder;
        S.Save();
        ScoResult r;
        try { r = ScoMaker.Make(sel, outDir, o); } catch (Exception ex) { ui.Msg(ex.Message); return; }
        string body = L.T("obj_done_body", r.Sco.Count, r.Copied.Count, outDir);
        if (r.Missing.Count > 0) body += "\n\n" + L.T("obj_done_missing", r.Missing.Count) + "\n" + string.Join("\n", r.Missing.GetRange(0, Math.Min(8, r.Missing.Count)).ToArray());
        ui.Confirm(L.T("obj_done_title"), body, L.T("btn_open_folder"), delegate(bool ok) { if (ok) ui.OpenFolder(outDir); });
    }

    // ------------------------------------------------------------ 스플라인
    public SplineSpec SplSpec { get { return spl; } }
    int splVersion;
    public int SplVersion { get { return splVersion; } }
    readonly Dictionary<string, string> splText = new Dictionary<string, string>();   // 입력 중인 글자 (숫자로 못 읽어도 그대로 둔다)

    void SplChanged(params string[] names) {
        splVersion++;
        S.Spl = spl.Pack();
        Raise(names); Raise("SplVersion", "SplSummary", "SplError", "SplWalkOn", "SplRoadTexText", "SplWalkTexText");
    }
    string NumText(string key, double v) { string t; return splText.TryGetValue(key, out t) ? t : Omsi_N(v); }
    static string Omsi_N(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
    bool ParseNum(string key, string text, out double v) {
        splText[key] = text ?? "";
        string t = (text ?? "").Trim().Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    public int SplLanes { get { return spl.Lanes; } set { spl.Lanes = Math.Max(1, Math.Min(12, value)); SplChanged("SplLanes"); } }
    public string SplLaneW { get { return NumText("lanew", spl.LaneW); } set { double v; if (ParseNum("lanew", value, out v)) spl.LaneW = v; SplChanged(); } }
    public string SplWalkL { get { return NumText("walkl", spl.WalkL); } set { double v; if (ParseNum("walkl", value, out v)) spl.WalkL = v; SplChanged(); } }
    public string SplWalkR { get { return NumText("walkr", spl.WalkR); } set { double v; if (ParseNum("walkr", value, out v)) spl.WalkR = v; SplChanged(); } }
    public string SplCurb { get { return NumText("curb", spl.Curb); } set { double v; if (ParseNum("curb", value, out v)) spl.Curb = v; SplChanged(); } }
    public string SplRoadRepeat { get { return NumText("rrep", spl.RoadRepeat); } set { double v; if (ParseNum("rrep", value, out v)) spl.RoadRepeat = v; SplChanged(); } }
    public string SplWalkRepeat { get { return NumText("wrep", spl.WalkRepeat); } set { double v; if (ParseNum("wrep", value, out v)) spl.WalkRepeat = v; SplChanged(); } }
    public string SplDir { get { return spl.OneWay ? "one" : "two"; } set { spl.OneWay = value == "one"; SplChanged("SplDir"); } }
    public string SplTexFit { get { return spl.PerLane ? "lane" : "road"; } set { spl.PerLane = value == "lane"; SplChanged("SplTexFit"); } }
    public bool SplWalkOn { get { return spl.WalkL > 0 || spl.WalkR > 0; } }
    public string SplRoadTexText { get { return spl.RoadTex.Length > 0 ? Path.GetFileName(spl.RoadTex) : L.T("spl_tex_default"); } }
    public string SplWalkTexText { get { return spl.WalkTex.Length > 0 ? Path.GetFileName(spl.WalkTex) : L.T("spl_tex_default"); } }
    public string SplSummary { get { return L.T("spl_summary", Omsi_N(spl.TotalW), Omsi_N(spl.RoadW), spl.Lanes, spl.Paths().Count); } }
    public string SplError { get { string e; return spl.Valid(out e) ? "" : L.T(e); } }
    public string SplName { get { return S.SplName; } set { S.SplName = value ?? ""; Raise("SplName", "SplOutText"); } }
    public string SplFolder { get { return S.SplFolder; } set { S.SplFolder = Omsi.SafeName(value); Raise("SplFolder", "SplOutText"); } }
    string SplOut { get { return OmsiOk && S.SplFolder.Length > 0 ? Path.Combine(S.OmsiDir, "Splines", S.SplFolder) : null; } }
    public string SplOutText {
        get {
            if (!OmsiOk) return L.T("omsi_notfound");
            if (SplOut == null || Omsi.SafeName(S.SplName).Length == 0) return L.T("spl_out_needname");
            return Path.Combine(SplOut, Omsi.SafeName(S.SplName) + ".sli");
        }
    }

    void DropTexture(string path) { spl.RoadTex = path; SplChanged("SplRoadTex"); ui.Toast(L.T("toast_drop_tex")); }

    void MakeSpline() {
        string err;
        if (!spl.Valid(out err)) { ui.Msg(L.T(err)); return; }
        string outDir = SplOut;
        if (!OmsiOk) { ui.Msg(L.T("omsi_err")); return; }
        if (outDir == null || Omsi.SafeName(S.SplName).Length == 0) { ui.Msg(L.T("spl_out_needname")); return; }
        S.Save();
        string f = Path.Combine(outDir, Omsi.SafeName(S.SplName) + ".sli");
        Action go = delegate {
            try { spl.Save(outDir, S.SplName); } catch (Exception ex) { ui.Msg(ex.Message); return; }
            ui.Confirm(L.T("spl_done_title"), L.T("spl_done_body", f), L.T("btn_open_folder"), delegate(bool ok) { if (ok) ui.OpenFolder(outDir); });
        };
        if (File.Exists(f)) ui.Confirm(L.T("spl_exists_title"), L.T("spl_exists_body", Path.GetFileName(f)), L.T("btn_overwrite"), delegate(bool ok) { if (ok) go(); });
        else go();
    }
}
}
