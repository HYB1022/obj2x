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
            ui.BrowseFolder(start, delegate(string d) { if (d != null) LoadMap(d); });
        });
        CopyMissingCmd = new Cmd(delegate {
            if (mapInfo == null) return;
            StringBuilder sb = new StringBuilder();
            foreach (UseItem u in mapInfo.MissingObjects) sb.AppendLine(u.Path + "\t" + u.Count);
            foreach (UseItem u in mapInfo.MissingSplines) sb.AppendLine(u.Path + "\t" + u.Count);
            foreach (UseItem u in mapInfo.MissingAi) sb.AppendLine(u.Path + "\t" + u.Count);
            foreach (string h in mapInfo.MissingHumans) sb.AppendLine(h);
            try { ui.Copy(sb.ToString()); ui.Toast(L.T("map_copied")); } catch (Exception ex) { ui.Msg(ex.Message); }
        }, delegate(object p) { return mapInfo != null; });
        BrowseHofCmd = new Cmd(delegate {
            ui.PickFile("HOF", "HOF (*.hof)|*.hof", OmsiOk ? Path.Combine(Path.Combine(S.OmsiDir, "Vehicles"), "x") : "", delegate(string f) { if (f != null) LoadHof(f); });
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
    // 문자열 경로를 Image.Source에 넘기면 URI로 해석돼서 맥 경로(/Users/...)는 열리지 않는다 → 직접 읽어 Bitmap으로
    string mapPicPath; Avalonia.Media.Imaging.Bitmap mapPic;
    public Avalonia.Media.Imaging.Bitmap MapPicture {
        get {
            string p = mapInfo != null ? mapInfo.Picture : null;
            if (p != mapPicPath) {
                mapPicPath = p; mapPic = null;
                if (p != null) try { using (FileStream f = File.OpenRead(p)) mapPic = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(f, 640); } catch (Exception) { }
            }
            return mapPic;
        }
    }
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
    void RaiseMap() { Raise("MapData", "MapLoaded", "MapEmpty", "MapBusy", "MapStatus", "MapPath", "MapTitle", "MapDesc", "MapPicture", "MapStats", "MissingText"); Cmd.Requery(); }

    public const int MissingMax = 150;
    void FillMapRows() {
        MissingRows.Clear(); AiRows.Clear(); PeopleRows.Clear();
        if (mapInfo == null) return;
        // 줄이 수천 개면 화면이 멈추므로 앞의 MissingMax개만 보여 주고 나머지는 개수만 (전체는 '목록 복사')
        List<InfoRow> mrows = new List<InfoRow>();
        foreach (UseItem u in mapInfo.MissingObjects) mrows.Add(new InfoRow { Title = u.Path, Sub = L.T("map_miss_obj", u.Count) });
        foreach (UseItem u in mapInfo.MissingSplines) mrows.Add(new InfoRow { Title = u.Path, Sub = L.T("map_miss_spl", u.Count) });
        foreach (UseItem u in mapInfo.MissingAi) mrows.Add(new InfoRow { Title = u.Path, Sub = L.T("map_miss_ai", u.Count) });
        foreach (string h in mapInfo.MissingHumans) mrows.Add(new InfoRow { Title = h, Sub = L.T("map_miss_human") });
        for (int i = 0; i < mrows.Count && i < MissingMax; i++) MissingRows.Add(mrows[i]);
        if (mrows.Count > MissingMax) MissingRows.Add(new InfoRow { Title = L.T("map_more", mrows.Count - MissingMax), Sub = L.T("map_more_sub") });
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

    // ---- HOF: OMSI Vehicles 아래 차량 폴더들의 .hof 목록.
    // 여러 차량에 똑같은 hof(이름과 내용이 완전히 같음)가 들어 있는 경우가 많아서 하나로 합친다.
    // 이름은 같은데 내용이 다르면 구별되게 뒤에 차량 폴더 이름을 붙인다
    void FindHofsAsync() {
        // OMSI Vehicles + (맥) openOMSI content의 Vehicles
        List<string> vehs = new List<string>();
        if (OmsiOk) vehs.Add(Path.Combine(S.OmsiDir, "Vehicles"));
        foreach (string r in MapInfo.ExtraRoots) if (Directory.Exists(Path.Combine(r, "Vehicles"))) vehs.Add(Path.Combine(r, "Vehicles"));
        if (vehs.Count == 0) return;
        Thread th = new Thread(delegate() {
            List<string[]> found = HofList(vehs.ToArray());
            found.Sort(delegate(string[] a, string[] b) { return string.Compare(a[1], b[1], StringComparison.CurrentCultureIgnoreCase); });
            ui.Post(delegate {
                Hofs.Clear();
                foreach (string[] f in found) { string label = f[1]; Hofs.Add(new TrItem(f[0], delegate { return label; })); }
                Raise("HofsVisible");
            });
        });
        th.IsBackground = true; th.Start();
    }
    // [경로, 표시 이름]
    // 폴더 목록 순서는 운영체제마다 다르다 (맥은 정렬하지 않고 준다) → 이름순으로
    static string[] Sorted(string[] a) { Array.Sort(a, StringComparer.OrdinalIgnoreCase); return a; }
    public static List<string[]> HofList(params string[] vehiclesDirs) {
        List<string> keys = new List<string>();                     // 이름 + 내용 해시, 처음 나온 순서
        Dictionary<string, string> first = new Dictionary<string, string>();
        Dictionary<string, string> nameOf = new Dictionary<string, string>();
        Dictionary<string, List<string>> dirs = new Dictionary<string, List<string>>();
        try {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                foreach (string vehiclesDir in vehiclesDirs) { if (!Directory.Exists(vehiclesDir)) continue; foreach (string d in Sorted(Directory.GetDirectories(vehiclesDir)))
                    foreach (string f in Sorted(Directory.GetFiles(d, "*.hof"))) {
                        string name, hash;
                        try { name = HofFile.ReadName(f); hash = Convert.ToBase64String(md5.ComputeHash(File.ReadAllBytes(f))); } catch (Exception) { continue; }
                        if (name.Length == 0) name = Path.GetFileNameWithoutExtension(f);
                        string key = name + "\n" + hash;
                        if (!first.ContainsKey(key)) { first[key] = f; nameOf[key] = name; dirs[key] = new List<string>(); keys.Add(key); }
                        dirs[key].Add(Path.GetFileName(d));
                    } }
        } catch (Exception) { }
        Dictionary<string, int> sameName = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        foreach (string k in keys) { int n; sameName.TryGetValue(nameOf[k], out n); sameName[nameOf[k]] = n + 1; }
        List<string[]> r = new List<string[]>();
        foreach (string k in keys) {
            string label = nameOf[k];
            if (sameName[label] > 1) {
                List<string> ds = dirs[k];
                label += " · " + ds[0] + (ds.Count > 1 ? " +" + (ds.Count - 1) : "");
            }
            r.Add(new string[] { first[k], label });
        }
        return r;
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
