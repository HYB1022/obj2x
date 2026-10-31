using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Obj2x;

namespace BusDepotMac;

// 빠른 변환: Blender/OmsiXConv 없이 obj → o3d (FastO3D). 맥에서는 OmsiXConv를 쓸 수 없어서 이것만
public class ConvertPage : UserControl {
    readonly TextBlock inText = Ui.Sub(""), outText = Ui.Sub(""), status = Ui.Sub("");
    readonly StackPanel rows = new StackPanel { Spacing = 0 };
    readonly ProgressBar bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, Margin = new Thickness(0, 6) };
    Button run;
    volatile bool running;

    public ConvertPage() {
        Settings S = MacEnv.S;
        run = Ui.Btn(L.T("mac_conv_run"), Start, true);
        Content = Ui.Page(
            Ui.Title(L.T("mac_nav_convert")),
            Ui.Sub(L.T("mac_conv_intro")),
            Ui.Caption(L.T("wiz_confirm_input")),
            Ui.Card(
                Ui.Row(L.T("mac_conv_in"), null, Ui.Btn(L.T("btn_browse"), async () => { string d = await Ui.PickFolder(L.T("mac_conv_in"), S.Inp); if (d != null) { S.Inp = d; S.Save(); Refresh(); } })),
                inText,
                Ui.Row(L.T("mac_conv_same"), null, Ui.Check(S.SameDir, v => { S.SameDir = v; S.Save(); Refresh(); })),
                Ui.Row(L.T("mac_conv_out"), null, Ui.Btn(L.T("btn_browse"), async () => { string d = await Ui.PickFolder(L.T("mac_conv_out"), S.Out); if (d != null) { S.Out = d; S.SameDir = false; S.Save(); Refresh(); } })),
                outText,
                Ui.Row(L.T("mac_conv_sub"), null, Ui.Check(S.Sub, v => { S.Sub = v; S.Save(); })),
                Ui.Row(L.T("mac_conv_inc"), null, Ui.Check(S.Inc, v => { S.Inc = v; S.Save(); }))),
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 4, 0), Children = { run } },
            Ui.Caption(L.T("col_stat")),
            Ui.Card(status, bar, Header(), rows));
        Refresh();
    }

    string OutDir => MacEnv.S.SameDir || string.IsNullOrEmpty(MacEnv.S.Out) ? MacEnv.S.Inp : MacEnv.S.Out;

    void Refresh() {
        inText.Text = string.IsNullOrEmpty(MacEnv.S.Inp) ? L.T("mac_not_set") : MacEnv.S.Inp;
        outText.Text = string.IsNullOrEmpty(OutDir) ? L.T("mac_not_set") : OutDir;
    }

    static Grid Line(string a, string b, string c, string d, string e, string f, bool head, IBrush color = null) {
        Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,80,80,60,70,2*"), Margin = new Thickness(0, 3) };
        string[] t = { a, b, c, d, e, f };
        for (int i = 0; i < 6; i++) {
            TextBlock x = new TextBlock { Text = t[i], FontSize = 13, Opacity = head ? 0.6 : 1, TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = i >= 1 && i <= 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(i == 5 ? 14 : 0, 0, 0, 0) };
            if (color != null) x.Foreground = color;
            Grid.SetColumn(x, i); g.Children.Add(x);
        }
        return g;
    }
    static Grid Header() => Line(L.T("col_file"), L.T("col_vert"), L.T("col_tri"), L.T("col_mat"), L.T("col_time"), L.T("col_stat"), true);

    void Start() {
        Settings S = MacEnv.S;
        if (running) return;
        if (string.IsNullOrEmpty(S.Inp) || !Directory.Exists(S.Inp)) { Ui.Msg(L.T("mac_conv_in") + ": " + L.T("mac_not_set")); return; }
        List<ObjItem> objs = ObjList.Find(S.Inp, S.Sub);
        string outDir = OutDir;
        if (S.Inc) objs = ObjList.FilterChanged(objs, outDir, ".o3d");
        if (objs.Count == 0) { Ui.Msg(L.T("mac_conv_none")); return; }
        running = true; run.IsEnabled = false; rows.Children.Clear(); bar.Value = 0;
        Thread th = new Thread(() => {
            DateTime t0 = DateTime.Now; int ok = 0, warn = 0, fail = 0;
            for (int i = 0; i < objs.Count; i++) {
                ObjItem it = objs[i];
                string dst = Path.Combine(outDir, it.Rel + ".o3d");
                DateTime tf = DateTime.Now;
                string err = null, w = null; int nv = 0, nt = 0, nm = 0;
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    FastO3D.ConvertEx(it.Path, dst, out nv, out nt, out nm, out w);
                } catch (Exception ex) { err = ex.Message; }
                double sec = (DateTime.Now - tf).TotalSeconds;
                int kind = err != null ? 2 : string.IsNullOrEmpty(w) ? 0 : 1;
                if (kind == 0) ok++; else if (kind == 1) warn++; else fail++;
                int n = i + 1;
                Ui.Post(() => {
                    IBrush col = kind == 2 ? Brushes.IndianRed : kind == 1 ? Brushes.DarkOrange : null;
                    rows.Children.Add(Line(it.Rel + ".o3d", kind == 2 ? "" : nv.ToString("N0"), kind == 2 ? "" : nt.ToString("N0"), kind == 2 ? "" : nm.ToString(),
                        sec.ToString("0.00"), kind == 2 ? err : kind == 1 ? w : "OK", false, col));
                    bar.Value = (double)n / objs.Count;
                    status.Text = L.T("mac_conv_prog", n, objs.Count);
                });
            }
            double total = (DateTime.Now - t0).TotalSeconds;
            Ui.Post(() => { running = false; run.IsEnabled = true; status.Text = L.T("mac_conv_done", ok, warn, fail, total.ToString("0.0")); });
        }) { IsBackground = true };
        th.Start();
    }
}
