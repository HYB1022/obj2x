using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;

namespace Obj2x.UI;

// ---------------------------------------------------------------- 3D 화면: 끌어서 돌리기, 오른쪽 버튼(또는 가운데)으로 옮기기, 휠로 확대, 두 번 눌러 처음 위치
// 그리기는 OpenGL (안쪽 GlSurface). 조명/카메라는 예전 WPF 화면과 같게 맞췄다 (가로 화각 40도, 주변광 + 시선 방향 빛 + 위쪽 빛).
public class ModelView : Border {
    public static readonly StyledProperty<IEnumerable<PartItem>> PartsProperty = AvaloniaProperty.Register<ModelView, IEnumerable<PartItem>>(nameof(Parts));
    public static readonly StyledProperty<int> SceneProperty = AvaloniaProperty.Register<ModelView, int>(nameof(Scene));
    public static readonly StyledProperty<int> ShowProperty = AvaloniaProperty.Register<ModelView, int>(nameof(Show));
    // 사람 길, 좌석, 피벗 같은 덧그림 (VM이 만든다)
    public static readonly StyledProperty<Model3D> OverlayProperty = AvaloniaProperty.Register<ModelView, Model3D>(nameof(Overlay));
    // 부품을 누르면 그 PartItem으로 실행 (끌어서 돌린 것은 제외)
    public static readonly StyledProperty<ICommand> PickCommandProperty = AvaloniaProperty.Register<ModelView, ICommand>(nameof(PickCommand));
    // 덧그림 도형의 손잡이(GeometryModel3D.Handle)를 누르고 끌면 MoveCommand로 GizmoDrag를 보낸다 (Blender의 G처럼 화면과 평행한 면 위에서 자유 이동)
    public static readonly StyledProperty<ICommand> MoveCommandProperty = AvaloniaProperty.Register<ModelView, ICommand>(nameof(MoveCommand));
    // 켜면 부품을 모두 숨긴다 (덧그림만 보기)
    public static readonly StyledProperty<bool> HidePartsProperty = AvaloniaProperty.Register<ModelView, bool>(nameof(HideParts));
    public IEnumerable<PartItem> Parts { get => GetValue(PartsProperty); set => SetValue(PartsProperty, value); }
    public int Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public int Show { get => GetValue(ShowProperty); set => SetValue(ShowProperty, value); }
    public Model3D Overlay { get => GetValue(OverlayProperty); set => SetValue(OverlayProperty, value); }
    public ICommand PickCommand { get => GetValue(PickCommandProperty); set => SetValue(PickCommandProperty, value); }
    public ICommand MoveCommand { get => GetValue(MoveCommandProperty); set => SetValue(MoveCommandProperty, value); }
    public bool HideParts { get => GetValue(HidePartsProperty); set => SetValue(HidePartsProperty, value); }

    internal readonly List<Model3D> drawList = new();     // 불투명 → 투명 순서
    internal const double Fov = 40;
    internal double yaw = 35, pitch = 18, dist = 10, fitDist = 10;
    internal Point3D target;
    internal Point3D camPos; internal Vector3D look, up = new(0, 1, 0);
    readonly GlSurface surface;
    object gHandle; Point3D gStart; Vector3D gAxis; bool gOnAxis; double gS0;
    Point last, downAt; bool rot, pan;
    int shownParts;
    public int Visible => shownParts;   // 셀프 테스트용 (보이는 부품 수)

    public ModelView() {
        CornerRadius = new CornerRadius(16);
        Res.Dyn(this, BackgroundProperty, "Bg");
        ClipToBounds = true;
        surface = new GlSurface(this);
        Child = surface;
        Cursor = new Cursor(StandardCursorType.SizeAll);
        Look.Changed += Redraw;
        UpdateCam();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (e.Property == PartsProperty || e.Property == SceneProperty) Rebuild(true);
        else if (e.Property == ShowProperty || e.Property == HidePartsProperty) Rebuild(false);
        else if (e.Property == OverlayProperty) Redraw();
    }

    public void Redraw() { surface.RequestNextFrameRendering(); }

    void Rebuild(bool refit) {
        drawList.Clear(); shownParts = 0;
        Rect3D box = Rect3D.Empty;
        List<PartItem> on = new();
        int total = 0;
        if (Parts != null && !HideParts) {
            foreach (PartItem p in Parts) {
                if (!p.Checked || p.Model == null) continue;
                drawList.Add(p.Visual != null ? p.Visual : p.Model);
                on.Add(p); total += p.Tris;
            }
            shownParts = on.Count;
            // 투명 재질은 불투명한 것을 모두 그린 뒤에
            foreach (PartItem p in on) if (p.ModelAlpha != null) drawList.Add(p.VisualAlpha != null ? p.VisualAlpha : p.ModelAlpha);
        }
        Redraw();
        if (!refit) return;
        // 화면 맞춤은 삼각형의 95%를 차지하는 큰 부품들로 (애니메이션으로 옮겨지는 작은 표지판 등이 멀리 있어도 버스가 가운데에 오게)
        on.Sort((a, b) => b.Tris.CompareTo(a.Tris));
        int acc = 0;
        foreach (PartItem p in on) { if (acc >= total * 0.95 && !box.IsEmpty) break; box.Union(p.Bounds); acc += p.Tris; }
        if (box.IsEmpty) return;
        target = box.Center;
        double r = Math.Sqrt(box.SizeX * box.SizeX + box.SizeY * box.SizeY + box.SizeZ * box.SizeZ) / 2;
        fitDist = dist = Math.Max(0.5, r / Math.Tan(Fov / 2 * Math.PI / 180) * 1.25);
        yaw = 35; pitch = 18;
        UpdateCam();
    }

    internal void UpdateCam() {
        double y = yaw * Math.PI / 180, p = pitch * Math.PI / 180;
        // 버스 앞(o3d +z → 화면 -z)을 비스듬히 보는 위치에서 시작
        Vector3D dir = new(Math.Sin(y) * Math.Cos(p), Math.Sin(p), -Math.Cos(y) * Math.Cos(p));
        camPos = target + dir * dist;
        look = -dir; look.Normalize();
        Redraw();
    }
    internal double Near => Math.Max(0.01, dist / 500);

    // 개발용(소개 영상): 각도를 정해서 돌린다
    public void Orbit(double yawDeg, double pitchDeg, double zoom) { yaw = yawDeg; pitch = pitchDeg; dist = fitDist * zoom; UpdateCam(); }

    // ---------------------------------------------------------------- 마우스
    static bool Shift(PointerEventArgs e) => (e.KeyModifiers & KeyModifiers.Shift) != 0;
    protected override void OnPointerPressed(PointerPressedEventArgs e) {
        base.OnPointerPressed(e);
        PointerPointProperties pp = e.GetCurrentPoint(this).Properties;
        if (pp.IsLeftButtonPressed && e.ClickCount == 2) { Rebuild(true); return; }
        last = e.GetPosition(this); downAt = last;
        if (pp.IsLeftButtonPressed && MoveCommand != null) {
            object h = HitHandle(last, out Point3D at);
            if (h != null) {
                gOnAxis = h is GizmoAxis;
                if (h is GizmoAxis ga) { h = ga.Target; gAxis = ga.Axis; gAxis.Normalize(); }
                // 시작을 먼저 VM에 알리고 나서 마우스를 잡는다 (이전 드래그의 시작 값으로 되돌아가지 않게)
                gStart = at;
                if (gOnAxis) gS0 = AxisS(last);
                Send(new GizmoDrag { Handle = h, Start = true });
                gHandle = h; Cursor = new Cursor(StandardCursorType.Arrow); e.Handled = true;
                e.Pointer.Capture(this);
                return;
            }
        }
        rot = pp.IsLeftButtonPressed; pan = pp.IsRightButtonPressed || pp.IsMiddleButtonPressed;
        e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) {
        base.OnPointerReleased(e);
        Point pos = e.GetPosition(this);
        if (gHandle != null) {
            Send(new GizmoDrag { Handle = gHandle, Delta = DragDelta(pos), Done = true, Snap = Shift(e) });
            gHandle = null; e.Pointer.Capture(null); e.Handled = true; HoverCursor(pos); return;
        }
        Vector mv = pos - downAt;
        bool click = rot && Math.Sqrt(mv.X * mv.X + mv.Y * mv.Y) < 4;
        rot = pan = false; e.Pointer.Capture(null); e.Handled = true;
        if (click && PickCommand != null) Pick(pos);
    }
    protected override void OnPointerMoved(PointerEventArgs e) {
        base.OnPointerMoved(e);
        Point p = e.GetPosition(this);
        if (gHandle != null) { Send(new GizmoDrag { Handle = gHandle, Delta = DragDelta(p), Snap = Shift(e) }); return; }
        if (!rot && !pan) { HoverCursor(p); return; }
        Vector d = p - last; last = p;
        if (rot) { yaw += d.X * 0.4; pitch = Math.Max(-89, Math.Min(89, pitch + d.Y * 0.4)); }
        else {
            // 화면 평면으로 옮기기 (거리에 비례)
            Vector3D right = Vector3D.CrossProduct(look, new Vector3D(0, 1, 0)); right.Normalize();
            Vector3D upv = Vector3D.CrossProduct(right, look);
            double k = dist * 0.0018;
            target += right * (-d.X * k) + upv * (d.Y * k);
        }
        UpdateCam();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) {
        base.OnPointerWheelChanged(e);
        dist = Math.Max(0.2, Math.Min(fitDist * 6, dist * (e.Delta.Y > 0 ? 0.88 : 1 / 0.88)));
        UpdateCam(); e.Handled = true;
    }

    // 손잡이 위에서는 일반 화살표 커서 (덧그림만 검사: 부품 수십만 개를 매번 검사하지 않게)
    void HoverCursor(Point pt) {
        bool on = false;
        if (MoveCommand != null && Overlay != null) {
            Vector3D ray = Ray(pt);
            double best = double.MaxValue; object h = null;
            Hit.Walk(Overlay, Matrix3D.Identity, camPos, ray, ref best, ref h, true);
            on = h != null;
        }
        Cursor = new Cursor(on ? StandardCursorType.Arrow : StandardCursorType.SizeAll);
    }

    void Send(GizmoDrag g) { if (MoveCommand != null && MoveCommand.CanExecute(g)) MoveCommand.Execute(g); }

    public object HandleAt(Point pt) { return HitHandle(pt, out _); }   // 셀프 테스트용
    // 가장 앞에 맞은 도형이 손잡이면 그것 (가려진 것은 잡지 않는다)
    object HitHandle(Point pt, out Point3D at) {
        Vector3D ray = Ray(pt);
        double best = double.MaxValue; object h = null;
        foreach (Model3D m in drawList) Hit.Walk(m, Matrix3D.Identity, camPos, ray, ref best, ref h, false);
        double partBest = best; h = null;
        if (Overlay != null) Hit.Walk(Overlay, Matrix3D.Identity, camPos, ray, ref best, ref h, false);
        at = camPos + ray * best;
        if (best >= partBest) return null;      // 부품이 더 앞에 있다
        return h;
    }

    // 마우스 아래를 지나는 시선 (카메라 위치에서 나가는 방향, 길이는 look 성분이 1)
    internal Vector3D Ray(Point pt) {
        double w = Math.Max(1, Bounds.Width), h = Math.Max(1, Bounds.Height);
        Vector3D right = Vector3D.CrossProduct(look, up); right.Normalize();
        Vector3D upv = Vector3D.CrossProduct(right, look);
        double t = Math.Tan(Fov / 2 * Math.PI / 180);
        return look + right * ((2 * pt.X / w - 1) * t) + upv * ((1 - 2 * pt.Y / h) * t * h / w);
    }
    // 축 직선(처음 잡은 점 + gAxis·s)에서 시선과 가장 가까운 곳의 s
    double AxisS(Point pt) {
        Vector3D d = Ray(pt), w0 = gStart - camPos;
        double b = Vector3D.DotProduct(gAxis, d), c = Vector3D.DotProduct(d, d), dd = Vector3D.DotProduct(gAxis, w0), e = Vector3D.DotProduct(d, w0);
        double den = c - b * b;   // gAxis는 단위 벡터
        if (Math.Abs(den) < 1e-9) return gS0;
        return (b * e - c * dd) / den;
    }
    // 마우스 아래를 지나는 시선이 처음 잡은 점을 지나고 화면과 평행한 면과 만나는 곳 - 처음 잡은 점 (축 손잡이면 그 축 위로만)
    Vector3D DragDelta(Point pt) {
        if (Bounds.Width < 1 || Bounds.Height < 1) return new Vector3D();
        if (gOnAxis) return gAxis * (AxisS(pt) - gS0);
        Vector3D dir = Ray(pt);
        double den = Vector3D.DotProduct(dir, look);
        if (Math.Abs(den) < 1e-9) return new Vector3D();
        double k = Vector3D.DotProduct(gStart - camPos, look) / den;
        return (camPos + dir * k) - gStart;
    }

    void Pick(Point pt) {
        if (Parts == null) return;
        Vector3D ray = Ray(pt);
        PartItem hit = null; double best = double.MaxValue;
        foreach (PartItem p in Parts) {
            if (!p.Checked || p.Model == null || HideParts) continue;
            double b = best; object h = null;
            Hit.Walk(p.Visual != null ? p.Visual : p.Model, Matrix3D.Identity, camPos, ray, ref b, ref h, false);
            if (p.ModelAlpha != null) Hit.Walk(p.VisualAlpha != null ? p.VisualAlpha : p.ModelAlpha, Matrix3D.Identity, camPos, ray, ref b, ref h, false);
            if (b < best) { best = b; hit = p; }
        }
        if (hit != null && PickCommand.CanExecute(hit)) PickCommand.Execute(hit);
    }

    // 개발용(--shots): 지금 화면을 그림으로 (OpenGL 내용 포함)
    public Avalonia.Media.Imaging.Bitmap Snapshot(int w, int h) => surface.Snapshot(w, h);
    public string RendererInfo => surface.Info;
}

// ---------------------------------------------------------------- 시선 검사 (CPU): 삼각형마다 Möller–Trumbore
static class Hit {
    // best: 지금까지 가장 가까운 거리(시선 길이 단위). handleOnly면 손잡이 도형만 본다
    public static void Walk(Model3D m, Matrix3D parent, Point3D o, Vector3D d, ref double best, ref object handle, bool handleOnly) {
        Matrix3D w = m.Transform != null ? m.Transform.Value * parent : parent;
        if (m is Model3DGroup g) { foreach (Model3D c in g.Children) Walk(c, w, o, d, ref best, ref handle, handleOnly); return; }
        if (m is not GeometryModel3D gm || gm.Geometry == null) return;
        if (handleOnly && gm.Handle == null) return;
        // 시선을 모델 좌표로
        Point3D lo = o; Vector3D ld = d;
        if (!w.IsIdentity) { if (!w.TryInvert(out Matrix3D inv)) return; lo = inv.Transform(o); ld = inv.Transform(d); }
        if (!BoxHit(gm.Geometry.Bounds, lo, ld, best)) return;
        float[] p = gm.Geometry.Pos; int[] ix = gm.Geometry.Idx;
        for (int t = 0; t + 2 < ix.Length; t += 3) {
            int a = ix[t] * 3, b = ix[t + 1] * 3, c = ix[t + 2] * 3;
            double e1x = p[b] - p[a], e1y = p[b + 1] - p[a + 1], e1z = p[b + 2] - p[a + 2];
            double e2x = p[c] - p[a], e2y = p[c + 1] - p[a + 1], e2z = p[c + 2] - p[a + 2];
            double px = ld.Y * e2z - ld.Z * e2y, py = ld.Z * e2x - ld.X * e2z, pz = ld.X * e2y - ld.Y * e2x;
            double det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-12) continue;
            double inv = 1 / det;
            double tx = lo.X - p[a], ty = lo.Y - p[a + 1], tz = lo.Z - p[a + 2];
            double u = (tx * px + ty * py + tz * pz) * inv; if (u < 0 || u > 1) continue;
            double qx = ty * e1z - tz * e1y, qy = tz * e1x - tx * e1z, qz = tx * e1y - ty * e1x;
            double v = (ld.X * qx + ld.Y * qy + ld.Z * qz) * inv; if (v < 0 || u + v > 1) continue;
            double dist = (e2x * qx + e2y * qy + e2z * qz) * inv;
            if (dist > 1e-6 && dist < best) { best = dist; handle = gm.Handle; }
        }
    }
    static bool BoxHit(Rect3D b, Point3D o, Vector3D d, double maxT) {
        if (b.IsEmpty) return false;
        double t0 = 0, t1 = maxT;
        for (int i = 0; i < 3; i++) {
            double oo = i == 0 ? o.X : i == 1 ? o.Y : o.Z, dd = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
            double lo = i == 0 ? b.X : i == 1 ? b.Y : b.Z, hi = lo + (i == 0 ? b.SizeX : i == 1 ? b.SizeY : b.SizeZ);
            if (Math.Abs(dd) < 1e-12) { if (oo < lo || oo > hi) return false; continue; }
            double a = (lo - oo) / dd, c = (hi - oo) / dd;
            if (a > c) (a, c) = (c, a);
            t0 = Math.Max(t0, a); t1 = Math.Min(t1, c);
            if (t0 > t1) return false;
        }
        return true;
    }
}

// ---------------------------------------------------------------- OpenGL 그리기
sealed unsafe class GlSurface : OpenGlControlBase {
    readonly ModelView view;
    GlApi gl;
    int prog, uMvp, uModel, uTex, uUseTex, uAlpha, uColor, uEmis, uHead, uFill;
    int msFbo, msColor, msDepth, msW, msH, samples;
    readonly Dictionary<Mesh3D, int[]> meshes = new();      // vao, vbo(pos), vbo(nor), vbo(uv), ibo, count
    readonly Dictionary<Tex, int> textures = new();
    readonly HashSet<object> used = new();
    public string Info = "";
    bool failed;
    // Snapshot: 다음 그리기에서 화면 대신 이 크기로 그려 읽어 온다
    int snapW, snapH; byte[] snapPx;

    public GlSurface(ModelView v) { view = v; }

    protected override void OnOpenGlInit(GlInterface glIf) {
        try {
            GlVersion ver = glIf.ContextInfo.Version;
            gl = new GlApi(glIf.GetProcAddress, ver.Type == GlProfileType.OpenGLES, ver.Major, ver.Minor);
            Info = gl.Str(GlApi.RENDERER) + " · " + gl.Str(GlApi.VERSION);
            string head = gl.Es ? (gl.Major >= 3 ? "#version 300 es\nprecision highp float;\n" : "#version 100\nprecision highp float;\n")
                                : (gl.Major > 3 || gl.Major == 3 && gl.Minor >= 2 ? "#version 150\n" : "#version 120\n");
            bool modern = gl.Es ? gl.Major >= 3 : (gl.Major > 3 || gl.Major == 3 && gl.Minor >= 2);
            string vin = modern ? "in" : "attribute", vout = modern ? "out" : "varying", fin = modern ? "in" : "varying";
            string vs = head +
                vin + " vec3 aPos; " + vin + " vec3 aNor; " + vin + " vec2 aUv;\n" +
                "uniform mat4 uMvp; uniform mat4 uModel;\n" +
                vout + " vec3 vNor; " + vout + " vec2 vUv;\n" +
                "void main() { vNor = mat3(uModel) * aNor; vUv = aUv; gl_Position = uMvp * vec4(aPos, 1.0); }\n";
            string fs = head +
                fin + " vec3 vNor; " + fin + " vec2 vUv;\n" +
                (modern ? "out vec4 fragColor;\n" : "") +
                "uniform sampler2D uTex; uniform int uUseTex; uniform int uAlpha; uniform vec4 uColor; uniform vec4 uEmis; uniform vec3 uHead; uniform vec3 uFill;\n" +
                "void main() {\n" +
                "  vec3 n = normalize(vNor); if (!gl_FrontFacing) n = -n;\n" +
                "  vec4 base = uUseTex == 1 ? " + (modern ? "texture" : "texture2D") + "(uTex, vUv) : uColor;\n" +
                "  if (uAlpha == 0) base.a = 1.0;\n" +
                // WPF와 같은 조명: 주변광 120 + 시선 방향 빛 150 + (0.4, -1, 0.3) 방향 빛 70 (0~255)
                "  float l = 0.4706 + 0.5882 * max(dot(n, -uHead), 0.0) + 0.2745 * max(dot(n, -uFill), 0.0);\n" +
                "  vec3 c = base.rgb * l + uEmis.rgb * uEmis.a;\n" +
                "  " + (modern ? "fragColor" : "gl_FragColor") + " = vec4(min(c, vec3(1.0)), base.a);\n" +
                "}\n";
            prog = gl.Program(vs, fs, "aPos", "aNor", "aUv");
            uMvp = gl.Uniform(prog, "uMvp"); uModel = gl.Uniform(prog, "uModel"); uTex = gl.Uniform(prog, "uTex"); uUseTex = gl.Uniform(prog, "uUseTex");
            uAlpha = gl.Uniform(prog, "uAlpha"); uColor = gl.Uniform(prog, "uColor"); uEmis = gl.Uniform(prog, "uEmis"); uHead = gl.Uniform(prog, "uHead"); uFill = gl.Uniform(prog, "uFill");
            if (gl.HasMsaa) { int s; gl.GetIntegerv(GlApi.MAX_SAMPLES, &s); samples = Math.Min(4, s); }
        } catch (Exception ex) { failed = true; Info = "GL: " + ex.Message; }
    }

    protected override void OnOpenGlDeinit(GlInterface glIf) {
        if (gl == null) return;
        foreach (int[] m in meshes.Values) FreeMesh(m);
        meshes.Clear();
        foreach (int t in textures.Values) gl.Del(gl.DeleteTextures, t);
        textures.Clear();
        FreeMs();
        if (prog != 0) gl.DeleteProgram(prog);
        prog = 0; gl = null;
    }

    void FreeMesh(int[] m) {
        if (gl.HasVao) gl.Del(gl.DeleteVertexArrays, m[0]);
        for (int i = 1; i <= 4; i++) gl.Del(gl.DeleteBuffers, m[i]);
    }
    void FreeMs() {
        if (gl == null) return;
        gl.Del(gl.DeleteFramebuffers, msFbo); gl.Del(gl.DeleteRenderbuffers, msColor); gl.Del(gl.DeleteRenderbuffers, msDepth);
        msFbo = msColor = msDepth = 0; msW = msH = 0;
    }

    // 계단 현상 없이: 4배 멀티샘플 버퍼에 그린 뒤 화면 버퍼로 옮긴다 (WPF 3D와 비슷하게)
    bool EnsureMs(int w, int h) {
        if (samples <= 1) return false;
        if (msFbo != 0 && msW == w && msH == h) return true;
        FreeMs();
        msFbo = gl.Gen(gl.GenFramebuffers); msColor = gl.Gen(gl.GenRenderbuffers); msDepth = gl.Gen(gl.GenRenderbuffers);
        gl.BindRenderbuffer(GlApi.RENDERBUFFER, msColor); gl.RenderbufferStorageMultisample(GlApi.RENDERBUFFER, samples, GlApi.RGBA8, w, h);
        gl.BindRenderbuffer(GlApi.RENDERBUFFER, msDepth); gl.RenderbufferStorageMultisample(GlApi.RENDERBUFFER, samples, GlApi.DEPTH_COMPONENT24, w, h);
        gl.BindFramebuffer(GlApi.FRAMEBUFFER, msFbo);
        gl.FramebufferRenderbuffer(GlApi.FRAMEBUFFER, GlApi.COLOR_ATTACHMENT0, GlApi.RENDERBUFFER, msColor);
        gl.FramebufferRenderbuffer(GlApi.FRAMEBUFFER, GlApi.DEPTH_ATTACHMENT, GlApi.RENDERBUFFER, msDepth);
        bool ok = gl.CheckFramebufferStatus(GlApi.FRAMEBUFFER) == GlApi.FRAMEBUFFER_COMPLETE;
        msW = w; msH = h;
        if (!ok) { samples = 0; FreeMs(); }
        return ok;
    }

    protected override void OnOpenGlRender(GlInterface glIf, int fb) {
        if (gl == null || failed) return;
        double scale = VisualRoot?.RenderScaling ?? 1;
        int w = Math.Max(1, (int)(Bounds.Width * scale)), h = Math.Max(1, (int)(Bounds.Height * scale));
        bool snap = snapW > 0;
        int outW = w, outH = h;
        if (snap) { outW = snapW; outH = snapH; }
        bool ms = EnsureMs(outW, outH);
        int target = ms ? msFbo : fb;
        int snapFbo = 0, snapRb = 0, snapDepth = 0;
        if (snap && !ms) {
            snapFbo = gl.Gen(gl.GenFramebuffers); snapRb = gl.Gen(gl.GenRenderbuffers); snapDepth = gl.Gen(gl.GenRenderbuffers);
            gl.BindRenderbuffer(GlApi.RENDERBUFFER, snapRb); gl.RenderbufferStorageMultisample(GlApi.RENDERBUFFER, 0, GlApi.RGBA8, outW, outH);
            gl.BindRenderbuffer(GlApi.RENDERBUFFER, snapDepth); gl.RenderbufferStorageMultisample(GlApi.RENDERBUFFER, 0, GlApi.DEPTH_COMPONENT24, outW, outH);
            gl.BindFramebuffer(GlApi.FRAMEBUFFER, snapFbo);
            gl.FramebufferRenderbuffer(GlApi.FRAMEBUFFER, GlApi.COLOR_ATTACHMENT0, GlApi.RENDERBUFFER, snapRb);
            gl.FramebufferRenderbuffer(GlApi.FRAMEBUFFER, GlApi.DEPTH_ATTACHMENT, GlApi.RENDERBUFFER, snapDepth);
            target = snapFbo;
        }
        gl.BindFramebuffer(GlApi.FRAMEBUFFER, target);
        gl.Viewport(0, 0, outW, outH);
        Color bg = Look.Bg;
        gl.ClearColor(bg.R / 255f, bg.G / 255f, bg.B / 255f, 1);
        gl.DepthMask(1);
        gl.Clear(GlApi.COLOR_BUFFER_BIT | GlApi.DEPTH_BUFFER_BIT);
        gl.Enable(GlApi.DEPTH_TEST); gl.DepthFunc(GlApi.LEQUAL);
        gl.FrontFace(GlApi.CCW);
        gl.UseProgram(prog);
        gl.Uniform1i(uTex, 0);

        // 카메라: 가로 화각 40도 (WPF PerspectiveCamera와 같다)
        float[] proj = Perspective(ModelView.Fov, (double)outW / outH, view.Near, 5000);
        float[] viewM = LookAt(view.camPos, view.look, view.up);
        float[] vp = Mul(viewM, proj);
        Vector3D head = view.look;
        Vector3D fill = new(0.4, -1, 0.3); fill.Normalize();
        gl.Uniform3f(uHead, (float)head.X, (float)head.Y, (float)head.Z);
        gl.Uniform3f(uFill, (float)fill.X, (float)fill.Y, (float)fill.Z);

        used.Clear();
        foreach (Model3D m in view.drawList) Draw(m, Matrix3D.Identity, vp);
        if (view.Overlay != null) Draw(view.Overlay, Matrix3D.Identity, vp);
        gl.Disable(GlApi.BLEND); gl.Disable(GlApi.CULL_FACE);
        if (gl.HasVao) gl.BindVertexArray(0);

        // 이번 장면에 없는 버퍼/텍스처는 버린다 (다른 버스를 열면 예전 것을 놓아준다)
        if (meshes.Count > used.Count) {
            List<Mesh3D> drop = new();
            foreach (Mesh3D k in meshes.Keys) if (!used.Contains(k)) drop.Add(k);
            foreach (Mesh3D k in drop) { FreeMesh(meshes[k]); meshes.Remove(k); }
        }
        if (textures.Count > 0) {
            List<Tex> drop = new();
            foreach (Tex k in textures.Keys) if (!used.Contains(k)) drop.Add(k);
            foreach (Tex k in drop) { gl.Del(gl.DeleteTextures, textures[k]); textures.Remove(k); }
        }

        if (ms) {
            int dst = snap ? 0 : fb;
            if (snap) {
                snapFbo = gl.Gen(gl.GenFramebuffers); snapRb = gl.Gen(gl.GenRenderbuffers);
                gl.BindRenderbuffer(GlApi.RENDERBUFFER, snapRb); gl.RenderbufferStorageMultisample(GlApi.RENDERBUFFER, 0, GlApi.RGBA8, outW, outH);
                gl.BindFramebuffer(GlApi.FRAMEBUFFER, snapFbo);
                gl.FramebufferRenderbuffer(GlApi.FRAMEBUFFER, GlApi.COLOR_ATTACHMENT0, GlApi.RENDERBUFFER, snapRb);
                dst = snapFbo;
            }
            gl.BindFramebuffer(GlApi.READ_FRAMEBUFFER, msFbo);
            gl.BindFramebuffer(GlApi.DRAW_FRAMEBUFFER, dst);
            gl.BlitFramebuffer(0, 0, outW, outH, 0, 0, outW, outH, GlApi.COLOR_BUFFER_BIT, GlApi.NEAREST);
            gl.BindFramebuffer(GlApi.FRAMEBUFFER, dst);
        }
        if (snap) {
            byte[] px = new byte[outW * outH * 4];
            gl.PixelStorei(GlApi.PACK_ALIGNMENT, 1);
            fixed (byte* p = px) gl.ReadPixels(0, 0, outW, outH, GlApi.RGBA, GlApi.UNSIGNED_BYTE, p);
            snapPx = px;
            gl.Del(gl.DeleteFramebuffers, snapFbo); gl.Del(gl.DeleteRenderbuffers, snapRb); gl.Del(gl.DeleteRenderbuffers, snapDepth);
            snapW = snapH = 0;
            // 화면 쪽도 다시 그린다
            Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
        }
        gl.BindFramebuffer(GlApi.FRAMEBUFFER, fb);
    }

    void Draw(Model3D m, Matrix3D parent, float[] vp) {
        Matrix3D w = m.Transform != null ? m.Transform.Value * parent : parent;
        if (m is Model3DGroup g) { foreach (Model3D c in g.Children) Draw(c, w, vp); return; }
        if (m is not GeometryModel3D gm || gm.Geometry == null || gm.Geometry.Idx.Length == 0) return;
        int[] buf = Upload(gm.Geometry);
        used.Add(gm.Geometry);
        Material mat = gm.Material ?? new Material();
        float[] model = ToGl(w);
        gl.Mat4(uMvp, Mul(model, vp));
        gl.Mat4(uModel, model);
        if (gm.TwoSided) gl.Disable(GlApi.CULL_FACE); else { gl.Enable(GlApi.CULL_FACE); gl.CullFace(GlApi.BACK); }
        bool blend = mat.Alpha && mat.Texture != null;
        if (blend) { gl.Enable(GlApi.BLEND); gl.BlendFunc(GlApi.SRC_ALPHA, GlApi.ONE_MINUS_SRC_ALPHA); } else gl.Disable(GlApi.BLEND);
        if (mat.Texture != null) {
            gl.ActiveTexture(GlApi.TEXTURE0);
            gl.BindTexture(GlApi.TEXTURE_2D, TexId(mat.Texture));
            used.Add(mat.Texture);
            gl.Uniform1i(uUseTex, 1);
        } else gl.Uniform1i(uUseTex, 0);
        gl.Uniform1i(uAlpha, blend ? 1 : 0);
        Color col = mat.Color, e = mat.Emissive;
        gl.Uniform4f(uColor, col.R / 255f, col.G / 255f, col.B / 255f, 1);
        gl.Uniform4f(uEmis, e.R / 255f, e.G / 255f, e.B / 255f, e.A / 255f);
        Bind(buf);
        gl.DrawElements(GlApi.TRIANGLES, buf[5], GlApi.UNSIGNED_INT, IntPtr.Zero);
    }

    int[] Upload(Mesh3D m) {
        if (meshes.TryGetValue(m, out int[] b)) return b;
        b = new int[6];
        if (gl.HasVao) { b[0] = gl.Gen(gl.GenVertexArrays); gl.BindVertexArray(b[0]); }
        b[1] = Vbo(m.Pos); b[2] = Vbo(m.Nor); b[3] = Vbo(m.Uv);
        b[4] = gl.Gen(gl.GenBuffers);
        gl.BindBuffer(GlApi.ELEMENT_ARRAY_BUFFER, b[4]);
        fixed (int* p = m.Idx) gl.BufferData(GlApi.ELEMENT_ARRAY_BUFFER, (IntPtr)(m.Idx.Length * 4), p, GlApi.STATIC_DRAW);
        b[5] = m.Idx.Length;
        if (gl.HasVao) Attribs(b);
        meshes[m] = b;
        return b;
    }
    int Vbo(float[] data) {
        int id = gl.Gen(gl.GenBuffers);
        gl.BindBuffer(GlApi.ARRAY_BUFFER, id);
        if (data == null || data.Length == 0) data = new float[4];
        fixed (float* p = data) gl.BufferData(GlApi.ARRAY_BUFFER, (IntPtr)(data.Length * 4), p, GlApi.STATIC_DRAW);
        return id;
    }
    void Attribs(int[] b) {
        gl.BindBuffer(GlApi.ARRAY_BUFFER, b[1]); gl.EnableVertexAttribArray(0); gl.VertexAttribPointer(0, 3, GlApi.FLOAT, 0, 12, IntPtr.Zero);
        gl.BindBuffer(GlApi.ARRAY_BUFFER, b[2]); gl.EnableVertexAttribArray(1); gl.VertexAttribPointer(1, 3, GlApi.FLOAT, 0, 12, IntPtr.Zero);
        gl.BindBuffer(GlApi.ARRAY_BUFFER, b[3]); gl.EnableVertexAttribArray(2); gl.VertexAttribPointer(2, 2, GlApi.FLOAT, 0, 8, IntPtr.Zero);
        gl.BindBuffer(GlApi.ELEMENT_ARRAY_BUFFER, b[4]);
    }
    void Bind(int[] b) { if (gl.HasVao) gl.BindVertexArray(b[0]); else Attribs(b); }

    int TexId(Tex t) {
        if (textures.TryGetValue(t, out int id)) return id;
        id = gl.Gen(gl.GenTextures);
        gl.BindTexture(GlApi.TEXTURE_2D, id);
        gl.PixelStorei(GlApi.UNPACK_ALIGNMENT, 1);
        fixed (byte* p = t.Rgba) gl.TexImage2D(GlApi.TEXTURE_2D, 0, GlApi.RGBA, t.W, t.H, 0, GlApi.RGBA, GlApi.UNSIGNED_BYTE, p);
        gl.TexParameteri(GlApi.TEXTURE_2D, GlApi.TEXTURE_WRAP_S, GlApi.REPEAT);
        gl.TexParameteri(GlApi.TEXTURE_2D, GlApi.TEXTURE_WRAP_T, GlApi.REPEAT);
        gl.TexParameteri(GlApi.TEXTURE_2D, GlApi.TEXTURE_MAG_FILTER, GlApi.LINEAR);
        if (gl.GenerateMipmap != null) { gl.GenerateMipmap(GlApi.TEXTURE_2D); gl.TexParameteri(GlApi.TEXTURE_2D, GlApi.TEXTURE_MIN_FILTER, GlApi.LINEAR_MIPMAP_LINEAR); }
        else gl.TexParameteri(GlApi.TEXTURE_2D, GlApi.TEXTURE_MIN_FILTER, GlApi.LINEAR);
        textures[t] = id;
        return id;
    }

    // ---- 행렬 (행 벡터 기준 Matrix3D를 그대로 column-major float 배열로: GL의 열 벡터 곱과 같아진다)
    static float[] ToGl(Matrix3D m) => new[] { (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14, (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
                                                (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34, (float)m.OffsetX, (float)m.OffsetY, (float)m.OffsetZ, (float)m.M44 };
    // 행 벡터 곱 a·b (a 먼저 적용)
    static float[] Mul(float[] a, float[] b) {
        float[] r = new float[16];
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { float s = 0; for (int k = 0; k < 4; k++) s += a[i * 4 + k] * b[k * 4 + j]; r[i * 4 + j] = s; }
        return r;
    }
    static float[] LookAt(Point3D eye, Vector3D f, Vector3D up) {
        f.Normalize();
        Vector3D s = Vector3D.CrossProduct(f, up); s.Normalize();
        Vector3D u = Vector3D.CrossProduct(s, f);
        double tx = -Vector3D.DotProduct(s, (Vector3D)eye), ty = -Vector3D.DotProduct(u, (Vector3D)eye), tz = Vector3D.DotProduct(f, (Vector3D)eye);
        return new[] { (float)s.X, (float)u.X, (float)-f.X, 0, (float)s.Y, (float)u.Y, (float)-f.Y, 0, (float)s.Z, (float)u.Z, (float)-f.Z, 0, (float)tx, (float)ty, (float)tz, 1 };
    }
    // 가로 화각 hfov
    static float[] Perspective(double hfov, double aspect, double n, double f) {
        double sx = 1 / Math.Tan(hfov / 2 * Math.PI / 180), sy = sx * aspect;
        return new[] { (float)sx, 0, 0, 0, 0, (float)sy, 0, 0, 0, 0, (float)((f + n) / (n - f)), -1, 0, 0, (float)(2 * f * n / (n - f)), 0 };
    }

    // 개발용(--shots): 그 크기로 한 번 그려 픽셀을 읽는다. GL이 없으면 빈 그림
    public Avalonia.Media.Imaging.Bitmap Snapshot(int w, int h) {
        snapW = w; snapH = h; snapPx = null;
        RequestNextFrameRendering();
        DateTime end = DateTime.Now.AddSeconds(3);
        while (snapPx == null && DateTime.Now < end) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(10); }
        snapW = snapH = 0;
        if (snapPx == null) return null;
        Avalonia.Media.Imaging.WriteableBitmap wb = new(new PixelSize(w, h), new Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Opaque);
        using (var fbuf = wb.Lock()) {
            for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(snapPx, (h - 1 - y) * w * 4, fbuf.Address + y * fbuf.RowBytes, w * 4);   // GL은 아래 줄부터
        }
        return wb;
    }
}
