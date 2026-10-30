using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Obj2x.UI {

// 맵 탭의 2D 지도: 타일 칸, 스플라인(곡선은 호로), 오브젝트 점. 없는 파일은 빨강. 끌어서 이동, 휠로 확대, 두 번 눌러 전체 보기.
// 수만 개를 세계 좌표 도형 하나로 그리면 확대/이동할 때마다 WPF가 전부 다시 테셀레이션해서 창이 멈춘다.
// 그래서 그릴 때마다 화면에 보이는 것만 화면 좌표로 만들고, 오브젝트는 3px 칸마다 하나만, 스플라인은 2px보다 짧은 마디를 건너뛴다.
public class MapCanvas : Drawn {
    public static readonly DependencyProperty MapProperty = Dp("Map", typeof(MapInfo), typeof(MapCanvas), null);
    public static readonly DependencyProperty LayersProperty = Dp("Layers", typeof(string), typeof(MapCanvas), "tiles,splines,objects");
    public MapInfo Map { get { return (MapInfo)GetValue(MapProperty); } set { SetValue(MapProperty, value); } }
    public string Layers { get { return (string)GetValue(LayersProperty); } set { SetValue(LayersProperty, value); } }

    // 스플라인을 세계 좌표 점들로 한 번만 풀어 둔다 (+ 둘레 상자로 화면 밖은 빨리 건너뜀)
    class SplPath { public Point[] P; public Rect Box; public bool Missing; }
    List<SplPath> spl = new List<SplPath>();
    double scale = 1, cx, cy;     // 화면 1px = 1/scale m, 화면 가운데 = 세계 (cx, cy)
    Point last; bool drag;

    bool fitted;
    public MapCanvas() { ClipToBounds = true; Cursor = Cursors.SizeAll; Focusable = false; SizeChanged += delegate { if (!fitted) Fit(); }; }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == MapProperty) { Build(); fitted = false; Fit(); }
    }

    void Build() {
        spl = new List<SplPath>();
        MapInfo m = Map; if (m == null) return;
        foreach (MapSpline s in m.Splines) {
            Point[] p = SplinePoints(s);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Point q in p) { x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y); }
            spl.Add(new SplPath { P = p, Box = new Rect(x0, y0, x1 - x0, y1 - y0), Missing = s.Missing });
        }
    }

    // 시작점, 방향(+y에서 +x 쪽으로 도), 길이, 반지름(0 = 직선, 부호 = 휘는 쪽)
    static Point[] SplinePoints(MapSpline s) {
        double a = s.Dir * Math.PI / 180, x = s.X, y = s.Y;
        if (Math.Abs(s.Rad) < 0.01 || s.Len <= 0) return new Point[] { new Point(x, y), new Point(x + Math.Sin(a) * s.Len, y + Math.Cos(a) * s.Len) };
        int n = Math.Max(2, Math.Min(32, (int)(s.Len / 5)));
        Point[] p = new Point[n + 1]; p[0] = new Point(x, y);
        double step = s.Len / n, turn = step / s.Rad;
        for (int i = 0; i < n; i++) { a += turn / 2; x += Math.Sin(a) * step; y += Math.Cos(a) * step; a += turn / 2; p[i + 1] = new Point(x, y); }
        return p;
    }

    void Fit() {
        MapInfo m = Map; if (m == null || m.Tiles.Count == 0 || ActualWidth < 10) { InvalidateVisual(); return; }
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (MapTile t in m.Tiles) { x0 = Math.Min(x0, t.X * 300.0); y0 = Math.Min(y0, t.Y * 300.0); x1 = Math.Max(x1, t.X * 300.0 + 300); y1 = Math.Max(y1, t.Y * 300.0 + 300); }
        cx = (x0 + x1) / 2; cy = (y0 + y1) / 2;
        scale = Math.Min(ActualWidth / (x1 - x0), ActualHeight / (y1 - y0)) * 0.92;
        fitted = true;
        InvalidateVisual();
    }

    // 세계 → 화면 (세계 y는 북쪽이 위)
    double SX(double x) { return ActualWidth / 2 + (x - cx) * scale; }
    double SY(double y) { return ActualHeight / 2 - (y - cy) * scale; }

    protected override void OnRender(DrawingContext dc) {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Theme.Brush(Theme.Dark ? Color.FromRgb(22, 24, 30) : Color.FromRgb(236, 239, 244)), null, new Rect(0, 0, w, h));
        MapInfo m = Map; if (m == null) return;
        string ly = Layers ?? "";
        bool onlyMiss = ly.Contains("missing");
        // 화면에 보이는 세계 범위 (조금 넉넉하게)
        double pad = 8 / scale;
        Rect view = new Rect(cx - w / 2 / scale - pad, cy - h / 2 / scale - pad, w / scale + 2 * pad, h / scale + 2 * pad);
        Color red = Color.FromRgb(239, 68, 68);

        if (ly.Contains("tiles")) {
            StreamGeometry t = new StreamGeometry(), tm = new StreamGeometry();
            using (StreamGeometryContext a = t.Open()) using (StreamGeometryContext b = tm.Open())
                foreach (MapTile tile in m.Tiles) {
                    double x = tile.X * 300.0, y = tile.Y * 300.0;
                    if (x > view.Right || x + 300 < view.Left || y > view.Bottom || y + 300 < view.Top) continue;
                    Box(tile.Exists ? a : b, SX(x), SY(y + 300), 300 * scale, 300 * scale);
                }
            t.Freeze(); tm.Freeze();
            Color grid = Theme.Dark ? Color.FromRgb(44, 48, 58) : Color.FromRgb(214, 219, 228);
            dc.DrawGeometry(Theme.Brush(Theme.Dark ? Color.FromRgb(30, 33, 41) : Colors.White), Art.Pen(grid, 1), t);
            dc.DrawGeometry(Theme.Brush(Color.FromArgb(60, 239, 68, 68)), Art.Pen(red, 1), tm);
        }

        if (ly.Contains("splines")) {
            StreamGeometry s = new StreamGeometry(), sm = new StreamGeometry();
            using (StreamGeometryContext a = s.Open()) using (StreamGeometryContext b = sm.Open())
                foreach (SplPath p in spl) {
                    if (onlyMiss && !p.Missing) continue;
                    if (!p.Box.IntersectsWith(view)) continue;
                    StreamGeometryContext c = p.Missing ? b : a;
                    Point prev = new Point(SX(p.P[0].X), SY(p.P[0].Y));
                    c.BeginFigure(prev, false, false);
                    for (int i = 1; i < p.P.Length; i++) {
                        Point q = new Point(SX(p.P[i].X), SY(p.P[i].Y));
                        // 화면에서 2px보다 가까운 점은 건너뛰되 끝점은 꼭 잇는다
                        if (i < p.P.Length - 1 && Math.Abs(q.X - prev.X) + Math.Abs(q.Y - prev.Y) < 2) continue;
                        c.LineTo(q, true, false); prev = q;
                    }
                }
            s.Freeze(); sm.Freeze();
            dc.DrawGeometry(null, Art.Pen(Theme.Dark ? Color.FromRgb(150, 156, 170) : Color.FromRgb(96, 104, 120), Math.Round(Math.Max(1.5, Math.Min(4, 1.6 * scale)) * 2) / 2), s);
            dc.DrawGeometry(null, Art.Pen(red, Math.Round(Math.Max(2.5, Math.Min(5, 2.4 * scale)) * 2) / 2), sm);
        }

        if (ly.Contains("objects")) {
            // 점은 확대 배율과 상관없이 화면에서 3px. 같은 3px 칸에 여러 개면 하나만 그린다
            StreamGeometry o = new StreamGeometry(), om = new StreamGeometry();
            HashSet<long> seen = new HashSet<long>(), seenM = new HashSet<long>();
            using (StreamGeometryContext a = o.Open()) using (StreamGeometryContext b = om.Open())
                foreach (MapObj ob in m.Objects) {
                    if (onlyMiss && !ob.Missing) continue;
                    if (!view.Contains(ob.X, ob.Y)) continue;
                    double x = SX(ob.X), y = SY(ob.Y);
                    long key = ((long)Math.Floor(x / 3) << 32) ^ (uint)(int)Math.Floor(y / 3);
                    if (!(ob.Missing ? seenM : seen).Add(key)) continue;
                    if (ob.Missing) Box(b, x - 2.5, y - 2.5, 5, 5); else Box(a, x - 1.5, y - 1.5, 3, 3);
                }
            o.Freeze(); om.Freeze();
            dc.DrawGeometry(Theme.Brush(Theme.A(Theme.Accent, 0.85)), null, o);
            dc.DrawGeometry(Theme.Brush(red), null, om);
        }

        // 축척 막대
        double m100 = 100 * scale; string lab = "100 m";
        if (m100 < 40) { m100 = 1000 * scale; lab = "1 km"; }
        if (m100 > 260) { m100 = 10 * scale; lab = "10 m"; }
        Pen p2 = Art.Pen(Theme.Text, 2); double bx = 14, by = h - 16;
        dc.DrawLine(p2, new Point(bx, by), new Point(bx + m100, by));
        dc.DrawText(Art.Ft(lab, 11, false, Theme.Text), new Point(bx, by - 18));
    }

    static void Box(StreamGeometryContext c, double x, double y, double w, double h) {
        c.BeginFigure(new Point(x, y), true, true);
        c.PolyLineTo(new Point[] { new Point(x + w, y), new Point(x + w, y + h), new Point(x, y + h) }, true, false);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        if (e.ClickCount == 2) { Fit(); return; }
        last = e.GetPosition(this); drag = true; CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { drag = false; ReleaseMouseCapture(); }
    protected override void OnMouseMove(MouseEventArgs e) {
        if (!drag) return;
        Point p = e.GetPosition(this); cx -= (p.X - last.X) / scale; cy += (p.Y - last.Y) / scale; last = p; InvalidateVisual();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e) {
        Point p = e.GetPosition(this);
        double wx = cx + (p.X - ActualWidth / 2) / scale, wy = cy - (p.Y - ActualHeight / 2) / scale;
        scale = Math.Max(0.002, Math.Min(40, scale * (e.Delta > 0 ? 1.2 : 1 / 1.2)));
        cx = wx - (p.X - ActualWidth / 2) / scale; cy = wy + (p.Y - ActualHeight / 2) / scale;
        InvalidateVisual(); e.Handled = true;
    }
}
}
