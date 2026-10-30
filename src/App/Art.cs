using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

#pragma warning disable 618   // FormattedText의 옛 생성자: .NET 4.0부터 있어서 가장 넓게 돈다

namespace Obj2x.UI {

// 직접 그리는 컨트롤이 함께 쓰는 그림 도구 (글자, 메뉴 아이콘, 로고)
public static class Art {
    // GDI 시절의 pt 크기를 WPF 픽셀로 (9pt = 12px)
    public static double Pt(double pt) { return pt * 96.0 / 72.0; }

    public static FormattedText Ft(string s, double px, bool bold, Color c) {
        FormattedText f = new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(Theme.Font, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), px, Theme.Brush(c));
        return f;
    }
    // 한 줄, 폭을 넘으면 … 으로 줄인다
    public static FormattedText Line(string s, double px, bool bold, Color c, double maxW) {
        FormattedText f = Ft(s, px, bold, c);
        if (maxW > 1) { f.MaxTextWidth = maxW; f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis; }
        return f;
    }
    // 사각형 안 세로 가운데에 왼쪽/가운데/오른쪽 정렬로 그린다
    public static void Text(DrawingContext dc, FormattedText f, Rect r, TextAlignment al) {
        f.TextAlignment = al;
        if (f.MaxTextWidth <= 0 || f.MaxTextWidth > r.Width) { f.MaxTextWidth = Math.Max(1, r.Width); f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis; }
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
    static readonly Dictionary<long, Pen> pens = new Dictionary<long, Pen>();
    static Pen CachedPen(Color c, double w, bool round) {
        long key = ((long)(uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B) << 24) | ((long)Math.Round(w * 100) << 1) | (round ? 1L : 0L);
        Pen p;
        if (pens.TryGetValue(key, out p)) return p;
        if (pens.Count > 4096) pens.Clear();
        p = new Pen(Theme.Brush(c), w);
        if (round) { p.StartLineCap = PenLineCap.Round; p.EndLineCap = PenLineCap.Round; p.LineJoin = PenLineJoin.Round; }
        p.Freeze();
        pens[key] = p;
        return p;
    }
    public static Pen RoundPen(Color c, double w) { return CachedPen(c, w, true); }
    public static Pen Pen(Color c, double w) { return CachedPen(c, w, false); }
    public static Geometry Poly(bool closed, params Point[] pts) {
        StreamGeometry g = new StreamGeometry();
        using (StreamGeometryContext c = g.Open()) {
            c.BeginFigure(pts[0], closed, closed);
            for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i], true, true);
        }
        g.Freeze(); return g;
    }

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
            default: return Theme.Accent;
        }
    }

    public static void Icon(DrawingContext dc, string name, double x, double y, double size, double alpha) {
        if (alpha <= 0.004) return;
        Color tc = Theme.A(TileColor(name), alpha), wc = Theme.A(Colors.White, alpha);
        Brush tb = Theme.Brush(tc), wb = Theme.Brush(wc);
        double k = size / 24.0;
        dc.DrawRoundedRectangle(tb, null, new Rect(x, y, size, size), size * 0.27, size * 0.27);
        Pen p = RoundPen(wc, Math.Max(1.2, 1.7 * k));
        switch (name) {
            case "dashboard":   // 네 칸 타일
                for (int i = 0; i < 4; i++) {
                    double cx = x + ((i % 2 == 0) ? 6.5 : 12.5) * k, cy = y + ((i < 2) ? 6.5 : 12.5) * k;
                    dc.DrawRoundedRectangle(wb, null, new Rect(cx, cy, 5 * k, 5 * k), 1.3 * k, 1.3 * k);
                }
                break;
            case "wizard":      // 마법봉 + 반짝임
                dc.DrawLine(p, new Point(x + 6.5 * k, y + 17.5 * k), new Point(x + 13.5 * k, y + 10.5 * k));
                Sparkle(dc, wb, x + 16 * k, y + 8 * k, 3.8 * k);
                Sparkle(dc, wb, x + 8.5 * k, y + 8 * k, 2 * k);
                break;
            case "advanced": {  // 슬라이더 세 줄
                Pen thin = RoundPen(wc, Math.Max(1.0, 1.4 * k));
                double[] ys = new double[] { 7.5, 12, 16.5 }, ks = new double[] { 14.5, 9, 13 };
                for (int i = 0; i < 3; i++) {
                    dc.DrawLine(thin, new Point(x + 6 * k, y + ys[i] * k), new Point(x + 18 * k, y + ys[i] * k));
                    dc.DrawEllipse(tb, thin, new Point(x + ks[i] * k, y + ys[i] * k), 2 * k, 2 * k);
                }
                break;
            }
            case "settings": {  // 톱니바퀴
                double gx = x + 12 * k, gy = y + 12 * k;
                for (int i = 0; i < 8; i++) {
                    dc.PushTransform(new RotateTransform(i * 45, gx, gy));
                    dc.DrawRectangle(wb, null, new Rect(gx - 1.6 * k, gy - 7.2 * k, 3.2 * k, 3.4 * k));
                    dc.Pop();
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
                Pen thin = RoundPen(wc, Math.Max(1.0, 1.4 * k));
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
                    gc.BeginFigure(new Point(c.X, c.Y - 6 * k), true, true);
                    gc.ArcTo(new Point(c.X, c.Y + 6 * k), new Size(6 * k, 6 * k), 0, false, SweepDirection.Clockwise, true, false);
                }
                g.Freeze();
                dc.DrawGeometry(wb, null, g);
                break;
            }
            case "object": {    // 정육면체 (OMSI 오브젝트)
                Point top = new Point(x + 12 * k, y + 5.5 * k), l = new Point(x + 6 * k, y + 8.8 * k), r = new Point(x + 18 * k, y + 8.8 * k);
                Point c = new Point(x + 12 * k, y + 12.1 * k), lb = new Point(x + 6 * k, y + 15.4 * k), rb = new Point(x + 18 * k, y + 15.4 * k), b = new Point(x + 12 * k, y + 18.7 * k);
                dc.DrawGeometry(Theme.Brush(Theme.A(Colors.White, alpha * 0.35)), null, Poly(true, top, r, c, l));
                dc.DrawGeometry(null, p, Poly(true, top, r, rb, b, lb, l));
                dc.DrawGeometry(null, p, Poly(false, l, c, r));
                dc.DrawLine(p, c, b);
                break;
            }
            case "spline": {    // 멀리 뻗은 도로 + 가운데 점선 (OMSI 스플라인)
                dc.DrawLine(p, new Point(x + 10 * k, y + 5.5 * k), new Point(x + 5.5 * k, y + 18.5 * k));
                dc.DrawLine(p, new Point(x + 14 * k, y + 5.5 * k), new Point(x + 18.5 * k, y + 18.5 * k));
                Pen dash = RoundPen(wc, Math.Max(1.0, 1.5 * k));
                dc.DrawLine(dash, new Point(x + 12 * k, y + 6.5 * k), new Point(x + 12 * k, y + 8.5 * k));
                dc.DrawLine(dash, new Point(x + 12 * k, y + 11 * k), new Point(x + 12 * k, y + 13.2 * k));
                dc.DrawLine(dash, new Point(x + 12 * k, y + 15.8 * k), new Point(x + 12 * k, y + 18.3 * k));
                break;
            }
            case "map": {       // 접힌 지도 + 위치 핀 (맵 탭)
                dc.DrawGeometry(null, p, Poly(true, new Point(x + 5 * k, y + 7.5 * k), new Point(x + 9.5 * k, y + 6 * k), new Point(x + 14.5 * k, y + 7.5 * k),
                    new Point(x + 19 * k, y + 6 * k), new Point(x + 19 * k, y + 16.5 * k), new Point(x + 14.5 * k, y + 18 * k), new Point(x + 9.5 * k, y + 16.5 * k), new Point(x + 5 * k, y + 18 * k)));
                Pen thin = RoundPen(wc, Math.Max(1.0, 1.2 * k));
                dc.DrawLine(thin, new Point(x + 9.5 * k, y + 6 * k), new Point(x + 9.5 * k, y + 16.5 * k));
                dc.DrawLine(thin, new Point(x + 14.5 * k, y + 7.5 * k), new Point(x + 14.5 * k, y + 18 * k));
                break;
            }
            case "bus": {       // 버스 옆모습 (버스 탭)
                dc.DrawRoundedRectangle(null, p, new Rect(x + 4.5 * k, y + 7 * k, 15 * k, 8.5 * k), 1.8 * k, 1.8 * k);
                Pen thin = RoundPen(wc, Math.Max(1.0, 1.3 * k));
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
    static void Sparkle(DrawingContext dc, Brush b, double cx, double cy, double r) {
        double i = r * 0.28;
        dc.DrawGeometry(b, null, Poly(true,
            new Point(cx, cy - r), new Point(cx + i, cy - i), new Point(cx + r, cy), new Point(cx + i, cy + i),
            new Point(cx, cy + r), new Point(cx - i, cy + i), new Point(cx - r, cy), new Point(cx - i, cy - i)));
    }

    // ---- 로고: 뒤쪽 그라데이션 타일 + 앞쪽 반투명 타일 + OBJ/2X 글자 (전체 크기 size)
    public static void Logo(DrawingContext dc, double x, double y, double size) {
        double s = size / 172.0;
        Rect back = new Rect(x + 22 * s, y + 22 * s, 150 * s, 150 * s);
        LinearGradientBrush gb = new LinearGradientBrush(Theme.Accent, Theme.AccentHov, new Point(0, 0), new Point(1, 1));
        gb.Freeze();
        dc.DrawRoundedRectangle(gb, null, back, 34 * s, 34 * s);
        dc.DrawRoundedRectangle(Theme.Brush(Theme.A(Theme.Accent, 150 / 255.0)), Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1, 1.5 * s)), new Rect(x, y, 150 * s, 150 * s), 34 * s, 34 * s);
        // 앞 타일 위에 흰 버스 앞모습 (Bus Depot): 행선지 띠, 앞 유리, 전조등, 바퀴
        Brush wb = Theme.Brush(Colors.White), ab = Theme.Brush(Theme.Mix(Theme.Accent, Colors.Black, 0.15)), lb = Theme.Brush(Color.FromRgb(255, 214, 102));
        dc.DrawRoundedRectangle(ab, null, new Rect(x + 42 * s, y + 108 * s, 18 * s, 22 * s), 5 * s, 5 * s);
        dc.DrawRoundedRectangle(ab, null, new Rect(x + 90 * s, y + 108 * s, 18 * s, 22 * s), 5 * s, 5 * s);
        dc.DrawRoundedRectangle(wb, null, new Rect(x + 32 * s, y + 24 * s, 86 * s, 92 * s), 16 * s, 16 * s);
        dc.DrawRoundedRectangle(ab, null, new Rect(x + 44 * s, y + 31 * s, 62 * s, 9 * s), 3 * s, 3 * s);
        dc.DrawRoundedRectangle(ab, null, new Rect(x + 40 * s, y + 46 * s, 70 * s, 38 * s), 7 * s, 7 * s);
        dc.DrawEllipse(lb, null, new Point(x + 49 * s, y + 99 * s), 6 * s, 6 * s);
        dc.DrawEllipse(lb, null, new Point(x + 101 * s, y + 99 * s), 6 * s, 6 * s);
        dc.DrawRoundedRectangle(ab, null, new Rect(x + 62 * s, y + 96 * s, 26 * s, 6 * s), 3 * s, 3 * s);
    }

    // 아이콘 파일(.ico): 16~256 크기를 PNG로 담는다 (개발용 --make-icon)
    public static void WriteIco(string path) {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int z in sizes) {
            BitmapSource b = Render(z, z, delegate(DrawingContext dc) { Logo(dc, z * 0.02, z * 0.02, z * 0.96); });
            PngBitmapEncoder enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(b));
            using (MemoryStream ms = new MemoryStream()) { enc.Save(ms); pngs.Add(ms.ToArray()); }
        }
        using (BinaryWriter w = new BinaryWriter(File.Create(path))) {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int off = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++) {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32); w.Write(pngs[i].Length); w.Write(off);
                off += pngs[i].Length;
            }
            foreach (byte[] b in pngs) w.Write(b);
        }
    }

    public static BitmapSource Render(int w, int h, Action<DrawingContext> draw) {
        DrawingVisual v = new DrawingVisual();
        using (DrawingContext dc = v.RenderOpen()) draw(dc);
        RenderTargetBitmap b = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        b.Render(v); b.Freeze();
        return b;
    }

    // 창 아이콘 (로고를 64px로)
    public static ImageSource AppIcon() {
        return Render(64, 64, delegate(DrawingContext dc) { Logo(dc, 4, 4, 56); });
    }
}

// 색 타일 아이콘 하나 (환영 화면의 언어/보기 줄)
public class TileIcon : FrameworkElement {
    public static readonly DependencyProperty NameKeyProperty = DependencyProperty.Register("NameKey", typeof(string), typeof(TileIcon),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public string NameKey { get { return (string)GetValue(NameKeyProperty); } set { SetValue(NameKeyProperty, value); } }
    protected override void OnRender(DrawingContext dc) { Art.Icon(dc, NameKey, 0, 0, Math.Min(ActualWidth, ActualHeight), IsEnabled ? 1 : 0.5); }
}

// 로고 컨트롤 (사이드바, 환영 화면)
public class LogoArt : FrameworkElement {
    public LogoArt() { Loaded += delegate { Theme.Changed += InvalidateVisual; }; Unloaded += delegate { Theme.Changed -= InvalidateVisual; }; }
    protected override void OnRender(DrawingContext dc) {
        double s = Math.Min(ActualWidth, ActualHeight);
        Art.Logo(dc, 0, 0, s);
    }
}
}
