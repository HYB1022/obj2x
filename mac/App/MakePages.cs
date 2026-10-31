using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Obj2x;

namespace BusDepotMac;

// 오브젝트 만들기: o3d 폴더 → .sco + model/texture 복사. openOMSI는 OMSI 2 폴더에 쓰지 않으므로 기본 저장 위치는 Mods/Bus Depot
public class ObjectPage : UserControl {
    readonly StackPanel models = new StackPanel();
    readonly TextBlock modelsText = Ui.Sub(""), outText = Ui.Sub("");
    readonly List<(string path, CheckBox box)> items = new();
    readonly ScoOptions opt = new ScoOptions();

    public ObjectPage() {
        Settings S = MacEnv.S;
        opt.Combine = S.ObjCombine; opt.Group = S.ObjGroup; opt.Collision = S.ObjColl; opt.Surface = S.ObjSurf; opt.Shadow = S.ObjShadow; opt.Fixed = S.ObjFixed; opt.Alpha = S.ObjAlpha; opt.Name = S.ObjName;
        string[][] alpha = { new[] { "0", L.T("obj_alpha_off") }, new[] { "1", L.T("obj_alpha_cut") }, new[] { "2", L.T("obj_alpha_blend") } };
        Content = Ui.Page(
            Ui.Title(L.T("nav_object")),
            Ui.Sub(L.T("obj_intro")),
            Ui.Caption(L.T("obj_sec_models")),
            Ui.Card(Ui.Row(L.T("obj_models"), null, Ui.Btn(L.T("btn_browse"), async () => {
                string d = await Ui.PickFolder(L.T("obj_models"), S.ObjModels);
                if (d != null) { S.ObjModels = d; if (string.IsNullOrEmpty(S.ObjFolder)) S.ObjFolder = Path.GetFileName(d); S.Save(); Refresh(); }
            })), modelsText, models),
            Ui.Caption(L.T("obj_sec_object")),
            Ui.Card(
                Ui.Row(L.T("obj_mode"), L.T("obj_mode_sub"), Ui.Choice(new[] { new[] { "each", L.T("obj_mode_each") }, new[] { "one", L.T("obj_mode_one") } }, opt.Combine ? "one" : "each", v => { opt.Combine = v == "one"; S.ObjCombine = opt.Combine; S.Save(); })),
                Ui.Row(L.T("obj_name"), L.T("obj_name_sub"), Ui.Edit(opt.Name, v => { opt.Name = v; S.ObjName = v; S.Save(); })),
                Ui.Row(L.T("obj_group"), L.T("obj_group_sub"), Ui.Edit(opt.Group, v => { opt.Group = v; S.ObjGroup = v; S.Save(); }))),
            Ui.Caption(L.T("obj_sec_props")),
            Ui.Card(
                Ui.Row(L.T("obj_coll"), L.T("obj_coll_sub"), Ui.Check(opt.Collision, v => { opt.Collision = v; S.ObjColl = v; S.Save(); })),
                Ui.Row(L.T("obj_surf"), L.T("obj_surf_sub"), Ui.Check(opt.Surface, v => { opt.Surface = v; S.ObjSurf = v; S.Save(); })),
                Ui.Row(L.T("obj_shadow"), L.T("obj_shadow_sub"), Ui.Check(opt.Shadow, v => { opt.Shadow = v; S.ObjShadow = v; S.Save(); })),
                Ui.Row(L.T("obj_fixed"), L.T("obj_fixed_sub"), Ui.Check(opt.Fixed, v => { opt.Fixed = v; S.ObjFixed = v; S.Save(); })),
                Ui.Row(L.T("obj_alpha"), null, Ui.Combo(alpha, opt.Alpha.ToString(), v => { opt.Alpha = int.Parse(v); S.ObjAlpha = opt.Alpha; S.Save(); }))),
            Ui.Caption(L.T("obj_sec_save")),
            Ui.Card(Ui.Row(L.T("obj_folder"), null, Ui.Edit(S.ObjFolder, v => { S.ObjFolder = v; S.Save(); Out(); })), outText),
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 4, 0), Children = { Ui.Btn(L.T("obj_make"), Make, true) } });
        Refresh();
    }

    string OutDir => Path.Combine(MacEnv.MakeRoot, "Sceneryobjects", Omsi.SafeName(MacEnv.S.ObjFolder ?? ""));
    void Out() { outText.Text = string.IsNullOrEmpty(MacEnv.MakeRoot) ? L.T("omsi_notfound") : L.T("mac_make_root", OutDir); }

    void Refresh() {
        Settings S = MacEnv.S;
        models.Children.Clear(); items.Clear();
        string[] files = Directory.Exists(S.ObjModels) ? Directory.GetFiles(S.ObjModels, "*.o3d") : new string[0];
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        modelsText.Text = !Directory.Exists(S.ObjModels) ? L.T("obj_models_none") : L.T("obj_models_count", files.Length);
        foreach (string f in files) {
            int tex = 0; try { tex = Omsi.O3dTextures(f).Count; } catch (Exception) { }
            CheckBox c = new CheckBox { IsChecked = true };
            models.Children.Add(Ui.Row(Path.GetFileName(f), L.T("obj_model_sub", tex, 0), c));
            items.Add((f, c));
        }
        Out();
    }

    void Make() {
        List<string> sel = new List<string>();
        foreach (var it in items) if (it.box.IsChecked == true) sel.Add(it.path);
        if (sel.Count == 0) { Ui.Msg(L.T("obj_err_none")); return; }
        if (string.IsNullOrWhiteSpace(MacEnv.S.ObjFolder)) { Ui.Msg(L.T("obj_out_needname")); return; }
        if (string.IsNullOrEmpty(MacEnv.MakeRoot)) { Ui.Msg(L.T("omsi_notfound")); return; }
        try {
            string outDir = OutDir;
            Directory.CreateDirectory(outDir);
            ScoResult r = ScoMaker.Make(sel, outDir, opt);
            string body = L.T("obj_done_body", r.Sco.Count, r.Copied.Count, outDir);
            if (r.Missing.Count > 0) body += "\n\n" + L.T("obj_done_missing", r.Missing.Count) + "\n" + string.Join("\n", r.Missing.GetRange(0, Math.Min(8, r.Missing.Count)));
            Ui.Msg(body, L.T("obj_done_title"));
        } catch (Exception ex) { Ui.Msg(ex.Message); }
    }
}

// 스플라인 만들기: 차선/인도 값 → .sli (+ 기본 텍스처). 미리보기는 위에서 본 도로와 단면
public class SplinePage : UserControl {
    readonly SplineSpec spec;
    readonly SplinePreview preview = new SplinePreview();
    readonly TextBlock summary = Ui.Sub(""), err = new TextBlock { Foreground = Brushes.IndianRed, FontSize = 12.5 }, outText = Ui.Sub("");

    public SplinePage() {
        Settings S = MacEnv.S;
        spec = SplineSpec.Unpack(S.Spl);
        preview.Spec = spec;
        Content = Ui.Page(
            Ui.Title(L.T("nav_spline")),
            Ui.Sub(L.T("spl_intro")),
            Ui.Caption(L.T("spl_sec_preview")),
            Ui.Card(preview, summary, err),
            Ui.Caption(L.T("spl_sec_road")),
            Ui.Card(
                Ui.Row(L.T("spl_lanes"), L.T("spl_lanes_sub"), Ui.Num(spec.Lanes, v => { spec.Lanes = (int)Math.Round(v); Changed(); }, 70)),
                Ui.Row(L.T("spl_lanew"), null, Ui.Num(spec.LaneW, v => { spec.LaneW = v; Changed(); }, 70)),
                Ui.Row(L.T("spl_dir"), L.T("spl_dir_sub"), Ui.Choice(new[] { new[] { "two", L.T("spl_two") }, new[] { "one", L.T("spl_one") } }, spec.OneWay ? "one" : "two", v => { spec.OneWay = v == "one"; Changed(); })),
                Ui.Row(L.T("spl_extra"), L.T("spl_extra_sub"), Ui.Choice(new[] { new[] { "l", L.T("spl_extra_left") }, new[] { "r", L.T("spl_extra_right") } }, spec.ExtraRight ? "r" : "l", v => { spec.ExtraRight = v == "r"; Changed(); })),
                Ui.Row(L.T("spl_fit"), L.T("spl_fit_sub"), Ui.Choice(new[] { new[] { "road", L.T("spl_fit_road") }, new[] { "lane", L.T("spl_fit_lane") } }, spec.PerLane ? "lane" : "road", v => { spec.PerLane = v == "lane"; Changed(); })),
                Ui.Row(L.T("spl_repeat"), L.T("spl_repeat_sub"), Ui.Num(spec.RoadRepeat, v => { spec.RoadRepeat = v; Changed(); }, 70))),
            Ui.Caption(L.T("spl_sec_walk")),
            Ui.Card(
                Ui.Row(L.T("spl_walkl"), L.T("spl_walk_sub"), Ui.Num(spec.WalkL, v => { spec.WalkL = v; Changed(); }, 70)),
                Ui.Row(L.T("spl_walkr"), L.T("spl_walk_sub"), Ui.Num(spec.WalkR, v => { spec.WalkR = v; Changed(); }, 70)),
                Ui.Row(L.T("spl_curb"), null, Ui.Num(spec.Curb, v => { spec.Curb = v; Changed(); }, 70)),
                Ui.Row(L.T("spl_tile"), L.T("spl_tile_sub"), Ui.Num(spec.WalkRepeat, v => { spec.WalkRepeat = v; Changed(); }, 70))),
            Ui.Caption(L.T("spl_sec_save")),
            Ui.Card(
                Ui.Row(L.T("spl_folder"), L.T("spl_folder_sub"), Ui.Edit(S.SplFolder, v => { S.SplFolder = v; S.Save(); Out(); })),
                Ui.Row(L.T("spl_name"), null, Ui.Edit(S.SplName, v => { S.SplName = v; S.Save(); })),
                outText),
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 4, 0), Children = { Ui.Btn(L.T("spl_make"), Make, true) } });
        Changed(); Out();
    }

    string OutDir => Path.Combine(MacEnv.MakeRoot, "Splines", Omsi.SafeName(MacEnv.S.SplFolder ?? ""));
    void Out() { outText.Text = string.IsNullOrEmpty(MacEnv.MakeRoot) ? L.T("omsi_notfound") : L.T("mac_make_root", OutDir); }

    void Changed() {
        string e;
        bool ok = spec.Valid(out e);
        err.Text = ok ? "" : L.T(e);
        summary.Text = L.T("spl_summary", Ui.N(spec.TotalW), Ui.N(spec.RoadW), spec.Lanes, spec.Paths().Count);
        if (ok) { MacEnv.S.Spl = spec.Pack(); MacEnv.S.Save(); }
        preview.InvalidateVisual();
    }

    void Make() {
        string e;
        if (!spec.Valid(out e)) { Ui.Msg(L.T(e)); return; }
        if (string.IsNullOrWhiteSpace(MacEnv.S.SplName) || string.IsNullOrWhiteSpace(MacEnv.S.SplFolder)) { Ui.Msg(L.T("spl_out_needname")); return; }
        if (string.IsNullOrEmpty(MacEnv.MakeRoot)) { Ui.Msg(L.T("omsi_notfound")); return; }
        try {
            string f = spec.Save(OutDir, MacEnv.S.SplName);
            Ui.Msg(L.T("spl_done_body", f), L.T("spl_done_title"));
        } catch (Exception ex) { Ui.Msg(ex.Message); }
    }
}

// 위에서 본 도로(인도, 차선, 방향 화살표)와 폭 표시
public class SplinePreview : Control {
    public SplineSpec Spec;
    public SplinePreview() { Height = 190; }
    public override void Render(DrawingContext dc) {
        SplineSpec sp = Spec;
        double W = Bounds.Width;
        if (sp == null || W < 50) return;
        double total = Math.Max(0.1, sp.TotalW), pad = 8, k = (W - pad * 2) / total, top = 6, bot = 130;
        IBrush walk = new SolidColorBrush(Ui.Dark ? Color.FromRgb(104, 100, 94) : Color.FromRgb(214, 208, 198));
        IBrush asph = new SolidColorBrush(Ui.Dark ? Color.FromRgb(62, 64, 70) : Color.FromRgb(78, 80, 86));
        double xl = pad, xr0 = pad + sp.WalkL * k, xr1 = xr0 + sp.RoadW * k;
        dc.DrawRectangle(walk, null, new Rect(xl, top, sp.WalkL * k, bot - top));
        dc.DrawRectangle(walk, null, new Rect(xr1, top, sp.WalkR * k, bot - top));
        dc.DrawRectangle(asph, null, new Rect(xr0, top, sp.RoadW * k, bot - top));
        Pen white = new Pen(new SolidColorBrush(Color.FromArgb(220, 240, 240, 240)), 2), yellow = new Pen(new SolidColorBrush(Color.FromRgb(250, 200, 40)), 2);
        dc.DrawLine(white, new Point(xr0 + 3, top), new Point(xr0 + 3, bot));
        dc.DrawLine(white, new Point(xr1 - 3, top), new Point(xr1 - 3, bot));
        int back = sp.BackLanes;
        for (int i = 1; i < sp.Lanes; i++) {
            double lx = xr0 + i * sp.LaneW * k;
            if (i == back) { dc.DrawLine(yellow, new Point(lx - 2.5, top), new Point(lx - 2.5, bot)); dc.DrawLine(yellow, new Point(lx + 2.5, top), new Point(lx + 2.5, bot)); }
            else for (double y = top + 6; y < bot; y += 26) dc.DrawLine(white, new Point(lx, y), new Point(lx, Math.Min(bot, y + 13)));
        }
        Pen arrow = new Pen(Brushes.White, 2.4);
        Pen walkPath = new Pen(new SolidColorBrush(Color.FromRgb(80, 70, 229)), 2.2);
        foreach (SplineSpec.PathDef pd in sp.Paths()) {
            double px = pad + (pd.X + sp.RoadW / 2 + sp.WalkL) * k, cy = (top + bot) / 2;
            if (pd.Kind == 0) {
                double d = pd.Dir == 0 ? -1 : 1;   // 정방향은 위로
                dc.DrawLine(arrow, new Point(px, cy - 18 * d), new Point(px, cy + 18 * d));
                dc.DrawLine(arrow, new Point(px, cy + 18 * d), new Point(px - 7, cy + 10 * d));
                dc.DrawLine(arrow, new Point(px, cy + 18 * d), new Point(px + 7, cy + 10 * d));
            } else for (double y = top + 8; y < bot - 4; y += 12) dc.DrawLine(walkPath, new Point(px, y), new Point(px, y + 3));
        }
        // 폭
        IBrush mut = new SolidColorBrush(Color.FromArgb(160, 128, 128, 140));
        List<double[]> parts = new List<double[]>();
        if (sp.WalkL > 0) parts.Add(new[] { xl, xr0, sp.WalkL });
        if (sp.Lanes <= 4) for (int i = 0; i < sp.Lanes; i++) parts.Add(new[] { xr0 + i * sp.LaneW * k, xr0 + (i + 1) * sp.LaneW * k, sp.LaneW }); else parts.Add(new[] { xr0, xr1, sp.RoadW });
        if (sp.WalkR > 0) parts.Add(new[] { xr1, xr1 + sp.WalkR * k, sp.WalkR });
        Pen tick = new Pen(mut, 1);
        foreach (double[] pt in parts) {
            dc.DrawLine(tick, new Point(pt[0] + 1, bot + 12), new Point(pt[1] - 1, bot + 12));
            FormattedText ft = new FormattedText(pt[2].ToString("0.##", CultureInfo.InvariantCulture) + " m", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, mut);
            if (ft.Width < pt[1] - pt[0] - 4) dc.DrawText(ft, new Point((pt[0] + pt[1] - ft.Width) / 2, bot + 16));
        }
    }
}
