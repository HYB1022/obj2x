using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Obj2x;

namespace BusDepotMac;

// 맵 지도 (Windows 버전 MapView.cs와 같은 방식): 그릴 때마다 화면에 보이는 것만 화면 좌표로 만들고,
// 오브젝트는 3px 칸마다 하나만, 스플라인은 2px보다 짧은 마디를 건너뛴다. 끌어서 이동, 휠로 확대, 두 번 눌러 전체 보기
public class MapCanvas : Control {
    MapInfo map;
    public bool ShowObjects = true, ShowSplines = true, MissingOnly;
    class SplPath { public Point[] P; public Rect Box; public bool Missing; }
    List<SplPath> spl = new List<SplPath>();
    double scale = 1, cx, cy;
    Point last; bool drag, fitted;

    public MapCanvas() { ClipToBounds = true; Height = 520; Cursor = new Cursor(StandardCursorType.SizeAll); }

    public MapInfo Map {
        get => map;
        set { map = value; Build(); fitted = false; Fit(); }
    }

    void Build() {
        spl = new List<SplPath>();
        if (map == null) return;
        foreach (MapSpline s in map.Splines) {
            Point[] p = Points(s);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Point q in p) { x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y); }
            spl.Add(new SplPath { P = p, Box = new Rect(x0, y0, Math.Max(0.01, x1 - x0), Math.Max(0.01, y1 - y0)), Missing = s.Missing });
        }
    }
    static Point[] Points(MapSpline s) {
        double a = s.Dir * Math.PI / 180, x = s.X, y = s.Y;
        if (Math.Abs(s.Rad) < 0.01 || s.Len <= 0) return new[] { new Point(x, y), new Point(x + Math.Sin(a) * s.Len, y + Math.Cos(a) * s.Len) };
        int n = Math.Max(2, Math.Min(32, (int)(s.Len / 5)));
        Point[] p = new Point[n + 1]; p[0] = new Point(x, y);
        double step = s.Len / n, turn = step / s.Rad;
        for (int i = 0; i < n; i++) { a += turn / 2; x += Math.Sin(a) * step; y += Math.Cos(a) * step; a += turn / 2; p[i + 1] = new Point(x, y); }
        return p;
    }

    void Fit() {
        if (map == null || map.Tiles.Count == 0 || Bounds.Width < 10) { InvalidateVisual(); return; }
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (MapTile t in map.Tiles) { x0 = Math.Min(x0, t.X * 300.0); y0 = Math.Min(y0, t.Y * 300.0); x1 = Math.Max(x1, t.X * 300.0 + 300); y1 = Math.Max(y1, t.Y * 300.0 + 300); }
        cx = (x0 + x1) / 2; cy = (y0 + y1) / 2;
        scale = Math.Min(Bounds.Width / (x1 - x0), Bounds.Height / (y1 - y0)) * 0.92;
        fitted = true;
        InvalidateVisual();
    }
    protected override void OnSizeChanged(SizeChangedEventArgs e) { base.OnSizeChanged(e); if (!fitted) Fit(); }

    double SX(double x) => Bounds.Width / 2 + (x - cx) * scale;
    double SY(double y) => Bounds.Height / 2 - (y - cy) * scale;

    static readonly IBrush Red = new SolidColorBrush(Color.FromRgb(239, 68, 68));
    public override void Render(DrawingContext dc) {
        double w = Bounds.Width, h = Bounds.Height;
        bool dark = Ui.Dark;
        dc.DrawRectangle(new SolidColorBrush(dark ? Color.FromRgb(22, 24, 30) : Color.FromRgb(236, 239, 244)), null, new Rect(0, 0, w, h), 12, 12);
        if (map == null) return;
        double pad = 8 / scale;
        Rect view = new Rect(cx - w / 2 / scale - pad, cy - h / 2 / scale - pad, w / scale + 2 * pad, h / scale + 2 * pad);

        StreamGeometry t = new StreamGeometry(), tm = new StreamGeometry();
        using (StreamGeometryContext a = t.Open()) using (StreamGeometryContext b = tm.Open())
            foreach (MapTile tile in map.Tiles) {
                double x = tile.X * 300.0, y = tile.Y * 300.0;
                if (x > view.Right || x + 300 < view.Left || y > view.Bottom || y + 300 < view.Top) continue;
                Box(tile.Exists ? a : b, SX(x), SY(y + 300), 300 * scale, 300 * scale);
            }
        dc.DrawGeometry(new SolidColorBrush(dark ? Color.FromRgb(30, 33, 41) : Colors.White), new Pen(new SolidColorBrush(dark ? Color.FromRgb(44, 48, 58) : Color.FromRgb(214, 219, 228)), 1), t);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(60, 239, 68, 68)), new Pen(Red, 1), tm);

        if (ShowSplines) {
            StreamGeometry s = new StreamGeometry(), sm = new StreamGeometry();
            using (StreamGeometryContext a = s.Open()) using (StreamGeometryContext b = sm.Open())
                foreach (SplPath p in spl) {
                    if (MissingOnly && !p.Missing) continue;
                    if (!p.Box.Intersects(view)) continue;
                    StreamGeometryContext c = p.Missing ? b : a;
                    Point prev = new Point(SX(p.P[0].X), SY(p.P[0].Y));
                    c.BeginFigure(prev, false);
                    for (int i = 1; i < p.P.Length; i++) {
                        Point q = new Point(SX(p.P[i].X), SY(p.P[i].Y));
                        if (i < p.P.Length - 1 && Math.Abs(q.X - prev.X) + Math.Abs(q.Y - prev.Y) < 2) continue;
                        c.LineTo(q); prev = q;
                    }
                    c.EndFigure(false);
                }
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(dark ? Color.FromRgb(150, 156, 170) : Color.FromRgb(96, 104, 120)), Math.Max(1.5, Math.Min(4, 1.6 * scale))), s);
            dc.DrawGeometry(null, new Pen(Red, Math.Max(2.5, Math.Min(5, 2.4 * scale))), sm);
        }
        if (ShowObjects) {
            StreamGeometry o = new StreamGeometry(), om = new StreamGeometry();
            HashSet<long> seen = new HashSet<long>(), seenM = new HashSet<long>();
            using (StreamGeometryContext a = o.Open()) using (StreamGeometryContext b = om.Open())
                foreach (MapObj ob in map.Objects) {
                    if (MissingOnly && !ob.Missing) continue;
                    if (!view.Contains(new Point(ob.X, ob.Y))) continue;
                    double x = SX(ob.X), y = SY(ob.Y);
                    long key = ((long)Math.Floor(x / 3) << 32) ^ (uint)(int)Math.Floor(y / 3);
                    if (!(ob.Missing ? seenM : seen).Add(key)) continue;
                    if (ob.Missing) Box(b, x - 2.5, y - 2.5, 5, 5); else Box(a, x - 1.5, y - 1.5, 3, 3);
                }
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(217, 80, 70, 229)), null, o);
            dc.DrawGeometry(Red, null, om);
        }
        // 축척 막대
        double m100 = 100 * scale; string lab = "100 m";
        if (m100 < 40) { m100 = 1000 * scale; lab = "1 km"; }
        if (m100 > 260) { m100 = 10 * scale; lab = "10 m"; }
        IBrush fg = dark ? Brushes.White : Brushes.Black;
        dc.DrawLine(new Pen(fg, 2), new Point(14, h - 16), new Point(14 + m100, h - 16));
        dc.DrawText(new FormattedText(lab, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, fg), new Point(14, h - 34));
    }

    static void Box(StreamGeometryContext c, double x, double y, double w, double h) {
        c.BeginFigure(new Point(x, y), true);
        c.LineTo(new Point(x + w, y)); c.LineTo(new Point(x + w, y + h)); c.LineTo(new Point(x, y + h));
        c.EndFigure(true);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        if (e.ClickCount == 2) { Fit(); return; }
        last = e.GetPosition(this); drag = true; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { drag = false; e.Pointer.Capture(null); }
    protected override void OnPointerMoved(PointerEventArgs e) {
        if (!drag) return;
        Point p = e.GetPosition(this); cx -= (p.X - last.X) / scale; cy += (p.Y - last.Y) / scale; last = p; InvalidateVisual();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
        Point p = e.GetPosition(this);
        double wx = cx + (p.X - Bounds.Width / 2) / scale, wy = cy - (p.Y - Bounds.Height / 2) / scale;
        scale = Math.Max(0.002, Math.Min(40, scale * (e.Delta.Y > 0 ? 1.2 : 1 / 1.2)));
        cx = wx - (p.X - Bounds.Width / 2) / scale; cy = wy + (p.Y - Bounds.Height / 2) / scale;
        InvalidateVisual(); e.Handled = true;
    }
}
