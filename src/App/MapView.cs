using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Obj2x.UI {

// 맵 탭의 2D 지도: 타일 칸, 스플라인(곡선은 호로), 오브젝트 점. 없는 파일은 빨강. 끌어서 이동, 휠로 확대, 두 번 눌러 전체 보기.
// 맵을 바꿀 때 한 번만 세계 좌표로 도형을 만들어 두고, 그릴 때는 변환만 바꾼다 (오브젝트가 수만 개여도 부드럽게)
public class MapCanvas : Drawn {
    public static readonly DependencyProperty MapProperty = Dp("Map", typeof(MapInfo), typeof(MapCanvas), null);
    public static readonly DependencyProperty LayersProperty = Dp("Layers", typeof(string), typeof(MapCanvas), "tiles,splines,objects");
    public MapInfo Map { get { return (MapInfo)GetValue(MapProperty); } set { SetValue(MapProperty, value); } }
    public string Layers { get { return (string)GetValue(LayersProperty); } set { SetValue(LayersProperty, value); } }

    Geometry gTiles, gTilesMissing, gSpl, gSplMissing, gObj, gObjMissing;
    MapInfo built;
    double scale = 1, cx, cy;     // 화면 1px = 1/scale m, 화면 가운데 = 세계 (cx, cy)
    Point last; bool drag;

    bool fitted;
    public MapCanvas() { ClipToBounds = true; Cursor = Cursors.SizeAll; Focusable = false; SizeChanged += delegate { if (!fitted) Fit(); }; }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == MapProperty) { Build(); fitted = false; Fit(); }
    }

    void Build() {
        MapInfo m = Map; built = m;
        gTiles = gTilesMissing = gSpl = gSplMissing = gObj = gObjMissing = null;
        if (m == null) return;
        StreamGeometry t = new StreamGeometry(), tm = new StreamGeometry();
        using (StreamGeometryContext a = t.Open()) using (StreamGeometryContext b = tm.Open())
            foreach (MapTile tile in m.Tiles) Rect(tile.Exists ? a : b, tile.X * 300.0, tile.Y * 300.0, 300, 300);
        StreamGeometry s = new StreamGeometry(), sm = new StreamGeometry();
        using (StreamGeometryContext a = s.Open()) using (StreamGeometryContext b = sm.Open())
            foreach (MapSpline sp in m.Splines) Spline(sp.Missing ? b : a, sp);
        StreamGeometry o = new StreamGeometry(), om = new StreamGeometry();
        using (StreamGeometryContext a = o.Open()) using (StreamGeometryContext b = om.Open())
            foreach (MapObj ob in m.Objects) Rect(ob.Missing ? b : a, ob.X - 1.5, ob.Y - 1.5, 3, 3);
        foreach (StreamGeometry g in new StreamGeometry[] { t, tm, s, sm, o, om }) g.Freeze();
        gTiles = t; gTilesMissing = tm; gSpl = s; gSplMissing = sm; gObj = o; gObjMissing = om;
    }

    // 세계 좌표는 y가 북쪽(위)이라 그릴 때 y를 뒤집는다
    static void Rect(StreamGeometryContext c, double x, double y, double w, double h) {
        c.BeginFigure(new Point(x, -y), true, true);
        c.PolyLineTo(new Point[] { new Point(x + w, -y), new Point(x + w, -y - h), new Point(x, -y - h) }, true, false);
    }
    // 시작점, 방향(+y에서 +x 쪽으로 도), 길이, 반지름(0 = 직선, 부호 = 휘는 쪽)
    static void Spline(StreamGeometryContext c, MapSpline s) {
        double a = s.Dir * Math.PI / 180, x = s.X, y = s.Y;
        c.BeginFigure(new Point(x, -y), false, false);
        if (Math.Abs(s.Rad) < 0.01 || s.Len <= 0) { c.LineTo(new Point(x + Math.Sin(a) * s.Len, -(y + Math.Cos(a) * s.Len)), true, false); return; }
        int n = Math.Max(2, Math.Min(32, (int)(s.Len / 5)));
        double step = s.Len / n, turn = step / s.Rad;
        for (int i = 0; i < n; i++) { a += turn / 2; x += Math.Sin(a) * step; y += Math.Cos(a) * step; a += turn / 2; c.LineTo(new Point(x, -y), true, false); }
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

    protected override void OnRender(DrawingContext dc) {
        dc.DrawRectangle(Theme.Brush(Theme.Dark ? Color.FromRgb(22, 24, 30) : Color.FromRgb(236, 239, 244)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (Map == null) return;
        string ly = Layers ?? "";
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, ActualWidth / 2 - cx * scale, ActualHeight / 2 + cy * scale));
        double px = 1 / scale;   // 화면 1px의 세계 크기
        Color grid = Theme.Dark ? Color.FromRgb(44, 48, 58) : Color.FromRgb(214, 219, 228);
        if (ly.Contains("tiles")) {
            dc.DrawGeometry(Theme.Brush(Theme.Dark ? Color.FromRgb(30, 33, 41) : Colors.White), new Pen(Theme.Brush(grid), px), gTiles);
            dc.DrawGeometry(Theme.Brush(Color.FromArgb(60, 239, 68, 68)), new Pen(Theme.Brush(Color.FromRgb(239, 68, 68)), px), gTilesMissing);
        }
        bool onlyMiss = ly.Contains("missing");
        if (ly.Contains("splines")) {
            if (!onlyMiss) dc.DrawGeometry(null, new Pen(Theme.Brush(Theme.Dark ? Color.FromRgb(150, 156, 170) : Color.FromRgb(96, 104, 120)), Math.Max(px * 1.6, 2)), gSpl);
            dc.DrawGeometry(null, new Pen(Theme.Brush(Color.FromRgb(239, 68, 68)), Math.Max(px * 2.4, 3)), gSplMissing);
        }
        if (ly.Contains("objects")) {
            // 점은 확대 배율과 상관없이 화면에서 3px 정도로 보이게 (테두리 굵기를 화면 기준으로)
            if (!onlyMiss) dc.DrawGeometry(Theme.Brush(Theme.A(Theme.Accent, 0.85)), new Pen(Theme.Brush(Theme.A(Theme.Accent, 0.85)), px * 2.5), gObj);
            dc.DrawGeometry(Theme.Brush(Color.FromRgb(239, 68, 68)), new Pen(Theme.Brush(Color.FromRgb(239, 68, 68)), px * 3.5), gObjMissing);
        }
        dc.Pop();
        // 축척 막대
        double m100 = 100 * scale; string lab = "100 m";
        if (m100 < 40) { m100 = 1000 * scale; lab = "1 km"; }
        if (m100 > 260) { m100 = 10 * scale; lab = "10 m"; }
        Pen p = Art.Pen(Theme.Text, 2); double bx = 14, by = ActualHeight - 16;
        dc.DrawLine(p, new Point(bx, by), new Point(bx + m100, by));
        dc.DrawText(Art.Ft(lab, 11, false, Theme.Text), new Point(bx, by - 18));
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
