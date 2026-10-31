using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Obj2x;

namespace BusDepotMac;

// 맵: 맵 정보, 지도, 없는 오브젝트/스플라인/AI/사람 (OMSI 폴더 + openOMSI Mods 폴더 기준), AI 목록, HOF 보기
public class MapPage : UserControl {
    MapInfo info;
    readonly TextBlock pathText = Ui.Sub(""), status = Ui.Sub("");
    readonly StackPanel infoHost = new StackPanel(), lists = new StackPanel { Spacing = 4 }, hofHost = new StackPanel();
    readonly MapCanvas canvas = new MapCanvas();
    readonly ComboBox hofs = new ComboBox { Width = 360 };
    List<string[]> hofList = new List<string[]>();
    public const int MissingMax = 150;

    public MapPage() {
        Content = Ui.Page(
            Ui.Title(L.T("nav_map")),
            Ui.Sub(L.T("map_intro")),
            Ui.Caption(L.T("map_sec_file")),
            Ui.Card(Ui.Row(L.T("map_folder"), null, Ui.Btn(L.T("btn_browse"), async () => {
                string start = info != null ? info.Dir : (MacEnv.OmsiOk ? Path.Combine(MacEnv.S.OmsiDir, "maps") : "");
                string d = await Ui.PickFolder(L.T("map_folder"), start);
                if (d != null) LoadMap(d);
            })), pathText, status),
            infoHost,
            new Border { Child = canvas, CornerRadius = new CornerRadius(16), ClipToBounds = true, Margin = new Thickness(0, 10, 0, 0) },
            Ui.Sub(L.T("map_controls")),
            Ui.Caption(L.T("map_sec_layers")),
            Ui.Card(
                Ui.Row(L.T("map_show_splines"), L.T("map_show_splines_sub"), Ui.Check(true, v => { canvas.ShowSplines = v; canvas.InvalidateVisual(); })),
                Ui.Row(L.T("map_show_objects"), L.T("map_show_objects_sub"), Ui.Check(true, v => { canvas.ShowObjects = v; canvas.InvalidateVisual(); })),
                Ui.Row(L.T("map_missing_only"), L.T("map_missing_only_sub"), Ui.Check(false, v => { canvas.MissingOnly = v; canvas.InvalidateVisual(); }))),
            lists,
            Ui.Caption(L.T("map_sec_hof")),
            Ui.Card(Ui.Row(L.T("map_hof"), L.T("map_hof_sub"), hofs), hofHost));
        pathText.Text = L.T("map_none");
        hofs.SelectionChanged += (s, e) => { if (hofs.SelectedIndex >= 0 && hofs.SelectedIndex < hofList.Count) LoadHof(hofList[hofs.SelectedIndex][0]); };
        FindHofs();
    }

    public void LoadMap(string dir) {
        if (File.Exists(dir)) dir = Path.GetDirectoryName(dir);
        if (!File.Exists(Path.Combine(dir, "global.cfg"))) { Ui.Msg(L.T("map_err_noglobal")); return; }
        string root = MacEnv.OmsiOk ? MacEnv.S.OmsiDir : "";
        status.Text = L.T("map_loading", 0, 0);
        Thread th = new Thread(() => {
            MapInfo m = null; string err = null;
            try { m = MapInfo.Load(dir, root, (i, n) => Ui.Post(() => status.Text = L.T("map_loading", i, n))); } catch (Exception ex) { err = ex.Message; }
            Ui.Post(() => {
                status.Text = "";
                if (err != null) { Ui.Msg(err); return; }
                info = m; pathText.Text = dir;
                canvas.Map = m;
                Fill();
            });
        }) { IsBackground = true };
        th.Start();
    }

    void Fill() {
        infoHost.Children.Clear(); lists.Children.Clear();
        if (info == null) return;
        MapInfo m = info;
        Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        if (m.Picture != null) {
            try {
                Image img = new Image { Source = new Bitmap(m.Picture), Width = 250, Height = 160, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 20, 0) };
                g.Children.Add(new Border { Child = img, CornerRadius = new CornerRadius(12), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Top });
            } catch (Exception) { }
        }
        string years = m.YearFrom > 0 ? m.YearFrom + "–" + m.YearTo : "-";
        StackPanel t = new StackPanel { Spacing = 4 };
        t.Children.Add(Ui.Text(m.Friendly.Length > 0 ? m.Friendly : m.Name, 20, true));
        t.Children.Add(Ui.Sub(L.T("map_stats", m.Tiles.Count, m.Objects.Count.ToString("N0"), m.Splines.Count.ToString("N0"), years, m.Depot.Length > 0 ? m.Depot : "-", m.Lines, m.Trips)
            + (m.MissingTiles > 0 ? L.T("map_stats_tiles", m.MissingTiles) : "")));
        t.Children.Add(Ui.Text(m.Description, 13));
        Grid.SetColumn(t, 1); g.Children.Add(t);
        infoHost.Children.Add(new Border { Height = 10 });
        infoHost.Children.Add(Ui.Card(g));

        // 없는 파일
        int o = m.MissingObjects.Count, s = m.MissingSplines.Count, a = m.MissingAi.Count, h = m.MissingHumans.Count;
        StackPanel miss = new StackPanel();
        List<string[]> rows = new List<string[]>();
        foreach (UseItem u in m.MissingObjects) rows.Add(new[] { u.Path, L.T("map_miss_obj", u.Count) });
        foreach (UseItem u in m.MissingSplines) rows.Add(new[] { u.Path, L.T("map_miss_spl", u.Count) });
        foreach (UseItem u in m.MissingAi) rows.Add(new[] { u.Path, L.T("map_miss_ai", u.Count) });
        foreach (string x in m.MissingHumans) rows.Add(new[] { x, L.T("map_miss_human") });
        for (int i = 0; i < rows.Count && i < MissingMax; i++) miss.Children.Add(Pair(rows[i][0], rows[i][1]));
        if (rows.Count > MissingMax) miss.Children.Add(Pair(L.T("map_more", rows.Count - MissingMax), L.T("map_more_sub")));
        lists.Children.Add(Ui.Caption(L.T("map_sec_missing")));
        lists.Children.Add(Ui.Card(
            Ui.Row(L.T("map_missing"), o + s + a + h == 0 ? L.T("map_missing_none") : L.T("map_missing_stat", o, s, a, h), Ui.Btn(L.T("map_copy"), CopyMissing)),
            Ui.Sub(string.IsNullOrEmpty(MacEnv.S.ModsDir) ? "" : L.T("mac_map_mods", MacEnv.S.ModsDir)),
            miss));

        // AI / 사람
        StackPanel ai = new StackPanel();
        foreach (AiGroup grp in m.Ai) {
            int n = 0, missN = 0;
            foreach (UseItem v in grp.Vehicles) { n += v.Count; if (m.MissingAi.Exists(x => string.Equals(x.Path, v.Path, StringComparison.OrdinalIgnoreCase))) missN++; }
            List<string> names = new List<string>();
            foreach (UseItem v in grp.Vehicles) names.Add(Path.GetFileNameWithoutExtension(Obj2x.Omsi.Rel(v.Path)) + " ×" + v.Count);
            ai.Children.Add(Pair((grp.Name.Length > 0 ? grp.Name : grp.Kind) + " · " + L.T("map_ai_count", grp.Vehicles.Count, n) + (missN > 0 ? " · " + L.T("map_ai_miss", missN) : ""), string.Join(", ", names)));
        }
        lists.Children.Add(Ui.Caption(L.T("map_sec_ai")));
        lists.Children.Add(Ui.Card(ai));
        lists.Children.Add(Ui.Caption(L.T("map_sec_people")));
        lists.Children.Add(Ui.Card(Pair(L.T("map_humans", m.Humans.Count), Summ(m.Humans)), Pair(L.T("map_drivers", m.Drivers.Count), Summ(m.Drivers))));
    }

    static Control Pair(string a, string b) {
        StackPanel p = new StackPanel { Margin = new Thickness(0, 5) };
        p.Children.Add(new SelectableTextBlock { Text = a, FontSize = 13.5, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(b)) p.Children.Add(Ui.Sub(b));
        return p;
    }
    static string Summ(List<string> l) {
        List<string> n = new List<string>();
        foreach (string s in l) { n.Add(Path.GetFileNameWithoutExtension(Obj2x.Omsi.Rel(s))); if (n.Count >= 12) { n.Add("…"); break; } }
        return string.Join(", ", n);
    }

    async void CopyMissing() {
        if (info == null) return;
        StringBuilder sb = new StringBuilder();
        foreach (UseItem u in info.MissingObjects) sb.AppendLine(u.Path + "\t" + u.Count);
        foreach (UseItem u in info.MissingSplines) sb.AppendLine(u.Path + "\t" + u.Count);
        foreach (UseItem u in info.MissingAi) sb.AppendLine(u.Path + "\t" + u.Count);
        foreach (string h in info.MissingHumans) sb.AppendLine(h);
        TopLevel top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard != null) { await top.Clipboard.SetTextAsync(sb.ToString()); Ui.Msg(L.T("map_copied")); }
    }

    // ---- HOF: OMSI 2와 Mods 폴더들의 Vehicles/*/*.hof. 이름과 내용이 똑같으면 하나로
    void FindHofs() {
        List<string> roots = new List<string>();
        if (MacEnv.OmsiOk) roots.Add(MacEnv.S.OmsiDir);
        roots.AddRange(MapInfo.ExtraRoots);
        Thread th = new Thread(() => {
            List<string[]> r = HofList(roots);
            r.Sort((x, y) => string.Compare(x[1], y[1], StringComparison.CurrentCultureIgnoreCase));
            Ui.Post(() => {
                hofList = r; hofs.Items.Clear();
                foreach (string[] x in r) hofs.Items.Add(x[1]);
                if (r.Count == 0) hofHost.Children.Add(Ui.Sub(L.T("mac_hof_none")));
            });
        }) { IsBackground = true };
        th.Start();
    }
    public static List<string[]> HofList(List<string> roots) {
        List<string> keys = new List<string>();
        Dictionary<string, string> first = new(), nameOf = new();
        Dictionary<string, List<string>> dirs = new();
        using (MD5 md5 = MD5.Create())
            foreach (string root in roots) {
                string veh = Path.Combine(root, "Vehicles");
                if (!Directory.Exists(veh)) continue;
                string[] ds; try { ds = Directory.GetDirectories(veh); } catch (Exception) { continue; }
                foreach (string d in ds)
                    foreach (string f in SafeFiles(d, "*.hof")) {
                        string name, hash;
                        try { name = HofFile.ReadName(f); hash = Convert.ToBase64String(md5.ComputeHash(File.ReadAllBytes(f))); } catch (Exception) { continue; }
                        if (name.Length == 0) name = Path.GetFileNameWithoutExtension(f);
                        string key = name + "\n" + hash;
                        if (!first.ContainsKey(key)) { first[key] = f; nameOf[key] = name; dirs[key] = new List<string>(); keys.Add(key); }
                        dirs[key].Add(Path.GetFileName(d));
                    }
            }
        Dictionary<string, int> same = new(StringComparer.CurrentCultureIgnoreCase);
        foreach (string k in keys) { same.TryGetValue(nameOf[k], out int n); same[nameOf[k]] = n + 1; }
        List<string[]> res = new List<string[]>();
        foreach (string k in keys) {
            string label = nameOf[k];
            if (same[label] > 1) label += " · " + dirs[k][0] + (dirs[k].Count > 1 ? " +" + (dirs[k].Count - 1) : "");
            res.Add(new[] { first[k], label });
        }
        return res;
    }
    static string[] SafeFiles(string d, string pat) { try { return Directory.GetFiles(d, pat); } catch (Exception) { return new string[0]; } }

    void LoadHof(string p) {
        HofFile hof;
        try { hof = HofFile.Load(p); } catch (Exception ex) { Ui.Msg(ex.Message); return; }
        hofHost.Children.Clear();
        hofHost.Children.Add(Ui.Text(hof.Name, 17, true));
        hofHost.Children.Add(Ui.Sub(L.T("hof_stats", hof.Termini.Count, hof.Busstops.Count, hof.Trips.Count, hof.ServiceTrip) + "\n" + p));
        List<string> term = new List<string>();
        foreach (string[] r in hof.Termini) term.Add((r.Length > 2 ? r[2] : r.Length > 1 ? r[1] : r[0]).Trim());
        List<string> stops = new List<string>();
        foreach (string[] r in hof.Busstops) stops.Add(r[0].Trim());
        hofHost.Children.Add(Pair(L.T("hof_termini"), string.Join(" · ", term)));
        hofHost.Children.Add(Pair(L.T("hof_stops"), string.Join(" · ", stops)));
        hofHost.Children.Add(Ui.Text(L.T("hof_trips"), 14, true));
        foreach (HofTrip t in hof.Trips) hofHost.Children.Add(Pair(L.T("hof_trip", t.Line.Trim(), t.Code, t.Route, t.Stops.Count), string.Join(" → ", t.Stops)));
    }
}
