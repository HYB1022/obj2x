using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Obj2x;

namespace BusDepotMac;

// 버스: .bus를 열어 부품, [newanim] 블록(피벗·회전·factor), paths.cfg, passengercabin.cfg를 숫자로 고치고 저장.
// 맥 버전에는 3D 보기가 없다 (Windows 버전에서 3D로 확인)
public class BusPage : UserControl {
    BusFile bus; ModelCfg cfg; PathsCfg paths; CabinCfg cabin;
    string path = "";
    readonly StackPanel body = new StackPanel { Spacing = 4 };
    readonly TextBlock fileText = Ui.Sub("");

    public BusPage() {
        Content = Ui.Page(
            Ui.Title(L.T("nav_bus")),
            Ui.Sub(L.T("bus_intro")),
            Ui.Sub(L.T("mac_bus_no3d")),
            Ui.Caption(L.T("view_sec_file")),
            Ui.Card(Ui.Row(L.T("bus_file"), null, Ui.Btn(L.T("btn_browse"), async () => {
                string start = path.Length > 0 ? path : (MacEnv.OmsiOk ? Path.Combine(MacEnv.S.OmsiDir, "Vehicles") : "");
                string f = await Ui.PickFile(L.T("bus_file"), "OMSI", new[] { "*.bus", "*.ovh", "*.cfg", "*.sco" }, start);
                if (f != null) Load(f);
            })), fileText),
            body);
        fileText.Text = L.T("view_none");
    }

    public void Load(string p) {
        bus = null; paths = null; cabin = null; cfg = null;
        string model = p, ext = Path.GetExtension(p).ToLowerInvariant();
        try {
            if (ext == ".bus" || ext == ".ovh") {
                bus = BusFile.Load(p); model = bus.Model;
                if (File.Exists(bus.Paths)) paths = PathsCfg.Load(bus.Paths);
                if (File.Exists(bus.Cabin)) cabin = CabinCfg.Load(bus.Cabin);
            }
            if (!File.Exists(model)) { Ui.Msg(L.T("bus_err_model", model)); return; }
            cfg = ModelCfg.Load(model);
        } catch (Exception ex) { Ui.Msg(ex.Message); return; }
        path = p;
        fileText.Text = p;
        Build();
    }

    string Rel(string f) => string.IsNullOrEmpty(f) ? "-" : bus != null && f.StartsWith(bus.Dir, StringComparison.OrdinalIgnoreCase) ? f.Substring(bus.Dir.Length).TrimStart('\\', '/') : f;

    void Build() {
        body.Children.Clear();
        if (cfg == null) return;
        int anim = cfg.Meshes.FindAll(m => m.Animated).Count;
        body.Children.Add(Ui.Card(
            Ui.Text(bus != null ? bus.Name : Path.GetFileName(cfg.CfgPath), 18, true),
            Ui.Sub(bus != null ? L.T("bus_files", Rel(bus.Model), Rel(bus.Paths), Rel(bus.Cabin)) : cfg.CfgPath),
            Ui.Sub(L.T("mac_parts", cfg.Meshes.Count, anim))));

        // 부품 목록 → 고르면 아래에 블록 편집
        body.Children.Add(Ui.Caption(L.T("view_sec_parts")));
        StackPanel blocks = new StackPanel();
        ListBox list = new ListBox { MaxHeight = 320, Background = Brushes.Transparent };
        foreach (CfgMesh m in cfg.Meshes) {
            string sub = (m.Exists ? "" : L.T("view_part_nofile") + " · ") + (m.VisVar != null ? "[visible] " + m.VisVar + " = " + m.VisVal + " · " : "")
                + (m.Lod > 0 ? "LOD " + Ui.N(m.LodValue) + " · " : "") + (m.Anims.Count > 0 ? L.T("bus_block", m.Anims.Count) : "");
            StackPanel it = new StackPanel { Tag = m };
            it.Children.Add(Ui.Text(m.File));
            if (sub.Length > 0) it.Children.Add(Ui.Sub(sub.TrimEnd(' ', '·')));
            list.Items.Add(it);
        }
        list.SelectionChanged += (s, e) => { if (list.SelectedItem is StackPanel sp && sp.Tag is CfgMesh m) Blocks(blocks, m); };
        body.Children.Add(Ui.Card(list));
        body.Children.Add(blocks);

        if (paths != null) Paths();
        if (cabin != null) Cabin();
    }

    void Blocks(StackPanel host, CfgMesh m) {
        host.Children.Clear();
        CfgDoc doc = cfg.Doc;
        host.Children.Add(Ui.Caption(L.T("bus_sec_part") + " · " + m.File));
        if (m.Anims.Count == 0) { host.Children.Add(Ui.Card(Ui.Sub(L.T("bus_sel_static")))); return; }
        for (int i = 0; i < m.Anims.Count; i++) {
            AnimBlock b = m.Anims[i];
            StackPanel card = new StackPanel { Spacing = 4 };
            card.Children.Add(Ui.Text(L.T("bus_block", i + 1) + (b.FromMesh ? " · origin_from_mesh" : ""), 14, true));
            card.Children.Add(Triple(L.T("bus_pivot"), c => b.Trans[c], (c, v) => { b.Trans[c] = v; doc.Set(b.TransLine[c], v); }, c => b.TransLine[c] >= 0));
            card.Children.Add(Triple(L.T("bus_rot"), c => b.Rot[c], (c, v) => { b.Rot[c] = v; doc.Set(b.RotLine[c], v); }, c => b.RotLine[c] >= 0));
            if (b.RotLine[0] < 0 && b.RotLine[1] < 0 && b.RotLine[2] < 0) card.Children.Add(Ui.Sub(L.T("bus_block_norot")));
            foreach (AnimEntry e in b.Entries) {
                AnimEntry ee = e;
                card.Children.Add(Ui.Row(e.Kind + " " + e.Var, L.T("bus_factor"), Ui.Num(e.Factor, v => { ee.Factor = v; doc.Set(ee.Line, v); })));
            }
            host.Children.Add(Ui.Card(card));
            host.Children.Add(new Border { Height = 8 });
        }
        host.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { Ui.Btn(L.T("bus_save_cfg"), () => Save(doc), true) } });
    }

    static Control Triple(string label, Func<int, double> get, Action<int, double> set, Func<int, bool> can) {
        StackPanel p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        string[] ax = { "x", "y", "z" };
        for (int c = 0; c < 3; c++) {
            int cc = c;
            p.Children.Add(new TextBlock { Text = ax[c], VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
            p.Children.Add(Ui.Num(get(c), v => set(cc, v), 90, can(c)));
        }
        return Ui.Row(label, null, p);
    }

    void Save(CfgDoc doc) {
        if (doc == null) return;
        try { doc.Save(); Ui.Msg(L.T("bus_saved", doc.Path)); } catch (Exception ex) { Ui.Msg(ex.Message); }
    }

    // ---- paths.cfg: 점 x/y/z, 연결
    readonly StackPanel pathsHost = new StackPanel(), cabinHost = new StackPanel();
    void Paths() {
        if (!body.Children.Contains(pathsHost)) body.Children.Add(pathsHost);
        pathsHost.Children.Clear();
        pathsHost.Children.Add(Ui.Caption(L.T("bus_sec_paths")));
        StackPanel pts = new StackPanel { Spacing = 2 };
        HashSet<int> en = new HashSet<int>(cabin != null ? cabin.Entries : new List<int>()), ex = new HashSet<int>(cabin != null ? cabin.Exits : new List<int>());
        foreach (PathPoint pt in paths.Points) {
            PathPoint p = pt;
            string t = p.Index + (en.Contains(p.Index) ? " · " + L.T("bus_entry") : "") + (ex.Contains(p.Index) ? " · " + L.T("bus_exit") : "");
            pts.Children.Add(Triple(t, c => p.P[c], (c, v) => paths.SetPoint(p, c, v), c => true));
        }
        WrapPanel links = new WrapPanel();
        foreach (PathLink lk in paths.Links) {
            PathLink l = lk;
            links.Children.Add(Ui.Btn(l.A + " → " + l.B + "  ✕", () => { paths.RemoveLink(l); Paths(); }));
        }
        double a = 0, b = 1;
        pathsHost.Children.Add(Ui.Card(
            Ui.Sub(L.T("mac_paths_count", paths.Points.Count, paths.Links.Count)),
            pts,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8), Children = {
                Ui.Btn(L.T("bus_add_point"), () => { double[] l = paths.Points.Count > 0 ? paths.Points[paths.Points.Count - 1].P : new double[3]; paths.AddPoint(l[0], l[1] - 0.5, l[2]); Paths(); }) } },
            Ui.Row(L.T("bus_links"), L.T("bus_links_sub"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                Ui.Num(0, v => a = v, 60), new TextBlock { Text = "→", VerticalAlignment = VerticalAlignment.Center }, Ui.Num(1, v => b = v, 60),
                Ui.Btn(L.T("bus_add_link"), () => {
                    int ia = (int)a, ib = (int)b;
                    if (ia == ib || ia < 0 || ib < 0 || ia >= paths.Points.Count || ib >= paths.Points.Count) { Ui.Msg(L.T("bus_err_link", paths.Points.Count - 1)); return; }
                    paths.AddLink(ia, ib); Paths();
                }) } }),
            links,
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { Ui.Btn(L.T("bus_save"), () => Save(paths.Doc), true) } }));
    }

    // ---- passengercabin.cfg: 좌석 x/y/z/높이/회전
    void Cabin() {
        if (!body.Children.Contains(cabinHost)) body.Children.Add(cabinHost);
        cabinHost.Children.Clear();
        cabinHost.Children.Add(Ui.Caption(L.T("bus_sec_cabin")));
        StackPanel seats = new StackPanel { Spacing = 2 };
        int n = 0;
        string[] lab = { "x", "y", "z", L.T("bus_seat_h"), L.T("bus_seat_rot") };
        foreach (Seat st in cabin.Seats) {
            Seat s = st;
            StackPanel p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            for (int c = 0; c < 5; c++) {
                int cc = c;
                p.Children.Add(new TextBlock { Text = lab[c], VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
                p.Children.Add(Ui.Num(s.V[c], v => cabin.SetSeat(s, cc, v), 64));
            }
            if (!s.Driver) p.Children.Add(Ui.Btn("✕", () => { cabin.RemoveSeat(s); Cabin(); }));
            seats.Children.Add(Ui.Row(s.Driver ? L.T("bus_driver") : L.T("bus_seat", ++n), null, p));
        }
        cabinHost.Children.Add(Ui.Card(
            Ui.Sub(CabinStat()),
            seats,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = {
                Ui.Btn(L.T("bus_add_seat"), () => { double[] l = cabin.Seats.Count > 0 ? cabin.Seats[cabin.Seats.Count - 1].V : new double[5]; cabin.AddSeat(l[0], l[1] - 0.8, l[2]); Cabin(); }),
                Ui.Btn(L.T("bus_save"), () => Save(cabin.Doc), true) } }));
    }
    string CabinStat() {
        int drv = cabin.Seats.FindAll(s => s.Driver).Count;
        return L.T("bus_cabin_stat", cabin.Seats.Count - drv, drv, string.Join(", ", cabin.Entries), string.Join(", ", cabin.Exits));
    }
}
