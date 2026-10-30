using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Input;

namespace Obj2x.UI {

public class InfoRow { public string Title { get; set; } public string Sub { get; set; } }

// 맵 탭: 맵 정보, 2D 지도(타일·스플라인·오브젝트), 없는 오브젝트/스플라인, AI, 사람, 시간표, HOF 보기
public partial class MainViewModel {
    MapInfo mapInfo; bool mapBusy; string mapStatus = "";
    public ObservableCollection<InfoRow> MissingRows { get; private set; }
    public ObservableCollection<InfoRow> AiRows { get; private set; }
    public ObservableCollection<InfoRow> PeopleRows { get; private set; }
    public ObservableCollection<TrItem> Hofs { get; private set; }
    public ObservableCollection<InfoRow> HofTrips { get; private set; }
    public ICommand BrowseMapCmd { get; private set; }
    public ICommand CopyMissingCmd { get; private set; }
    public ICommand BrowseHofCmd { get; private set; }
    string mapLayers = "tiles,splines,objects";
    HofFile hof;

    void InitMap() {
        MissingRows = new ObservableCollection<InfoRow>(); AiRows = new ObservableCollection<InfoRow>(); PeopleRows = new ObservableCollection<InfoRow>();
        Hofs = new ObservableCollection<TrItem>(); HofTrips = new ObservableCollection<InfoRow>();
        BrowseMapCmd = new Cmd(delegate {
            string start = mapInfo != null ? mapInfo.Dir : (OmsiOk ? Path.Combine(S.OmsiDir, "maps") : "");
            string d = ui.BrowseFolder(start);
            if (d != null) LoadMap(d);
        });
        CopyMissingCmd = new Cmd(delegate {
            if (mapInfo == null) return;
            StringBuilder sb = new StringBuilder();
            foreach (UseItem u in mapInfo.MissingObjects) sb.AppendLine(u.Path + "\t" + u.Count);
            foreach (UseItem u in mapInfo.MissingSplines) sb.AppendLine(u.Path + "\t" + u.Count);
            foreach (UseItem u in mapInfo.MissingAi) sb.AppendLine(u.Path + "\t" + u.Count);
            foreach (string h in mapInfo.MissingHumans) sb.AppendLine(h);
            try { System.Windows.Clipboard.SetText(sb.ToString()); ui.Toast(L.T("map_copied")); } catch (Exception ex) { ui.Msg(ex.Message); }
        }, delegate(object p) { return mapInfo != null; });
        BrowseHofCmd = new Cmd(delegate {
            string f = ui.PickFile("HOF", "HOF (*.hof)|*.hof", OmsiOk ? Path.Combine(Path.Combine(S.OmsiDir, "Vehicles"), "x") : "");
            if (f != null) LoadHof(f);
        });
        PageChanged += delegate(string p) { if (p == "map" && Hofs.Count == 0) FindHofsAsync(); };
    }

    public MapInfo MapData { get { return mapInfo; } }
    public bool MapLoaded { get { return mapInfo != null; } }
    public bool MapEmpty { get { return mapInfo == null && !mapBusy; } }
    public bool MapBusy { get { return mapBusy; } }
    public string MapStatus { get { return mapStatus; } }
    public string MapPath { get { return mapInfo != null ? mapInfo.Dir : L.T("map_none"); } }
    public string MapTitle { get { return mapInfo == null ? "" : (mapInfo.Friendly.Length > 0 ? mapInfo.Friendly : mapInfo.Name); } }
    public string MapDesc { get { return mapInfo == null ? "" : mapInfo.Description; } }
    public string MapPicture { get { return mapInfo != null ? mapInfo.Picture : null; } }
    public string MapStats {
        get {
            if (mapInfo == null) return "";
            MapInfo m = mapInfo;
            string years = m.YearFrom > 0 ? m.YearFrom + "–" + m.YearTo : "-";
            return L.T("map_stats", m.Tiles.Count, m.Objects.Count.ToString("N0"), m.Splines.Count.ToString("N0"), years, m.Depot.Length > 0 ? m.Depot : "-", m.Lines, m.Trips)
                + (m.MissingTiles > 0 ? L.T("map_stats_tiles", m.MissingTiles) : "");
        }
    }
    public string MissingText {
        get {
            if (mapInfo == null) return "";
            int o = mapInfo.MissingObjects.Count, s = mapInfo.MissingSplines.Count, a = mapInfo.MissingAi.Count, h = mapInfo.MissingHumans.Count;
            return o + s + a + h == 0 ? L.T("map_missing_none") : L.T("map_missing_stat", o, s, a, h);
        }
    }
    public bool MapShowObjects { get { return mapLayers.Contains("objects"); } set { SetLayer("objects", value); } }
    public bool MapShowSplines { get { return mapLayers.Contains("splines"); } set { SetLayer("splines", value); } }
    public bool MapMissingOnly { get { return mapLayers.Contains("missing"); } set { SetLayer("missing", value); } }
    public string MapLayers { get { return mapLayers; } }
    void SetLayer(string n, bool on) {
        List<string> l = new List<string>(mapLayers.Split(','));
        l.Remove(n); if (on) l.Add(n);
        mapLayers = string.Join(",", l.ToArray()); Raise("MapLayers", "MapShowObjects", "MapShowSplines", "MapMissingOnly");
    }

    // 맵 폴더(global.cfg가 있는 곳) 또는 그 안의 파일
    public void LoadMap(string dir) {
        if (File.Exists(dir)) dir = Path.GetDirectoryName(dir);
        if (!File.Exists(Path.Combine(dir, "global.cfg"))) { ui.Msg(L.T("map_err_noglobal")); return; }
        if (mapBusy) return;
        mapBusy = true; mapInfo = null; mapStatus = L.T("map_loading", 0, 0);
        RaiseMap();
        string root = OmsiOk ? S.OmsiDir : "";
        Thread th = new Thread(delegate() {
            MapInfo m = null; string err = null;
            try {
                m = MapInfo.Load(dir, root, delegate(int i, int n) {
                    ui.Post(delegate { mapStatus = L.T("map_loading", i, n); Raise("MapStatus"); });
                });
            } catch (Exception ex) { err = ex.Message; }
            ui.Post(delegate {
                mapBusy = false; mapInfo = m;
                if (err != null) ui.Msg(err);
                FillMapRows();
                mapStatus = ""; RaiseMap();
            });
        });
        th.IsBackground = true; th.Start();
    }
    void RaiseMap() { Raise("MapData", "MapLoaded", "MapEmpty", "MapBusy", "MapStatus", "MapPath", "MapTitle", "MapDesc", "MapPicture", "MapStats", "MissingText"); CommandManager.InvalidateRequerySuggested(); }

    void FillMapRows() {
        MissingRows.Clear(); AiRows.Clear(); PeopleRows.Clear();
        if (mapInfo == null) return;
        foreach (UseItem u in mapInfo.MissingObjects) MissingRows.Add(new InfoRow { Title = u.Path, Sub = L.T("map_miss_obj", u.Count) });
        foreach (UseItem u in mapInfo.MissingSplines) MissingRows.Add(new InfoRow { Title = u.Path, Sub = L.T("map_miss_spl", u.Count) });
        foreach (UseItem u in mapInfo.MissingAi) MissingRows.Add(new InfoRow { Title = u.Path, Sub = L.T("map_miss_ai", u.Count) });
        foreach (string h in mapInfo.MissingHumans) MissingRows.Add(new InfoRow { Title = h, Sub = L.T("map_miss_human") });
        foreach (AiGroup g in mapInfo.Ai) {
            int n = 0, miss = 0; foreach (UseItem v in g.Vehicles) { n += v.Count; if (mapInfo.MissingAi.Exists(delegate(UseItem x) { return string.Equals(x.Path, v.Path, StringComparison.OrdinalIgnoreCase); })) miss++; }
            List<string> names = new List<string>(); foreach (UseItem v in g.Vehicles) names.Add(Path.GetFileNameWithoutExtension(v.Path) + " ×" + v.Count);
            AiRows.Add(new InfoRow { Title = (g.Name.Length > 0 ? g.Name : g.Kind) + " · " + L.T("map_ai_count", g.Vehicles.Count, n) + (miss > 0 ? " · " + L.T("map_ai_miss", miss) : ""),
                Sub = string.Join(", ", names.ToArray()) });
        }
        PeopleRows.Add(new InfoRow { Title = L.T("map_humans", mapInfo.Humans.Count), Sub = Summ(mapInfo.Humans) });
        PeopleRows.Add(new InfoRow { Title = L.T("map_drivers", mapInfo.Drivers.Count), Sub = Summ(mapInfo.Drivers) });
    }
    static string Summ(List<string> l) {
        List<string> n = new List<string>(); foreach (string s in l) { n.Add(Path.GetFileNameWithoutExtension(s)); if (n.Count >= 12) { n.Add("…"); break; } }
        return string.Join(", ", n.ToArray());
    }

    // ---- HOF: OMSI Vehicles 아래 차량 폴더들의 .hof 목록 (이름 | 차량 폴더)
    void FindHofsAsync() {
        if (!OmsiOk) return;
        string veh = Path.Combine(S.OmsiDir, "Vehicles");
        Thread th = new Thread(delegate() {
            List<string[]> found = new List<string[]>();
            try {
                foreach (string d in Directory.GetDirectories(veh))
                    foreach (string f in Directory.GetFiles(d, "*.hof")) found.Add(new string[] { f, HofFile.ReadName(f) + " · " + Path.GetFileName(d) });
            } catch (Exception) { }
            found.Sort(delegate(string[] a, string[] b) { return string.Compare(a[1], b[1], StringComparison.CurrentCultureIgnoreCase); });
            ui.Post(delegate {
                Hofs.Clear();
                foreach (string[] f in found) { string label = f[1]; Hofs.Add(new TrItem(f[0], delegate { return label; })); }
                Raise("HofsVisible");
            });
        });
        th.IsBackground = true; th.Start();
    }
    public bool HofsVisible { get { return Hofs.Count > 0; } }
    string hofPick = "";
    public string HofPick { get { return hofPick; } set { hofPick = value ?? ""; Raise("HofPick"); if (hofPick.Length > 0) LoadHof(hofPick); } }
    public bool HofLoaded { get { return hof != null; } }
    public string HofTitle { get { return hof == null ? "" : hof.Name; } }
    public string HofStats { get { return hof == null ? "" : L.T("hof_stats", hof.Termini.Count, hof.Busstops.Count, hof.Trips.Count, hof.ServiceTrip) + "\n" + hof.Path; } }
    public string HofTermini {
        get {
            if (hof == null) return "";
            List<string> t = new List<string>();
            foreach (string[] r in hof.Termini) { string name = r.Length > 2 ? r[2] : (r.Length > 1 ? r[1] : r[0]); if (r[0].StartsWith("{") && r.Length > 3) name = r[2]; t.Add(name.Trim()); }
            return string.Join(" · ", t.ToArray());
        }
    }
    public string HofStops {
        get {
            if (hof == null) return "";
            List<string> t = new List<string>(); foreach (string[] r in hof.Busstops) t.Add(r[0].Trim());
            return string.Join(" · ", t.ToArray());
        }
    }
    public void LoadHof(string path) {
        try { hof = HofFile.Load(path); } catch (Exception ex) { ui.Msg(ex.Message); return; }
        HofTrips.Clear();
        foreach (HofTrip t in hof.Trips)
            HofTrips.Add(new InfoRow { Title = L.T("hof_trip", t.Line.Trim(), t.Code, t.Route, t.Stops.Count), Sub = string.Join(" → ", t.Stops.ToArray()) });
        Raise("HofLoaded", "HofTitle", "HofStats", "HofTermini", "HofStops");
    }
}
}
