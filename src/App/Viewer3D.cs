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
    public Model3DGroup Visual;    // 화면에 넣는 껍데기 (애니메이션 변환을 여기에 건다, 화면 스레드에서 만든다)
    public Rect3D Bounds = Rect3D.Empty;
    public int Tris;
    public string Name { get; set; }
    public string Sub { get; set; }
    public bool Checked { get { return chk; } set { Set(ref chk, value); } }
}

public static class ModelScene {
    // 부품 하나를 3D 모델로. o3d 좌표(왼손, y 위, z 앞)를 WPF(오른손, y 위)로: z를 뒤집는다. 양면으로 그려서 면 방향은 신경 쓰지 않는다
    // twoSided=false면 OMSI처럼 앞면만 그린다 (노멀이 뒤집힌 면은 보이지 않음)
    public static PartItem Build(ModelCfg cfg, CfgMesh cm, Dictionary<string, Material> cache, List<string> missing, bool twoSided) {
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
                for (int kk = 0; kk < 3; kk++) {
                    int k = kk == 0 ? 0 : 3 - kk;   // 0, 2, 1: z를 뒤집어 거울상이 됐으니 감기는 방향을 되돌린다
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
            GeometryModel3D gm = new GeometryModel3D(geo, mat); if (twoSided) gm.BackMaterial = mat;
            gm.Freeze();
            g.Children.Add(gm);
        }
        g.Freeze();
        it.Model = g; it.Bounds = g.Bounds;
        // origin_from_mesh용: o3d 행렬의 기준점과 x축 (o3d (x, y위, z앞) → cfg (x, z, y))
        if (m.Matrix != null) {
            cm.MeshOrigin = new double[] { m.Matrix[12], m.Matrix[14], m.Matrix[13] };
            cm.MeshAxis = new double[] { m.Matrix[0], m.Matrix[2], m.Matrix[1] };
        }
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

// 덧그림의 손잡이(사람 길 점, 좌석)를 끌 때 VM에 넘기는 값. Delta는 처음 누른 곳에서 움직인 양(화면 좌표계), Snap은 Shift를 누른 채
public class GizmoDrag { public object Handle; public Vector3D Delta; public bool Start, Done, Snap; }
// 축 화살표 손잡이: 끌면 Target을 Axis(화면 좌표계 단위 벡터) 방향으로만 옮긴다
public class GizmoAxis { public object Target; public Vector3D Axis; }

// ---------------------------------------------------------------- 3D 화면: 끌어서 돌리기, 오른쪽 버튼(또는 가운데)으로 옮기기, 휠로 확대, 두 번 눌러 처음 위치
public class ModelView : Border {
    public static readonly DependencyProperty PartsProperty = DependencyProperty.Register("Parts", typeof(IEnumerable<PartItem>), typeof(ModelView),
        new FrameworkPropertyMetadata(null, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(true); }));
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register("Scene", typeof(int), typeof(ModelView),
        new FrameworkPropertyMetadata(0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(true); }));
    public static readonly DependencyProperty ShowProperty = DependencyProperty.Register("Show", typeof(int), typeof(ModelView),
        new FrameworkPropertyMetadata(0, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(false); }));
    // 사람 길, 좌석, 피벗 같은 덧그림 (VM이 만든다)
    public static readonly DependencyProperty OverlayProperty = DependencyProperty.Register("Overlay", typeof(Model3D), typeof(ModelView),
        new FrameworkPropertyMetadata(null, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).overlay.Content = e.NewValue as Model3D; }));
    // 부품을 누르면 그 PartItem으로 실행 (끌어서 돌린 것은 제외)
    public static readonly DependencyProperty PickCommandProperty = DependencyProperty.Register("PickCommand", typeof(ICommand), typeof(ModelView));
    // 덧그림 도형에 붙이는 손잡이 표시. 붙은 도형을 누르고 끌면 MoveCommand로 GizmoDrag를 보낸다 (Blender의 G처럼 화면과 평행한 면 위에서 자유 이동)
    public static readonly DependencyProperty HandleProperty = DependencyProperty.RegisterAttached("Handle", typeof(object), typeof(ModelView));
    public static readonly DependencyProperty MoveCommandProperty = DependencyProperty.Register("MoveCommand", typeof(ICommand), typeof(ModelView));
    public ICommand MoveCommand { get { return (ICommand)GetValue(MoveCommandProperty); } set { SetValue(MoveCommandProperty, value); } }
    object gHandle; Point3D gStart;
    Vector3D gAxis; bool gOnAxis; double gS0;
    // 켜면 부품을 모두 숨긴다 (덧그림만 보기)
    public static readonly DependencyProperty HidePartsProperty = DependencyProperty.Register("HideParts", typeof(bool), typeof(ModelView),
        new FrameworkPropertyMetadata(false, delegate(DependencyObject d, DependencyPropertyChangedEventArgs e) { ((ModelView)d).Rebuild(false); }));
    public bool HideParts { get { return (bool)GetValue(HidePartsProperty); } set { SetValue(HidePartsProperty, value); } }
    public Model3D Overlay { get { return (Model3D)GetValue(OverlayProperty); } set { SetValue(OverlayProperty, value); } }
    public ICommand PickCommand { get { return (ICommand)GetValue(PickCommandProperty); } set { SetValue(PickCommandProperty, value); } }
    readonly ModelVisual3D overlay = new ModelVisual3D();
    Point downAt;
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
        vp.Children.Add(overlay);
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
        if (Parts != null && !HideParts) foreach (PartItem p in Parts) {
            if (!p.Checked || p.Model == null) continue;
            content.Children.Add(p.Visual != null ? (Model3D)p.Visual : p.Model);
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
        last = e.GetPosition(this); downAt = last;
        if (e.ChangedButton == MouseButton.Left && MoveCommand != null) {
            Point3D at; object h = HitHandle(e.GetPosition(vp), out at);
            if (h != null) {
                GizmoAxis ga = h as GizmoAxis;
                gOnAxis = ga != null;
                if (gOnAxis) { h = ga.Target; gAxis = ga.Axis; gAxis.Normalize(); }
                gHandle = h; gStart = at; CaptureMouse(); e.Handled = true;
                if (gOnAxis) gS0 = AxisS(e.GetPosition(vp));
                Send(new GizmoDrag { Handle = h, Start = true });
                return;
            }
        }
        rot = e.ChangedButton == MouseButton.Left; pan = e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle;
        CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseUp(MouseButtonEventArgs e) {
        base.OnMouseUp(e);
        if (gHandle != null) {
            Send(new GizmoDrag { Handle = gHandle, Delta = DragDelta(e.GetPosition(vp)), Done = true, Snap = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 });
            gHandle = null; ReleaseMouseCapture(); e.Handled = true; return;
        }
        bool click = rot && (e.GetPosition(this) - downAt).Length < 4;
        rot = pan = false; ReleaseMouseCapture(); e.Handled = true;
        if (click && PickCommand != null) Pick(e.GetPosition(vp));
    }
    void Send(GizmoDrag g) { if (MoveCommand != null && MoveCommand.CanExecute(g)) MoveCommand.Execute(g); }

    // 가장 앞에 맞은 도형이 손잡이면 그것 (가려진 것은 잡지 않는다)
    object HitHandle(Point pt, out Point3D at) {
        object found = null; Point3D p = new Point3D();
        VisualTreeHelper.HitTest(vp, null, delegate(HitTestResult r) {
            RayMeshGeometry3DHitTestResult m = r as RayMeshGeometry3DHitTestResult;
            if (m == null) return HitTestResultBehavior.Continue;
            found = m.ModelHit.GetValue(HandleProperty); p = m.PointHit;
            return HitTestResultBehavior.Stop;
        }, new PointHitTestParameters(pt));
        at = p;
        return found;
    }

    // 마우스 아래를 지나는 시선 (카메라 위치에서 나가는 방향)
    Vector3D Ray(Point pt) {
        double w = Math.Max(1, vp.ActualWidth), h = Math.Max(1, vp.ActualHeight);
        Vector3D look = cam.LookDirection; look.Normalize();
        Vector3D right = Vector3D.CrossProduct(look, cam.UpDirection); right.Normalize();
        Vector3D up = Vector3D.CrossProduct(right, look);
        double t = Math.Tan(cam.FieldOfView / 2 * Math.PI / 180);
        return look + right * ((2 * pt.X / w - 1) * t) + up * ((1 - 2 * pt.Y / h) * t * h / w);
    }
    // 축 직선(처음 잡은 점 + gAxis·s)에서 시선과 가장 가까운 곳의 s
    double AxisS(Point pt) {
        Vector3D d = Ray(pt), w0 = gStart - cam.Position;
        double b = Vector3D.DotProduct(gAxis, d), c = Vector3D.DotProduct(d, d), dd = Vector3D.DotProduct(gAxis, w0), e = Vector3D.DotProduct(d, w0);
        double den = c - b * b;   // gAxis는 단위 벡터
        if (Math.Abs(den) < 1e-9) return gS0;
        return (b * e - c * dd) / den;
    }

    // 마우스 아래를 지나는 시선이 처음 잡은 점을 지나고 화면과 평행한 면과 만나는 곳 - 처음 잡은 점 (축 손잡이면 그 축 위로만)
    Vector3D DragDelta(Point pt) {
        double w = vp.ActualWidth, h = vp.ActualHeight;
        if (w < 1 || h < 1) return new Vector3D();
        if (gOnAxis) return gAxis * (AxisS(pt) - gS0);
        Vector3D look = cam.LookDirection; look.Normalize();
        Vector3D right = Vector3D.CrossProduct(look, cam.UpDirection); right.Normalize();
        Vector3D up = Vector3D.CrossProduct(right, look);
        double t = Math.Tan(cam.FieldOfView / 2 * Math.PI / 180);
        Vector3D dir = look + right * ((2 * pt.X / w - 1) * t) + up * ((1 - 2 * pt.Y / h) * t * h / w);
        double den = Vector3D.DotProduct(dir, look);
        if (Math.Abs(den) < 1e-9) return new Vector3D();
        double k = Vector3D.DotProduct(gStart - cam.Position, look) / den;
        return (cam.Position + dir * k) - gStart;
    }

    void Pick(Point pt) {
        PartItem hit = null;
        VisualTreeHelper.HitTest(vp, null, delegate(HitTestResult r) {
            RayMeshGeometry3DHitTestResult m = r as RayMeshGeometry3DHitTestResult;
            if (m == null || Parts == null) return HitTestResultBehavior.Continue;
            foreach (PartItem p in Parts) {
                Model3DGroup g = p.Model as Model3DGroup;
                if (p.Checked && g != null && g.Children.Contains(m.ModelHit)) { hit = p; return HitTestResultBehavior.Stop; }
            }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(pt));
        if (hit != null && PickCommand.CanExecute(hit)) PickCommand.Execute(hit);
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (gHandle != null) { Send(new GizmoDrag { Handle = gHandle, Delta = DragDelta(e.GetPosition(vp)), Snap = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 }); return; }
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
