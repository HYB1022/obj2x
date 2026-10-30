using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Obj2x.UI {

// ---------------------------------------------------------------- 텍스처 읽기: bmp/png/jpg는 WPF로, tga/dds는 직접 (OMSI에서 많이 쓰는 형식)
public static class TexLoad {
    public static BitmapSource Load(string path, int max) {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        BitmapSource b;
        if (ext == ".tga") b = Tga(File.ReadAllBytes(path));
        else if (ext == ".dds") b = Dds(File.ReadAllBytes(path));
        else {
            using (FileStream fs = File.OpenRead(path)) {
                BitmapDecoder dec = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                b = dec.Frames[0];
            }
        }
        if (b == null) return null;
        if (b.PixelWidth > max || b.PixelHeight > max) {
            double s = (double)max / Math.Max(b.PixelWidth, b.PixelHeight);
            b = new TransformedBitmap(b, new ScaleTransform(s, s));
        }
        b.Freeze();
        return b;
    }

    static BitmapSource Make(int w, int h, byte[] bgra) { return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4); }

    // TGA: 무압축/RLE, 24/32비트 컬러와 8비트 회색
    public static BitmapSource Tga(byte[] d) {
        int idLen = d[0], type = d[2], cmLen = d[5] | (d[6] << 8), cmBits = d[7];
        int w = d[12] | (d[13] << 8), h = d[14] | (d[15] << 8), bpp = d[16], desc = d[17];
        if (w <= 0 || h <= 0 || (type != 2 && type != 3 && type != 10 && type != 11)) return null;
        int px = bpp / 8, p = 18 + idLen + cmLen * ((cmBits + 7) / 8);
        if (px < 1 || px > 4) return null;
        byte[] o = new byte[w * h * 4];
        bool rle = type >= 9, top = (desc & 0x20) != 0;
        int n = 0, total = w * h;
        Action<int> put = delegate(int src) {
            int x = n % w, y = n / w; if (!top) y = h - 1 - y;
            int t = (y * w + x) * 4;
            if (px == 1) { o[t] = o[t + 1] = o[t + 2] = d[src]; o[t + 3] = 255; }
            else if (px == 2) { int v = d[src] | (d[src + 1] << 8); o[t] = (byte)((v & 31) << 3); o[t + 1] = (byte)(((v >> 5) & 31) << 3); o[t + 2] = (byte)(((v >> 10) & 31) << 3); o[t + 3] = 255; }
            else { o[t] = d[src]; o[t + 1] = d[src + 1]; o[t + 2] = d[src + 2]; o[t + 3] = px == 4 ? d[src + 3] : (byte)255; }
            n++;
        };
        while (n < total && p + px <= d.Length) {
            if (!rle) { put(p); p += px; continue; }
            int c = d[p++], cnt = (c & 127) + 1;
            if ((c & 128) != 0) { for (int i = 0; i < cnt && n < total; i++) put(p); p += px; }
            else for (int i = 0; i < cnt && n < total && p + px <= d.Length; i++) { put(p); p += px; }
        }
        return Make(w, h, o);
    }

    // DDS: DXT1/DXT3/DXT5와 무압축 32/24비트 (가장 큰 밉맵 한 장)
    public static BitmapSource Dds(byte[] d) {
        if (d.Length < 128 || d[0] != 'D' || d[1] != 'D' || d[2] != 'S') return null;
        int h = BitConverter.ToInt32(d, 12), w = BitConverter.ToInt32(d, 16);
        uint pfFlags = BitConverter.ToUInt32(d, 80);
        string four = System.Text.Encoding.ASCII.GetString(d, 84, 4);
        int bits = BitConverter.ToInt32(d, 88);
        byte[] o = new byte[w * h * 4];
        int p = 128;
        if ((pfFlags & 4) != 0 && (four == "DXT1" || four == "DXT3" || four == "DXT5")) {
            int bw = (w + 3) / 4, bh = (h + 3) / 4, blk = four == "DXT1" ? 8 : 16;
            if (p + (long)bw * bh * blk > d.Length) return null;
            int[] cr = new int[4], cg = new int[4], cb = new int[4], ca = new int[4];
            byte[] al = new byte[16];
            for (int by = 0; by < bh; by++) for (int bx = 0; bx < bw; bx++) {
                if (four == "DXT3") { for (int i = 0; i < 16; i++) al[i] = (byte)(((d[p + i / 2] >> ((i & 1) * 4)) & 15) * 17); p += 8; }
                else if (four == "DXT5") {
                    int a0 = d[p], a1 = d[p + 1];
                    ulong bitsA = 0; for (int i = 0; i < 6; i++) bitsA |= (ulong)d[p + 2 + i] << (8 * i);
                    for (int i = 0; i < 16; i++) {
                        int code = (int)((bitsA >> (3 * i)) & 7), a;
                        if (code == 0) a = a0; else if (code == 1) a = a1;
                        else if (a0 > a1) a = ((8 - code) * a0 + (code - 1) * a1) / 7;
                        else a = code == 6 ? 0 : code == 7 ? 255 : ((6 - code) * a0 + (code - 1) * a1) / 5;
                        al[i] = (byte)a;
                    }
                    p += 8;
                } else for (int i = 0; i < 16; i++) al[i] = 255;
                int c0 = d[p] | (d[p + 1] << 8), c1 = d[p + 2] | (d[p + 3] << 8);
                uint idx = BitConverter.ToUInt32(d, p + 4); p += 8;
                cr[0] = (c0 >> 11) * 255 / 31; cg[0] = ((c0 >> 5) & 63) * 255 / 63; cb[0] = (c0 & 31) * 255 / 31;
                cr[1] = (c1 >> 11) * 255 / 31; cg[1] = ((c1 >> 5) & 63) * 255 / 63; cb[1] = (c1 & 31) * 255 / 31;
                ca[0] = ca[1] = ca[2] = ca[3] = 255;
                if (c0 > c1 || four != "DXT1") {
                    cr[2] = (2 * cr[0] + cr[1]) / 3; cg[2] = (2 * cg[0] + cg[1]) / 3; cb[2] = (2 * cb[0] + cb[1]) / 3;
                    cr[3] = (cr[0] + 2 * cr[1]) / 3; cg[3] = (cg[0] + 2 * cg[1]) / 3; cb[3] = (cb[0] + 2 * cb[1]) / 3;
                } else {
                    cr[2] = (cr[0] + cr[1]) / 2; cg[2] = (cg[0] + cg[1]) / 2; cb[2] = (cb[0] + cb[1]) / 2;
                    cr[3] = cg[3] = cb[3] = 0; ca[3] = 0;
                }
                for (int i = 0; i < 16; i++) {
                    int x = bx * 4 + (i & 3), y = by * 4 + (i >> 2);
                    if (x >= w || y >= h) continue;
                    int k = (int)((idx >> (2 * i)) & 3), t = (y * w + x) * 4;
                    o[t] = (byte)cb[k]; o[t + 1] = (byte)cg[k]; o[t + 2] = (byte)cr[k];
                    o[t + 3] = (byte)(four == "DXT1" ? ca[k] : al[i]);
                }
            }
        } else if (bits == 32 || bits == 24) {
            int px = bits / 8;
            uint rm = BitConverter.ToUInt32(d, 92), am = BitConverter.ToUInt32(d, 104);
            bool rgba = rm == 0xFF;   // R가 가장 낮은 바이트면 RGBA 순서
            for (int i = 0; i < w * h && p + px <= d.Length; i++, p += px) {
                int t = i * 4;
                o[t] = d[p + (rgba ? 2 : 0)]; o[t + 1] = d[p + 1]; o[t + 2] = d[p + (rgba ? 0 : 2)];
                o[t + 3] = px == 4 && am != 0 ? d[p + 3] : (byte)255;
            }
        } else return null;
        return Make(w, h, o);
    }
}

// ---------------------------------------------------------------- 모델 보기의 부품 한 줄 (목록의 스위치 = 3D 화면에 보이기)
public class PartItem : Observable {
    bool chk;
    public CfgMesh Mesh;
    public Model3D Model;          // 다른 스레드에서 만들어 굳힌(Freeze) 것
    public Rect3D Bounds = Rect3D.Empty;
    public int Tris;
    public string Name { get; set; }
    public string Sub { get; set; }
    public bool Checked { get { return chk; } set { Set(ref chk, value); } }
}

public static class ModelScene {
    // 부품 하나를 3D 모델로. o3d 좌표(왼손, y 위, z 앞)를 WPF(오른손, y 위)로: z를 뒤집는다. 양면으로 그려서 면 방향은 신경 쓰지 않는다
    public static PartItem Build(ModelCfg cfg, CfgMesh cm, Dictionary<string, Material> cache, List<string> missing) {
        PartItem it = new PartItem(); it.Mesh = cm; it.Name = cm.File;
        O3dModel m;
        try { m = O3dModel.Load(cm.Path); }
        catch (Exception ex) { it.Sub = cm.Exists ? ex.Message : L.T("view_part_nofile"); return it; }
        Model3DGroup g = new Model3DGroup();
        int nm = Math.Max(1, m.Mats.Count);
        for (int mi = 0; mi < nm; mi++) {
            Point3DCollection pos = new Point3DCollection(); Vector3DCollection nor = new Vector3DCollection();
            PointCollection uv = new PointCollection(); Int32Collection idx = new Int32Collection();
            Dictionary<int, int> remap = new Dictionary<int, int>();
            for (int t = 0; t < m.TCount; t++) {
                if (m.Mats.Count > 0 && m.T[t * 4 + 3] != mi) continue;
                for (int k = 0; k < 3; k++) {
                    int v = m.T[t * 4 + k], r;
                    if (!remap.TryGetValue(v, out r)) {
                        r = pos.Count; remap[v] = r;
                        int o = v * 8;
                        pos.Add(new Point3D(m.V[o], m.V[o + 1], -m.V[o + 2]));
                        nor.Add(new Vector3D(m.V[o + 3], m.V[o + 4], -m.V[o + 5]));
                        uv.Add(new Point(m.V[o + 6], m.V[o + 7]));
                    }
                    idx.Add(r);
                }
            }
            if (idx.Count == 0) continue;
            it.Tris += idx.Count / 3;
            MeshGeometry3D geo = new MeshGeometry3D();
            geo.Positions = pos; geo.Normals = nor; geo.TextureCoordinates = uv; geo.TriangleIndices = idx;
            geo.Freeze();
            Material mat = MaterialFor(cfg, cm, mi < m.Mats.Count ? m.Mats[mi] : new O3dMaterial(), cache, missing);
            GeometryModel3D gm = new GeometryModel3D(geo, mat); gm.BackMaterial = mat;
            gm.Freeze();
            g.Children.Add(gm);
        }
        g.Freeze();
        it.Model = g; it.Bounds = g.Bounds;
        it.Sub = L.T("view_part_sub", it.Tris.ToString("N0"), m.Mats.Count) + (cm.VisVar != null ? " · [visible] " + cm.VisVar + " = " + cm.VisVal : "")
            + (cm.Lod > 0 ? " · LOD " + cm.LodValue.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
        return it;
    }

    static Material MaterialFor(ModelCfg cfg, CfgMesh cm, O3dMaterial mt, Dictionary<string, Material> cache, List<string> missing) {
        string f = mt.Tex.Length > 0 ? cfg.FindTex(mt.Tex, cm) : null;
        Color dc = Color.FromRgb(Clamp(mt.Diffuse[0]), Clamp(mt.Diffuse[1]), Clamp(mt.Diffuse[2]));
        bool alpha = cm.UsesAlpha(mt.Tex);
        string key = f != null ? f.ToLowerInvariant() + (alpha ? "|a" : "") : "#" + dc.ToString();
        Material mat;
        if (cache.TryGetValue(key, out mat)) return mat;
        Brush b = null;
        if (f != null) {
            try {
                BitmapSource bs = TexLoad.Load(f, 1024);
                // [matl_alpha]가 없으면 알파 채널을 버린다 (OMSI처럼 불투명하게)
                if (bs != null && !alpha && bs.Format != PixelFormats.Bgr32 && bs.Format != PixelFormats.Bgr24) { bs = new FormatConvertedBitmap(bs, PixelFormats.Bgr32, null, 0); bs.Freeze(); }
                if (bs != null) {
                    ImageBrush ib = new ImageBrush(bs); ib.ViewportUnits = BrushMappingMode.Absolute; ib.TileMode = TileMode.Tile; ib.Viewport = new Rect(0, 0, 1, 1);
                    b = ib;
                }
            } catch (Exception) { }
        } else if (mt.Tex.Length > 0) { lock (missing) if (!missing.Contains(mt.Tex)) missing.Add(mt.Tex); }
        if (b == null) b = new SolidColorBrush(f != null || mt.Tex.Length == 0 ? dc : Color.FromRgb(200, 200, 205));
        b.Freeze();
        mat = new DiffuseMaterial(b); mat.Freeze();
        cache[key] = mat;
        return mat;
    }
    static byte Clamp(float v) { return (byte)Math.Max(0, Math.Min(255, v * 255 * 1.25)); }
}

// ---------------------------------------------------------------- 3D 화면: 끌어서 돌리기, 오른쪽 버튼(또는 가운데)으로 옮기기, 휠로 확대, 두 번 눌러 처음 위치
public class ModelView : Border {
    public static readonly DependencyProperty PartsProperty = DependencyProperty.Register("Parts", typeof(IEnumerable<PartItem>), typeof(ModelView),
        new FrameworkPropertyMetadata(null, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(true); }));
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register("Scene", typeof(int), typeof(ModelView),
        new FrameworkPropertyMetadata(0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(true); }));
    public static readonly DependencyProperty ShowProperty = DependencyProperty.Register("Show", typeof(int), typeof(ModelView),
        new FrameworkPropertyMetadata(0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(false); }));
    public IEnumerable<PartItem> Parts { get { return (IEnumerable<PartItem>)GetValue(PartsProperty); } set { SetValue(PartsProperty, value); } }
    public int Scene { get { return (int)GetValue(SceneProperty); } set { SetValue(SceneProperty, value); } }
    public int Show { get { return (int)GetValue(ShowProperty); } set { SetValue(ShowProperty, value); } }

    readonly Viewport3D vp = new Viewport3D();
    readonly PerspectiveCamera cam = new PerspectiveCamera();
    readonly Model3DGroup content = new Model3DGroup();
    readonly DirectionalLight head = new DirectionalLight(Color.FromRgb(150, 150, 150), new Vector3D(0, 0, -1));
    double yaw = 35, pitch = 18, dist = 10, fitDist = 10;
    Point3D target = new Point3D();
    Point last; bool rot, pan;

    public ModelView() {
        CornerRadius = new CornerRadius(16);
        SetResourceReference(BackgroundProperty, "Bg");
        ClipToBounds = true;
        cam.FieldOfView = 40; cam.NearPlaneDistance = 0.05; cam.FarPlaneDistance = 5000;
        vp.Camera = cam;
        Model3DGroup lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(120, 120, 120)));
        lights.Children.Add(head);
        lights.Children.Add(new DirectionalLight(Color.FromRgb(70, 70, 70), new Vector3D(0.4, -1, 0.3)));
        ModelVisual3D lv = new ModelVisual3D(); lv.Content = lights; vp.Children.Add(lv);
        ModelVisual3D mv = new ModelVisual3D(); mv.Content = content; vp.Children.Add(mv);
        Child = vp;
        Cursor = Cursors.SizeAll;
        SizeChanged += delegate { Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 16, 16); };
        UpdateCam();
    }

    void Rebuild(bool refit) {
        content.Children.Clear();
        Rect3D box = Rect3D.Empty;
        List<PartItem> on = new List<PartItem>();
        int total = 0;
        if (Parts != null) foreach (PartItem p in Parts) {
            if (!p.Checked || p.Model == null) continue;
            content.Children.Add(p.Model);
            on.Add(p); total += p.Tris;
        }
        if (!refit) return;
        // 화면 맞춤은 삼각형의 95%를 차지하는 큰 부품들로 (애니메이션으로 옮겨지는 작은 표지판 등이 멀리 있어도 버스가 가운데에 오게)
        on.Sort(delegate(PartItem a, PartItem b) { return b.Tris.CompareTo(a.Tris); });
        int acc = 0;
        foreach (PartItem p in on) { if (acc >= total * 0.95 && !box.IsEmpty) break; box.Union(p.Bounds); acc += p.Tris; }
        if (box.IsEmpty) return;
        target = new Point3D(box.X + box.SizeX / 2, box.Y + box.SizeY / 2, box.Z + box.SizeZ / 2);
        double r = Math.Sqrt(box.SizeX * box.SizeX + box.SizeY * box.SizeY + box.SizeZ * box.SizeZ) / 2;
        fitDist = dist = Math.Max(0.5, r / Math.Tan(cam.FieldOfView / 2 * Math.PI / 180) * 1.25);
        yaw = 35; pitch = 18;
        UpdateCam();
    }
    public int Visible { get { return content.Children.Count; } }   // 셀프 테스트용

    void UpdateCam() {
        double y = yaw * Math.PI / 180, p = pitch * Math.PI / 180;
        // 버스 앞(o3d +z → 화면 -z)을 비스듬히 보는 위치에서 시작
        Vector3D dir = new Vector3D(Math.Sin(y) * Math.Cos(p), Math.Sin(p), -Math.Cos(y) * Math.Cos(p));
        cam.Position = target + dir * dist;
        cam.LookDirection = -dir * dist;
        cam.UpDirection = new Vector3D(0, 1, 0);
        cam.NearPlaneDistance = Math.Max(0.01, dist / 500);
        head.Direction = -dir;
    }

    // 개발용(소개 영상): 각도를 정해서 돌린다
    public void Orbit(double yawDeg, double pitchDeg, double zoom) { yaw = yawDeg; pitch = pitchDeg; dist = fitDist * zoom; UpdateCam(); }

    protected override void OnMouseDown(MouseButtonEventArgs e) {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) { Rebuild(true); return; }
        last = e.GetPosition(this);
        rot = e.ChangedButton == MouseButton.Left; pan = e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle;
        CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseUp(MouseButtonEventArgs e) { base.OnMouseUp(e); rot = pan = false; ReleaseMouseCapture(); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (!rot && !pan) return;
        Point p = e.GetPosition(this); Vector d = p - last; last = p;
        if (rot) { yaw += d.X * 0.4; pitch = Math.Max(-89, Math.Min(89, pitch + d.Y * 0.4)); }
        else {
            // 화면 평면으로 옮기기 (거리에 비례)
            Vector3D look = cam.LookDirection; look.Normalize();
            Vector3D right = Vector3D.CrossProduct(look, new Vector3D(0, 1, 0)); right.Normalize();
            Vector3D up = Vector3D.CrossProduct(right, look);
            double k = dist * 0.0018;
            target += right * (-d.X * k) + up * (d.Y * k);
        }
        UpdateCam();
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e) {
        base.OnMouseWheel(e);
        dist = Math.Max(0.2, Math.Min(fitDist * 6, dist * (e.Delta > 0 ? 0.88 : 1 / 0.88)));
        UpdateCam(); e.Handled = true;
    }
}
}
