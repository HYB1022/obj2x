using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace Obj2x.UI;

// 직접 그리는 컨트롤이 함께 쓰는 그림 도구 (글자, 메뉴 아이콘, 로고)
public static class Art {
    // GDI 시절의 pt 크기를 픽셀로 (9pt = 12px)
    public static double Pt(double pt) { return pt * 96.0 / 72.0; }

    public static FormattedText Ft(string s, double px, bool bold, Color c) {
        return new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Look.Font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), px, Look.Brush(c));
    }
    // 한 줄, 폭을 넘으면 … 으로 줄인다
    public static FormattedText Line(string s, double px, bool bold, Color c, double maxW) {
        FormattedText f = Ft(s, px, bold, c);
        if (maxW > 1) { f.MaxTextWidth = maxW; f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis; }
        return f;
    }
    // 사각형 안 세로 가운데에 왼쪽/가운데/오른쪽 정렬로 그린다
    public static void Text(DrawingContext dc, FormattedText f, Rect r, TextAlignment al) {
        if (f.MaxTextWidth <= 0 || f.MaxTextWidth > r.Width) { f.MaxTextWidth = Math.Max(1, r.Width); f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis; }
        f.TextAlignment = al;
        dc.DrawText(f, new Point(r.X, r.Y + (r.Height - f.Height) / 2));
    }
    public static double W(string s, double px, bool bold) { return Ft(s, px, bold, Colors.Black).WidthIncludingTrailingWhitespace; }
    // 모든 언어 중 가장 긴 문구의 폭 (언어를 바꿔도 잘리지 않게)
    public static double MaxW(string key, double px, bool bold) {
        string save = L.Lang; double w = 0;
        try { foreach (string[] l in L.Languages) { L.Lang = l[0]; w = Math.Max(w, W(L.T(key), px, bold)); } }
        finally { L.Lang = save; }
        return w;
    }

    // 펜도 색/굵기가 같으면 다시 쓴다
    static readonly Dictionary<long, IPen> pens = new();
    static IPen CachedPen(Color c, double w, bool round) {
        long key = ((long)(uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B) << 24) | ((long)Math.Round(w * 100) << 1) | (round ? 1L : 0L);
        lock (pens) {
            if (pens.TryGetValue(key, out IPen p)) return p;
            if (pens.Count > 4096) pens.Clear();
            p = round ? new ImmutablePen(Look.Brush(c), w, null, PenLineCap.Round, PenLineJoin.Round) : new ImmutablePen(Look.Brush(c), w);
            pens[key] = p;
            return p;
        }
    }
    public static IPen RoundPen(Color c, double w) { return CachedPen(c, w, true); }
    public static IPen Pen(Color c, double w) { return CachedPen(c, w, false); }
    public static Geometry Poly(bool closed, params Point[] pts) {
        StreamGeometry g = new StreamGeometry();
        using (StreamGeometryContext c = g.Open()) {
            c.BeginFigure(pts[0], closed);
            for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i]);
            c.EndFigure(closed);
        }
        return g;
    }
    // 둥근 사각형 모양 (잘라 내기용)
    public static Geometry RoundRect(Rect r, double rad) {
        rad = Math.Max(0, Math.Min(rad, Math.Min(r.Width, r.Height) / 2));
        StreamGeometry g = new StreamGeometry();
        using (StreamGeometryContext c = g.Open()) {
            if (rad <= 0) {
                c.BeginFigure(r.TopLeft, true); c.LineTo(r.TopRight); c.LineTo(r.BottomRight); c.LineTo(r.BottomLeft); c.EndFigure(true);
                return g;
            }
            Size s = new Size(rad, rad);
            c.BeginFigure(new Point(r.X + rad, r.Y), true);
            c.LineTo(new Point(r.Right - rad, r.Y)); c.ArcTo(new Point(r.Right, r.Y + rad), s, 0, false, SweepDirection.Clockwise);
            c.LineTo(new Point(r.Right, r.Bottom - rad)); c.ArcTo(new Point(r.Right - rad, r.Bottom), s, 0, false, SweepDirection.Clockwise);
            c.LineTo(new Point(r.X + rad, r.Bottom)); c.ArcTo(new Point(r.X, r.Bottom - rad), s, 0, false, SweepDirection.Clockwise);
            c.LineTo(new Point(r.X, r.Y + rad)); c.ArcTo(new Point(r.X + rad, r.Y), s, 0, false, SweepDirection.Clockwise);
            c.EndFigure(true);
        }
        return g;
    }
    public static void RoundedRect(DrawingContext dc, IBrush b, IPen p, Rect r, double rad) { dc.DrawRectangle(b, p, r, rad, rad); }

    // ---- 메뉴/정보 화면 아이콘: 색이 있는 둥근 사각형 타일 위에 흰 선 그림 (24 단위 격자 기준)
    public static Color TileColor(string name) {
        switch (name) {
            case "dashboard": return Color.FromRgb(88, 101, 242);
            case "wizard": return Color.FromRgb(175, 82, 222);
            case "advanced": return Color.FromRgb(255, 149, 0);
            case "settings": return Color.FromRgb(142, 142, 147);
            case "about": return Color.FromRgb(0, 122, 255);
            case "dev": return Color.FromRgb(96, 125, 139);
            case "lang": return Color.FromRgb(52, 199, 89);
            case "view": return Color.FromRgb(255, 45, 85);
            case "object": return Color.FromRgb(0, 178, 190);
            case "spline": return Color.FromRgb(52, 168, 108);
            case "bus": return Color.FromRgb(255, 69, 58);
            case "map": return Color.FromRgb(90, 160, 250);
            default: return Look.Accent;
        }
    }

    public static void Icon(DrawingContext dc, string name, double x, double y, double size, double alpha) {
        if (alpha <= 0.004) return;
        Color tc = Look.A(TileColor(name), alpha), wc = Look.A(Colors.White, alpha);
        IBrush tb = Look.Brush(tc), wb = Look.Brush(wc);
        double k = size / 24.0;
        dc.DrawRectangle(tb, null, new Rect(x, y, size, size), size * 0.27, size * 0.27);
        IPen p = RoundPen(wc, Math.Max(1.2, 1.7 * k));
        switch (name) {
            case "dashboard":   // 네 칸 타일
                for (int i = 0; i < 4; i++) {
                    double cx = x + ((i % 2 == 0) ? 6.5 : 12.5) * k, cy = y + ((i < 2) ? 6.5 : 12.5) * k;
                    dc.DrawRectangle(wb, null, new Rect(cx, cy, 5 * k, 5 * k), 1.3 * k, 1.3 * k);
                }
                break;
            case "wizard":      // 마법봉 + 반짝임
                dc.DrawLine(p, new Point(x + 6.5 * k, y + 17.5 * k), new Point(x + 13.5 * k, y + 10.5 * k));
                Sparkle(dc, wb, x + 16 * k, y + 8 * k, 3.8 * k);
                Sparkle(dc, wb, x + 8.5 * k, y + 8 * k, 2 * k);
                break;
            case "advanced": {  // 슬라이더 세 줄
                IPen thin = RoundPen(wc, Math.Max(1.0, 1.4 * k));
                double[] ys = { 7.5, 12, 16.5 }, ks = { 14.5, 9, 13 };
                for (int i = 0; i < 3; i++) {
                    dc.DrawLine(thin, new Point(x + 6 * k, y + ys[i] * k), new Point(x + 18 * k, y + ys[i] * k));
                    dc.DrawEllipse(tb, thin, new Point(x + ks[i] * k, y + ys[i] * k), 2 * k, 2 * k);
                }
                break;
            }
            case "settings": {  // 톱니바퀴
                double gx = x + 12 * k, gy = y + 12 * k;
                for (int i = 0; i < 8; i++) {
                    Matrix m = Matrix.CreateTranslation(-gx, -gy) * Matrix.CreateRotation(i * Math.PI / 4) * Matrix.CreateTranslation(gx, gy);
                    using (dc.PushTransform(m)) dc.DrawRectangle(wb, null, new Rect(gx - 1.6 * k, gy - 7.2 * k, 3.2 * k, 3.4 * k));
                }
                dc.DrawEllipse(wb, null, new Point(gx, gy), 5.2 * k, 5.2 * k);
                dc.DrawEllipse(tb, null, new Point(gx, gy), 2.2 * k, 2.2 * k);
                break;
            }
            case "about":       // i
                dc.DrawEllipse(wb, null, new Point(x + 12 * k, y + 7.2 * k), 1.4 * k, 1.4 * k);
                dc.DrawLine(RoundPen(wc, 2.4 * k), new Point(x + 12 * k, y + 11 * k), new Point(x + 12 * k, y + 17.5 * k));
                break;
            case "dev":         // </> (개발자 옵션)
                dc.DrawGeometry(null, p, Poly(false, new Point(x + 9 * k, y + 8 * k), new Point(x + 5.5 * k, y + 12 * k), new Point(x + 9 * k, y + 16 * k)));
                dc.DrawGeometry(null, p, Poly(false, new Point(x + 15 * k, y + 8 * k), new Point(x + 18.5 * k, y + 12 * k), new Point(x + 15 * k, y + 16 * k)));
                dc.DrawLine(p, new Point(x + 13.2 * k, y + 6.8 * k), new Point(x + 10.8 * k, y + 17.2 * k));
                break;
            case "lang": {      // 지구본
                IPen thin = RoundPen(wc, Math.Max(1.0, 1.4 * k));
                Point c = new Point(x + 12 * k, y + 12 * k);
                dc.DrawEllipse(null, thin, c, 6.5 * k, 6.5 * k);
                dc.DrawEllipse(null, thin, c, 2.8 * k, 6.5 * k);
                dc.DrawLine(thin, new Point(x + 5.5 * k, y + 12 * k), new Point(x + 18.5 * k, y + 12 * k));
                break;
            }
            case "view": {      // 반쯤 칠한 원 (밝게/어둡게)
                Point c = new Point(x + 12 * k, y + 12 * k);
                dc.DrawEllipse(null, p, c, 6 * k, 6 * k);
                StreamGeometry g = new StreamGeometry();
                using (StreamGeometryContext gc = g.Open()) {
                    gc.BeginFigure(new Point(c.X, c.Y - 6 * k), true);
                    gc.ArcTo(new Point(c.X, c.Y + 6 * k), new Size(6 * k, 6 * k), 0, false, SweepDirection.Clockwise);
                    gc.EndFigure(true);
                }
                dc.DrawGeometry(wb, null, g);
                break;
            }
            case "object": {    // 정육면체 (OMSI 오브젝트)
                Point top = new Point(x + 12 * k, y + 5.5 * k), l = new Point(x + 6 * k, y + 8.8 * k), r = new Point(x + 18 * k, y + 8.8 * k);
                Point c = new Point(x + 12 * k, y + 12.1 * k), lb = new Point(x + 6 * k, y + 15.4 * k), rb = new Point(x + 18 * k, y + 15.4 * k), b = new Point(x + 12 * k, y + 18.7 * k);
                dc.DrawGeometry(Look.Brush(Look.A(Colors.White, alpha * 0.35)), null, Poly(true, top, r, c, l));
                dc.DrawGeometry(null, p, Poly(true, top, r, rb, b, lb, l));
                dc.DrawGeometry(null, p, Poly(false, l, c, r));
                dc.DrawLine(p, c, b);
                break;
            }
            case "spline": {    // 멀리 뻗은 도로 + 가운데 점선 (OMSI 스플라인)
                dc.DrawLine(p, new Point(x + 10 * k, y + 5.5 * k), new Point(x + 5.5 * k, y + 18.5 * k));
                dc.DrawLine(p, new Point(x + 14 * k, y + 5.5 * k), new Point(x + 18.5 * k, y + 18.5 * k));
                IPen dash = RoundPen(wc, Math.Max(1.0, 1.5 * k));
                dc.DrawLine(dash, new Point(x + 12 * k, y + 6.5 * k), new Point(x + 12 * k, y + 8.5 * k));
                dc.DrawLine(dash, new Point(x + 12 * k, y + 11 * k), new Point(x + 12 * k, y + 13.2 * k));
                dc.DrawLine(dash, new Point(x + 12 * k, y + 15.8 * k), new Point(x + 12 * k, y + 18.3 * k));
                break;
            }
            case "map": {       // 접힌 지도 + 위치 핀 (맵 탭)
                dc.DrawGeometry(null, p, Poly(true, new Point(x + 5 * k, y + 7.5 * k), new Point(x + 9.5 * k, y + 6 * k), new Point(x + 14.5 * k, y + 7.5 * k),
                    new Point(x + 19 * k, y + 6 * k), new Point(x + 19 * k, y + 16.5 * k), new Point(x + 14.5 * k, y + 18 * k), new Point(x + 9.5 * k, y + 16.5 * k), new Point(x + 5 * k, y + 18 * k)));
                IPen thin = RoundPen(wc, Math.Max(1.0, 1.2 * k));
                dc.DrawLine(thin, new Point(x + 9.5 * k, y + 6 * k), new Point(x + 9.5 * k, y + 16.5 * k));
                dc.DrawLine(thin, new Point(x + 14.5 * k, y + 7.5 * k), new Point(x + 14.5 * k, y + 18 * k));
                break;
            }
            case "bus": {       // 버스 옆모습 (버스 탭)
                dc.DrawRectangle(null, p, new Rect(x + 4.5 * k, y + 7 * k, 15 * k, 8.5 * k), 1.8 * k, 1.8 * k);
                IPen thin = RoundPen(wc, Math.Max(1.0, 1.3 * k));
                dc.DrawLine(thin, new Point(x + 4.8 * k, y + 11 * k), new Point(x + 19.2 * k, y + 11 * k));
                dc.DrawLine(thin, new Point(x + 9.5 * k, y + 7.3 * k), new Point(x + 9.5 * k, y + 11 * k));
                dc.DrawLine(thin, new Point(x + 14.5 * k, y + 7.3 * k), new Point(x + 14.5 * k, y + 11 * k));
                dc.DrawEllipse(tb, p, new Point(x + 8.3 * k, y + 16 * k), 1.8 * k, 1.8 * k);
                dc.DrawEllipse(tb, p, new Point(x + 15.7 * k, y + 16 * k), 1.8 * k, 1.8 * k);
                break;
            }
            case "update":      // 아래 화살표 + 받침
                dc.DrawLine(p, new Point(x + 12 * k, y + 6 * k), new Point(x + 12 * k, y + 14 * k));
                dc.DrawGeometry(null, p, Poly(false, new Point(x + 8.5 * k, y + 10.8 * k), new Point(x + 12 * k, y + 14.3 * k), new Point(x + 15.5 * k, y + 10.8 * k)));
                dc.DrawGeometry(null, p, Poly(false, new Point(x + 6.5 * k, y + 15.5 * k), new Point(x + 6.5 * k, y + 17.8 * k), new Point(x + 17.5 * k, y + 17.8 * k), new Point(x + 17.5 * k, y + 15.5 * k)));
                break;
        }
    }

    // 네 갈래 반짝임
    static void Sparkle(DrawingContext dc, IBrush b, double cx, double cy, double r) {
        double i = r * 0.28;
        dc.DrawGeometry(b, null, Poly(true,
            new Point(cx, cy - r), new Point(cx + i, cy - i), new Point(cx + r, cy), new Point(cx + i, cy + i),
            new Point(cx, cy + r), new Point(cx - i, cy + i), new Point(cx - r, cy), new Point(cx - i, cy - i)));
    }

    // ---- 로고: 뒤쪽 그라데이션 타일 + 앞쪽 반투명 타일 + 흰 버스 앞모습 (전체 크기 size)
    public static void Logo(DrawingContext dc, double x, double y, double size) {
        double s = size / 172.0;
        Rect back = new Rect(x + 22 * s, y + 22 * s, 150 * s, 150 * s);
        LinearGradientBrush gb = new LinearGradientBrush {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Look.Accent, 0), new GradientStop(Look.AccentHov, 1) } };
        dc.DrawRectangle(gb, null, back, 34 * s, 34 * s);
        dc.DrawRectangle(Look.Brush(Look.A(Look.Accent, 150 / 255.0)), Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1, 1.5 * s)), new Rect(x, y, 150 * s, 150 * s), 34 * s, 34 * s);
        // 앞 타일 위에 흰 버스 앞모습 (Bus Depot): 행선지 띠, 앞 유리, 전조등, 바퀴
        IBrush wb = Look.Brush(Colors.White), ab = Look.Brush(Look.Mix(Look.Accent, Colors.Black, 0.15)), lb = Look.Brush(Color.FromRgb(255, 214, 102));
        dc.DrawRectangle(ab, null, new Rect(x + 42 * s, y + 108 * s, 18 * s, 22 * s), 5 * s, 5 * s);
        dc.DrawRectangle(ab, null, new Rect(x + 90 * s, y + 108 * s, 18 * s, 22 * s), 5 * s, 5 * s);
        dc.DrawRectangle(wb, null, new Rect(x + 32 * s, y + 24 * s, 86 * s, 92 * s), 16 * s, 16 * s);
        dc.DrawRectangle(ab, null, new Rect(x + 44 * s, y + 31 * s, 62 * s, 9 * s), 3 * s, 3 * s);
        dc.DrawRectangle(ab, null, new Rect(x + 40 * s, y + 46 * s, 70 * s, 38 * s), 7 * s, 7 * s);
        dc.DrawEllipse(lb, null, new Point(x + 49 * s, y + 99 * s), 6 * s, 6 * s);
        dc.DrawEllipse(lb, null, new Point(x + 101 * s, y + 99 * s), 6 * s, 6 * s);
        dc.DrawRectangle(ab, null, new Rect(x + 62 * s, y + 96 * s, 26 * s, 6 * s), 3 * s, 3 * s);
    }

    public static RenderTargetBitmap Render(int w, int h, Action<DrawingContext> draw) {
        RenderTargetBitmap b = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        using (DrawingContext dc = b.CreateDrawingContext()) draw(dc);
        return b;
    }

    static byte[] Png(int z) {
        using RenderTargetBitmap b = Render(z, z, dc => Logo(dc, z * 0.02, z * 0.02, z * 0.96));
        using MemoryStream ms = new MemoryStream(); b.Save(ms); return ms.ToArray();
    }
    // 아이콘 파일(.ico): 16~256 크기를 PNG로 담는다 (개발용 --make-icon)
    public static void WriteIco(string path) {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> pngs = new();
        foreach (int z in sizes) pngs.Add(Png(z));
        using BinaryWriter w = new BinaryWriter(File.Create(path));
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
        int off = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++) {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32); w.Write(pngs[i].Length); w.Write(off);
            off += pngs[i].Length;
        }
        foreach (byte[] b in pngs) w.Write(b);
    }
    // 맥 앱 아이콘(.icns): PNG를 담는 형식 ic07(128) ic08(256) ic09(512) ic10(1024) (개발용 --make-icns, mac/BusDepot.icns)
    public static void WriteIcns(string path) {
        (string tag, int z)[] items = { ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024) };
        List<(string, byte[])> parts = new();
        foreach (var (tag, z) in items) parts.Add((tag, Png(z)));
        int total = 8; foreach (var (t, d) in parts) total += 8 + d.Length;
        using BinaryWriter w = new BinaryWriter(File.Create(path));
        void BE(int v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
        w.Write(System.Text.Encoding.ASCII.GetBytes("icns")); BE(total);
        foreach (var (t, d) in parts) { w.Write(System.Text.Encoding.ASCII.GetBytes(t)); BE(8 + d.Length); w.Write(d); }
    }

    // 창 아이콘 (로고를 64px로)
    public static WindowIcon AppIcon() {
        using RenderTargetBitmap b = Render(64, 64, dc => Logo(dc, 4, 4, 56));
        MemoryStream ms = new MemoryStream();
        b.Save(ms); ms.Position = 0;
        return new WindowIcon(ms);
    }
}

// 색 타일 아이콘 하나 (환영 화면의 언어/보기 줄)
public class TileIcon : Control {
    public static readonly StyledProperty<string> NameKeyProperty = AvaloniaProperty.Register<TileIcon, string>(nameof(NameKey), "");
    public string NameKey { get => GetValue(NameKeyProperty); set => SetValue(NameKeyProperty, value); }
    static TileIcon() { AffectsRender<TileIcon>(NameKeyProperty, IsEnabledProperty); }
    public override void Render(DrawingContext dc) { Art.Icon(dc, NameKey, 0, 0, Math.Min(Bounds.Width, Bounds.Height), IsEffectivelyEnabled ? 1 : 0.5); }
}

// 로고 컨트롤 (사이드바, 환영 화면)
public class LogoArt : Control {
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); Look.Changed += InvalidateVisual; }
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e) { base.OnDetachedFromVisualTree(e); Look.Changed -= InvalidateVisual; }
    public override void Render(DrawingContext dc) { Art.Logo(dc, 0, 0, Math.Min(Bounds.Width, Bounds.Height)); }
}
