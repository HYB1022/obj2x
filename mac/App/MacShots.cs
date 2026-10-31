using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace BusDepotMac;

// 개발용: --shots <폴더> [버스 파일] [맵 폴더] — 페이지마다 창 내용을 PNG로 저장하고 끝난다 (Windows에서 화면 확인용)
public static class MacShots {
    public static string Dir, Bus, Map;

    public static void Start(IClassicDesktopStyleApplicationLifetime life) {
        Directory.CreateDirectory(Dir);
        MainWindow w = new MainWindow { Position = new PixelPoint(-4000, 0), ShowInTaskbar = false };
        life.MainWindow = w;
        w.Show();
        string[] pages = { "convert", "bus", "map", "object", "spline", "settings", "about" };
        int i = 0, wait = 0;
        DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        t.Tick += (s, e) => {
            if (i >= pages.Length) { t.Stop(); life.Shutdown(); return; }
            if (wait == 0) {
                w.Show(pages[i]);
                if (pages[i] == "bus" && Bus != null) w.Page<BusPage>("bus").Load(Bus);
                if (pages[i] == "map" && Map != null) w.Page<MapPage>("map").LoadMap(Map);
            }
            wait++;
            if (wait < (pages[i] == "map" ? 12 : 3)) return;
            Save(w, Path.Combine(Dir, (i + 1).ToString("00") + "_" + pages[i] + ".png"));
            // 긴 페이지는 아래도 한 장
            if (w.Content is Grid g && FindScroll(g) is ScrollViewer sv && sv.Extent.Height > sv.Viewport.Height + 50) {
                sv.Offset = new Vector(0, Math.Min(sv.Extent.Height - sv.Viewport.Height, sv.Viewport.Height * 0.9));
                Dispatcher.UIThread.RunJobs();
                w.UpdateLayout();
                Save(w, Path.Combine(Dir, (i + 1).ToString("00") + "_" + pages[i] + "_2.png"));
            }
            i++; wait = 0;
        };
        t.Start();
    }

    static ScrollViewer FindScroll(Control c) {
        foreach (Visual v in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(c))
            if (v is ScrollViewer sv && sv.IsVisible && sv.Bounds.Width > 400) return sv;
        return null;
    }

    static void Save(Window w, string path) {
        PixelSize size = new PixelSize((int)w.Bounds.Width, (int)w.Bounds.Height);
        using RenderTargetBitmap rtb = new RenderTargetBitmap(size);
        rtb.Render(w);
        rtb.Save(path);
    }
}
