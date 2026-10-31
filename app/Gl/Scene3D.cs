using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Obj2x.UI;

// 3D 장면 자료 (WPF Media3D와 같은 이름·좌표계: 오른손, y 위, 행 벡터 행렬). 그리기는 ModelView(OpenGL)가 한다.

// ---------------------------------------------------------------- 수학
public struct Vector3D {
    public double X, Y, Z;
    public Vector3D(double x, double y, double z) { X = x; Y = y; Z = z; }
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public void Normalize() { double l = Length; if (l > 1e-12) { X /= l; Y /= l; Z /= l; } }
    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator -(Vector3D a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3D operator *(Vector3D a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vector3D operator *(double k, Vector3D a) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vector3D operator /(Vector3D a, double k) => new(a.X / k, a.Y / k, a.Z / k);
    public static double DotProduct(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3D CrossProduct(Vector3D a, Vector3D b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public override string ToString() => X + "," + Y + "," + Z;
}

public struct Point3D {
    public double X, Y, Z;
    public Point3D(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static Point3D operator +(Point3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Point3D operator -(Point3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator -(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static explicit operator Vector3D(Point3D p) => new(p.X, p.Y, p.Z);
    public override string ToString() => X + "," + Y + "," + Z;
}

public struct Rect3D {
    public double X, Y, Z, SizeX, SizeY, SizeZ;
    bool notEmpty;
    public Rect3D(double x, double y, double z, double sx, double sy, double sz) { X = x; Y = y; Z = z; SizeX = sx; SizeY = sy; SizeZ = sz; notEmpty = true; }
    public static Rect3D Empty => default;
    public bool IsEmpty => !notEmpty;
    public void Union(Rect3D r) {
        if (r.IsEmpty) return;
        if (IsEmpty) { this = r; return; }
        double x0 = Math.Min(X, r.X), y0 = Math.Min(Y, r.Y), z0 = Math.Min(Z, r.Z);
        double x1 = Math.Max(X + SizeX, r.X + r.SizeX), y1 = Math.Max(Y + SizeY, r.Y + r.SizeY), z1 = Math.Max(Z + SizeZ, r.Z + r.SizeZ);
        this = new Rect3D(x0, y0, z0, x1 - x0, y1 - y0, z1 - z0);
    }
    public void Union(Point3D p) { Union(new Rect3D(p.X, p.Y, p.Z, 0, 0, 0)); }
    public bool Contains(Point3D p) => !IsEmpty && p.X >= X && p.Y >= Y && p.Z >= Z && p.X <= X + SizeX && p.Y <= Y + SizeY && p.Z <= Z + SizeZ;
    public Point3D Center => new(X + SizeX / 2, Y + SizeY / 2, Z + SizeZ / 2);
}

// 행 벡터 기준 4x4 (WPF Matrix3D와 같다: p' = p · M, 이동은 OffsetX/Y/Z)
public struct Matrix3D {
    public double M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34, OffsetX, OffsetY, OffsetZ, M44;
    public Matrix3D(double m11, double m12, double m13, double m14, double m21, double m22, double m23, double m24,
                    double m31, double m32, double m33, double m34, double ox, double oy, double oz, double m44) {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14; M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34; OffsetX = ox; OffsetY = oy; OffsetZ = oz; M44 = m44;
    }
    public static Matrix3D Identity => new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
    public bool IsIdentity => this.Equals(Identity);
    public Point3D Transform(Point3D p) {
        double x = p.X * M11 + p.Y * M21 + p.Z * M31 + OffsetX, y = p.X * M12 + p.Y * M22 + p.Z * M32 + OffsetY, z = p.X * M13 + p.Y * M23 + p.Z * M33 + OffsetZ;
        double w = p.X * M14 + p.Y * M24 + p.Z * M34 + M44;
        return Math.Abs(w - 1) < 1e-12 || Math.Abs(w) < 1e-12 ? new Point3D(x, y, z) : new Point3D(x / w, y / w, z / w);
    }
    public Vector3D Transform(Vector3D v) => new(v.X * M11 + v.Y * M21 + v.Z * M31, v.X * M12 + v.Y * M22 + v.Z * M32, v.X * M13 + v.Y * M23 + v.Z * M33);
    public static Matrix3D operator *(Matrix3D a, Matrix3D b) {
        double[] x = a.ToArray(), y = b.ToArray(), r = new double[16];
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { double s = 0; for (int k = 0; k < 4; k++) s += x[i * 4 + k] * y[k * 4 + j]; r[i * 4 + j] = s; }
        return From(r);
    }
    public double[] ToArray() => new[] { M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34, OffsetX, OffsetY, OffsetZ, M44 };
    public static Matrix3D From(double[] r) => new(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10], r[11], r[12], r[13], r[14], r[15]);
    // 역행렬 (시선 검사를 모델 좌표로 옮길 때)
    public bool TryInvert(out Matrix3D inv) {
        double[] m = ToArray(), o = new double[16];
        o[0] = m[5] * m[10] * m[15] - m[5] * m[11] * m[14] - m[9] * m[6] * m[15] + m[9] * m[7] * m[14] + m[13] * m[6] * m[11] - m[13] * m[7] * m[10];
        o[4] = -m[4] * m[10] * m[15] + m[4] * m[11] * m[14] + m[8] * m[6] * m[15] - m[8] * m[7] * m[14] - m[12] * m[6] * m[11] + m[12] * m[7] * m[10];
        o[8] = m[4] * m[9] * m[15] - m[4] * m[11] * m[13] - m[8] * m[5] * m[15] + m[8] * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
        o[12] = -m[4] * m[9] * m[14] + m[4] * m[10] * m[13] + m[8] * m[5] * m[14] - m[8] * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
        o[1] = -m[1] * m[10] * m[15] + m[1] * m[11] * m[14] + m[9] * m[2] * m[15] - m[9] * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
        o[5] = m[0] * m[10] * m[15] - m[0] * m[11] * m[14] - m[8] * m[2] * m[15] + m[8] * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
        o[9] = -m[0] * m[9] * m[15] + m[0] * m[11] * m[13] + m[8] * m[1] * m[15] - m[8] * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
        o[13] = m[0] * m[9] * m[14] - m[0] * m[10] * m[13] - m[8] * m[1] * m[14] + m[8] * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
        o[2] = m[1] * m[6] * m[15] - m[1] * m[7] * m[14] - m[5] * m[2] * m[15] + m[5] * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
        o[6] = -m[0] * m[6] * m[15] + m[0] * m[7] * m[14] + m[4] * m[2] * m[15] - m[4] * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
        o[10] = m[0] * m[5] * m[15] - m[0] * m[7] * m[13] - m[4] * m[1] * m[15] + m[4] * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
        o[14] = -m[0] * m[5] * m[14] + m[0] * m[6] * m[13] + m[4] * m[1] * m[14] - m[4] * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
        o[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
        o[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
        o[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
        o[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];
        double det = m[0] * o[0] + m[1] * o[4] + m[2] * o[8] + m[3] * o[12];
        if (Math.Abs(det) < 1e-18) { inv = Identity; return false; }
        for (int i = 0; i < 16; i++) o[i] /= det;
        inv = From(o); return true;
    }
}

public class Transform3D { public Matrix3D Value = Matrix3D.Identity; }
public class MatrixTransform3D : Transform3D { public MatrixTransform3D(Matrix3D m) { Value = m; } }

// ---------------------------------------------------------------- 장면
// 텍스처 (RGBA, 위쪽 줄부터). GPU로 올리는 것은 ModelView가 한다
public sealed class Tex {
    public int W, H; public byte[] Rgba;
    public Tex(int w, int h, byte[] rgba) { W = w; H = h; Rgba = rgba; }
}
// 재질: 색 또는 텍스처, 투명([matl_alpha]) 여부, 스스로 빛나는 색(덧그림)
public sealed class Material {
    public Color Color = Colors.White;
    public Tex Texture;
    public bool Alpha;               // 텍스처의 알파를 쓴다 (아니면 불투명하게)
    public Color Emissive;           // 덧그림: 조명과 상관없이 더하는 색 (알파 = 세기)
    public bool Tile = true;
}
// 삼각형 묶음 (위치/노멀/uv는 xyz/xyz/uv 순서 float, 인덱스는 int)
public sealed class Mesh3D {
    public float[] Pos, Nor, Uv; public int[] Idx;
    public Rect3D Bounds = Rect3D.Empty;
    public object GpuTag;            // ModelView가 올린 버퍼 (창마다 따로 둘 수 없으니 GL 문맥 하나 기준)
    public void ComputeBounds() {
        Rect3D b = Rect3D.Empty;
        if (Pos.Length >= 3) {
            double x0 = double.MaxValue, y0 = x0, z0 = x0, x1 = double.MinValue, y1 = x1, z1 = x1;
            for (int i = 0; i + 2 < Pos.Length; i += 3) {
                double x = Pos[i], y = Pos[i + 1], z = Pos[i + 2];
                if (x < x0) x0 = x; if (y < y0) y0 = y; if (z < z0) z0 = z;
                if (x > x1) x1 = x; if (y > y1) y1 = y; if (z > z1) z1 = z;
            }
            b = new Rect3D(x0, y0, z0, x1 - x0, y1 - y0, z1 - z0);
        }
        Bounds = b;
    }
}
public abstract class Model3D {
    public Transform3D Transform;
    public abstract Rect3D Bounds { get; }
    protected Rect3D Xf(Rect3D b) {
        if (Transform == null || b.IsEmpty) return b;
        Rect3D r = Rect3D.Empty;
        for (int i = 0; i < 8; i++) r.Union(Transform.Value.Transform(new Point3D(b.X + ((i & 1) != 0 ? b.SizeX : 0), b.Y + ((i & 2) != 0 ? b.SizeY : 0), b.Z + ((i & 4) != 0 ? b.SizeZ : 0))));
        return r;
    }
}
public sealed class GeometryModel3D : Model3D {
    public Mesh3D Geometry; public Material Material; public bool TwoSided;
    public object Handle;            // 덧그림의 손잡이 (누르고 끌면 ModelView.MoveCommand로)
    public GeometryModel3D(Mesh3D g, Material m) { Geometry = g; Material = m; }
    public override Rect3D Bounds => Xf(Geometry.Bounds);
}
public sealed class Model3DGroup : Model3D {
    public readonly List<Model3D> Children = new();
    public override Rect3D Bounds { get { Rect3D b = Rect3D.Empty; foreach (Model3D c in Children) b.Union(c.Bounds); return Xf(b); } }
}

// 프레임마다 부르는 일 (WPF의 CompositionTarget.Rendering 자리. 구독한 것이 있을 때만 돈다)
public static class CompositionTarget {
    static EventHandler handlers;
    static DispatcherTimer timer;
    public static event EventHandler Rendering {
        add {
            handlers += value;
            if (timer == null) { timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) }; timer.Tick += (s, e) => handlers?.Invoke(null, EventArgs.Empty); }
            timer.Start();
        }
        remove { handlers -= value; if (handlers == null) timer?.Stop(); }
    }
}

// ---------------------------------------------------------------- 텍스처 읽기: bmp/png/jpg는 Skia로, tga/dds는 직접 (OMSI에서 많이 쓰는 형식)
public static class TexLoad {
    public static Tex Load(string path, int max) {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        Tex t;
        if (ext == ".tga") t = Tga(File.ReadAllBytes(path));
        else if (ext == ".dds") t = Dds(File.ReadAllBytes(path));
        else t = Decode(path);
        if (t == null) return null;
        while (t.W > max || t.H > max) t = Half(t);
        return t;
    }

    static unsafe Tex Decode(string path) {
        using FileStream fs = File.OpenRead(path);
        using Bitmap bmp = new Bitmap(fs);
        int w = bmp.PixelSize.Width, h = bmp.PixelSize.Height;
        byte[] px = new byte[w * h * 4];
        using (WriteableBitmap wb = new WriteableBitmap(bmp.PixelSize, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul)) {
            using (ILockedFramebuffer fb = wb.Lock()) {
                bmp.CopyPixels(fb, AlphaFormat.Unpremul);
                for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(fb.Address + y * fb.RowBytes, px, y * w * 4, w * 4);
            }
        }
        return new Tex(w, h, px);
    }

    // 가로세로 절반으로 (2x2 평균)
    static Tex Half(Tex t) {
        int w = Math.Max(1, t.W / 2), h = Math.Max(1, t.H / 2);
        byte[] o = new byte[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int c = 0; c < 4; c++) {
            int x0 = Math.Min(t.W - 1, x * 2), x1 = Math.Min(t.W - 1, x * 2 + 1), y0 = Math.Min(t.H - 1, y * 2), y1 = Math.Min(t.H - 1, y * 2 + 1);
            int s = t.Rgba[(y0 * t.W + x0) * 4 + c] + t.Rgba[(y0 * t.W + x1) * 4 + c] + t.Rgba[(y1 * t.W + x0) * 4 + c] + t.Rgba[(y1 * t.W + x1) * 4 + c];
            o[(y * w + x) * 4 + c] = (byte)(s / 4);
        }
        return new Tex(w, h, o);
    }

    // TGA: 무압축/RLE, 24/32비트 컬러와 8비트 회색 (결과는 RGBA)
    public static Tex Tga(byte[] d) {
        int idLen = d[0], type = d[2], cmLen = d[5] | (d[6] << 8), cmBits = d[7];
        int w = d[12] | (d[13] << 8), h = d[14] | (d[15] << 8), bpp = d[16], desc = d[17];
        if (w <= 0 || h <= 0 || (type != 2 && type != 3 && type != 10 && type != 11)) return null;
        int px = bpp / 8, p = 18 + idLen + cmLen * ((cmBits + 7) / 8);
        if (px < 1 || px > 4) return null;
        byte[] o = new byte[w * h * 4];
        bool rle = type >= 9, top = (desc & 0x20) != 0;
        int n = 0, total = w * h;
        void put(int src) {
            int x = n % w, y = n / w; if (!top) y = h - 1 - y;
            int t = (y * w + x) * 4;
            if (px == 1) { o[t] = o[t + 1] = o[t + 2] = d[src]; o[t + 3] = 255; }
            else if (px == 2) { int v = d[src] | (d[src + 1] << 8); o[t + 2] = (byte)((v & 31) << 3); o[t + 1] = (byte)(((v >> 5) & 31) << 3); o[t] = (byte)(((v >> 10) & 31) << 3); o[t + 3] = 255; }
            else { o[t + 2] = d[src]; o[t + 1] = d[src + 1]; o[t] = d[src + 2]; o[t + 3] = px == 4 ? d[src + 3] : (byte)255; }
            n++;
        }
        while (n < total && p + px <= d.Length) {
            if (!rle) { put(p); p += px; continue; }
            int c = d[p++], cnt = (c & 127) + 1;
            if ((c & 128) != 0) { for (int i = 0; i < cnt && n < total; i++) put(p); p += px; }
            else for (int i = 0; i < cnt && n < total && p + px <= d.Length; i++) { put(p); p += px; }
        }
        return new Tex(w, h, o);
    }

    // DDS: DXT1/DXT3/DXT5와 무압축 32/24비트 (가장 큰 밉맵 한 장, 결과는 RGBA)
    public static Tex Dds(byte[] d) {
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
                    o[t] = (byte)cr[k]; o[t + 1] = (byte)cg[k]; o[t + 2] = (byte)cb[k];
                    o[t + 3] = (byte)(four == "DXT1" ? ca[k] : al[i]);
                }
            }
        } else if (bits == 32 || bits == 24) {
            int px = bits / 8;
            uint rm = BitConverter.ToUInt32(d, 92), am = BitConverter.ToUInt32(d, 104);
            bool rgba = rm == 0xFF;   // R가 가장 낮은 바이트면 RGBA 순서
            for (int i = 0; i < w * h && p + px <= d.Length; i++, p += px) {
                int t = i * 4;
                o[t + 2] = d[p + (rgba ? 2 : 0)]; o[t + 1] = d[p + 1]; o[t] = d[p + (rgba ? 0 : 2)];
                o[t + 3] = px == 4 && am != 0 ? d[p + 3] : (byte)255;
            }
        } else return null;
        return new Tex(w, h, o);
    }
}

// ---------------------------------------------------------------- 모델 보기의 부품 한 줄 (목록의 스위치 = 3D 화면에 보이기)
public class PartItem : Observable {
    bool chk;
    public CfgMesh Mesh;
    public Model3D Model;          // 다른 스레드에서 만든 것 (불투명한 재질)
    public Model3D ModelAlpha;     // 투명 재질([matl_alpha])만. 없으면 null
    public Model3DGroup Visual, VisualAlpha;    // 화면에 넣는 껍데기 (애니메이션 변환을 여기에 건다)
    public Rect3D Bounds = Rect3D.Empty;
    public int Tris;
    public string Name { get; set; }
    public string Sub { get; set; }
    public bool Checked { get { return chk; } set { Set(ref chk, value); } }
}

public static class ModelScene {
    // 부품 하나를 3D 모델로. o3d 좌표(왼손, y 위, z 앞)를 화면(오른손, y 위)으로: z를 뒤집는다.
    // twoSided=false면 OMSI처럼 앞면만 그린다 (노멀이 뒤집힌 면은 보이지 않음)
    public static PartItem Build(ModelCfg cfg, CfgMesh cm, Dictionary<string, Material> cache, List<string> missing, bool twoSided) {
        PartItem it = new PartItem { Mesh = cm, Name = cm.File };
        O3dModel m;
        try { m = O3dModel.Load(cm.Path); }
        catch (Exception ex) { it.Sub = cm.Exists ? ex.Message : L.T("view_part_nofile"); return it; }
        // 투명 재질은 따로 모은다 (화면에서는 불투명한 것을 모두 그린 뒤에 그린다)
        Model3DGroup g = new Model3DGroup(), ga = new Model3DGroup();
        int nm = Math.Max(1, m.Mats.Count);
        for (int mi = 0; mi < nm; mi++) {
            List<float> pos = new(), nor = new(), uv = new(); List<int> idx = new();
            Dictionary<int, int> remap = new();
            for (int t = 0; t < m.TCount; t++) {
                if (m.Mats.Count > 0 && m.T[t * 4 + 3] != mi) continue;
                for (int kk = 0; kk < 3; kk++) {
                    int k = kk == 0 ? 0 : 3 - kk;   // 0, 2, 1: z를 뒤집어 거울상이 됐으니 감기는 방향을 되돌린다
                    int v = m.T[t * 4 + k];
                    if (!remap.TryGetValue(v, out int r)) {
                        r = pos.Count / 3; remap[v] = r;
                        int o = v * 8;
                        pos.Add(m.V[o]); pos.Add(m.V[o + 1]); pos.Add(-m.V[o + 2]);
                        nor.Add(m.V[o + 3]); nor.Add(m.V[o + 4]); nor.Add(-m.V[o + 5]);
                        uv.Add(m.V[o + 6]); uv.Add(m.V[o + 7]);
                    }
                    idx.Add(r);
                }
            }
            if (idx.Count == 0) continue;
            it.Tris += idx.Count / 3;
            Mesh3D geo = new Mesh3D { Pos = pos.ToArray(), Nor = nor.ToArray(), Uv = uv.ToArray(), Idx = idx.ToArray() };
            geo.ComputeBounds();
            O3dMaterial omt = mi < m.Mats.Count ? m.Mats[mi] : new O3dMaterial();
            Material mat = MaterialFor(cfg, cm, omt, cache, missing);
            GeometryModel3D gm = new GeometryModel3D(geo, mat) { TwoSided = twoSided };
            (omt.Tex.Length > 0 && cm.UsesAlpha(omt.Tex) ? ga : g).Children.Add(gm);
        }
        it.Model = g; it.Bounds = g.Bounds;
        if (ga.Children.Count > 0) { it.ModelAlpha = ga; it.Bounds.Union(ga.Bounds); }
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
        lock (cache) if (cache.TryGetValue(key, out Material hit)) return hit;
        Material mat = new Material { Alpha = alpha };
        if (f != null) {
            try { mat.Texture = TexLoad.Load(f, 1024); } catch (Exception) { }   // [matl_alpha]가 없으면 알파를 버린다 (셰이더에서)
        } else if (mt.Tex.Length > 0) { lock (missing) if (!missing.Contains(mt.Tex)) missing.Add(mt.Tex); }
        if (mat.Texture == null) mat.Color = f != null || mt.Tex.Length == 0 ? dc : Color.FromRgb(200, 200, 205);
        lock (cache) cache[key] = mat;
        return mat;
    }
    static byte Clamp(float v) { return (byte)Math.Max(0, Math.Min(255, v * 255 * 1.25)); }
}

// 덧그림의 손잡이(사람 길 점, 좌석)를 끌 때 VM에 넘기는 값. Delta는 처음 누른 곳에서 움직인 양(화면 좌표계), Snap은 Shift를 누른 채
public class GizmoDrag { public object Handle; public Vector3D Delta; public bool Start, Done, Snap; }
// 축 화살표 손잡이: 끌면 Target을 Axis(화면 좌표계 단위 벡터) 방향으로만 옮긴다
public class GizmoAxis { public object Target; public Vector3D Axis; }

// ---------------------------------------------------------------- [newanim] 계산 (OMSI 좌표: x 오른쪽, y 앞, z 위)
// 블록마다: 피벗(origin_trans 또는 메시 기준점)을 중심으로, origin_rot로 돌린 좌표계의 x축을 회전축으로
// anim_rot = 값 × factor 도 회전, anim_trans = 값 × factor 만큼 축 방향 이동. 블록은 적힌 순서대로 겹쳐 적용한다.
// (행렬은 열 벡터 기준 4x4, m[r*4+c])
public static class AnimMath {
    public static bool ZFirst = true, Inner = true, Flip;

    public static double[] I() { return new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }; }
    public static double[] Mul(double[] a, double[] b) {
        double[] r = new double[16];
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) { double s = 0; for (int k = 0; k < 4; k++) s += a[i * 4 + k] * b[k * 4 + j]; r[i * 4 + j] = s; }
        return r;
    }
    static double[] T(double x, double y, double z) { double[] m = I(); m[3] = x; m[7] = y; m[11] = z; return m; }
    static double[] Rot(char ax, double deg) {
        double c = Math.Cos(deg * Math.PI / 180), s = Math.Sin(deg * Math.PI / 180); double[] m = I();
        if (ax == 'x') { m[5] = c; m[6] = -s; m[9] = s; m[10] = c; }
        else if (ax == 'y') { m[0] = c; m[2] = s; m[8] = -s; m[10] = c; }
        else { m[0] = c; m[1] = -s; m[4] = s; m[5] = c; }
        return m;
    }
    static double[] RotAxis(double[] a, double rad) {
        double c = Math.Cos(rad), s = Math.Sin(rad), t = 1 - c, x = a[0], y = a[1], z = a[2]; double[] m = I();
        m[0] = t * x * x + c; m[1] = t * x * y - s * z; m[2] = t * x * z + s * y;
        m[4] = t * x * y + s * z; m[5] = t * y * y + c; m[6] = t * y * z - s * x;
        m[8] = t * x * z - s * y; m[9] = t * y * z + s * x; m[10] = t * z * z + c;
        return m;
    }

    public static double[] Pivot(AnimBlock b, CfgMesh m) { return b.FromMesh && !b.HasTrans && m.MeshOrigin != null ? m.MeshOrigin : b.Trans; }
    public static double[] Axis(AnimBlock b, CfgMesh m) {
        double[] a;
        if (b.FromMesh && m.MeshAxis != null) a = (double[])m.MeshAxis.Clone();
        else {
            double[] R = ZFirst ? Mul(Mul(Rot('x', b.Rot[0]), Rot('y', b.Rot[1])), Rot('z', b.Rot[2])) : Mul(Mul(Rot('z', b.Rot[2]), Rot('y', b.Rot[1])), Rot('x', b.Rot[0]));
            a = new double[] { R[0], R[4], R[8] };
        }
        double l = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]); if (l < 1e-9) return new double[] { 1, 0, 0 };
        return new double[] { a[0] / l, a[1] / l, a[2] / l };
    }

    public static double[] Block(AnimBlock b, CfgMesh m, Func<string, double> val) {
        double[] o = Pivot(b, m), ax = Axis(b, m), M = I();
        double ang = 0, d = 0;
        foreach (AnimEntry e in b.Entries) { if (e.Trans) d += val(e.Var) * e.Factor; else ang += val(e.Var) * e.Factor; }
        if (ang != 0) M = RotAxis(ax, ang * Math.PI / 180 * (Flip ? -1 : 1));
        if (d != 0) M = Mul(T(ax[0] * d, ax[1] * d, ax[2] * d), M);
        return Mul(Mul(T(o[0], o[1], o[2]), M), T(-o[0], -o[1], -o[2]));
    }

    // only < 0: 모든 블록, 아니면 그 번호 앞(Inner) 또는 뒤의 블록만 (피벗 표시용)
    public static double[] Chain(CfgMesh m, Func<string, double> val, int upTo) {
        double[] M = I();
        int n = upTo < 0 ? m.Anims.Count : upTo;
        for (int i = 0; i < m.Anims.Count; i++) {
            if (upTo >= 0 && (Inner ? i >= n : i <= n)) continue;
            double[] b = Block(m.Anims[i], m, val);
            M = Inner ? Mul(b, M) : Mul(M, b);
        }
        return M;
    }

    // OMSI 좌표 (x, y, z) → 화면 좌표 (x, z, -y)
    public static Point3D P(double[] p) { return new Point3D(p[0], p[2], -p[1]); }
    public static Point3D Apply(double[] M, double[] p) {
        return P(new double[] { M[0] * p[0] + M[1] * p[1] + M[2] * p[2] + M[3], M[4] * p[0] + M[5] * p[1] + M[6] * p[2] + M[7], M[8] * p[0] + M[9] * p[1] + M[10] * p[2] + M[11] });
    }
    public static double[] Dir(double[] M, double[] v) {
        return new double[] { M[0] * v[0] + M[1] * v[1] + M[2] * v[2], M[4] * v[0] + M[5] * v[1] + M[6] * v[2], M[8] * v[0] + M[9] * v[1] + M[10] * v[2] };
    }

    // OMSI 좌표의 변환 M을 화면 좌표로: W = C·M·C⁻¹ (C: (x,y,z)→(x,z,-y)), Matrix3D는 행 벡터라 전치해서 넣는다
    public static Matrix3D ToWpf(double[] M) {
        double[] C = { 1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1 }, Ci = { 1, 0, 0, 0, 0, 0, -1, 0, 0, 1, 0, 0, 0, 0, 0, 1 };
        double[] W = Mul(Mul(C, M), Ci);
        return new Matrix3D(W[0], W[4], W[8], W[12], W[1], W[5], W[9], W[13], W[2], W[6], W[10], W[14], W[3], W[7], W[11], W[15]);
    }
}

// ---------------------------------------------------------------- 3D 위에 그리는 표시: 사람 길(점, 연결), 좌석, 피벗과 회전축
public class Overlay3D {
    readonly Dictionary<Color, Material> mats = new();
    public readonly Model3DGroup Group = new();

    Material Mat(Color c) {
        if (mats.TryGetValue(c, out Material m)) return m;
        m = new Material { Color = c, Emissive = Color.FromArgb(150, c.R, c.G, c.B) };
        mats[c] = m; return m;
    }

    // 화면 좌표의 점 a→b를 잇는 굵기 w의 막대 (네모 기둥). handle: 누르고 끌 수 있는 손잡이 표시
    public void Rod(Point3D a, Point3D b, double w, Color c, object handle = null) {
        Vector3D d = b - a; if (d.Length < 1e-6) return;
        d.Normalize();
        Vector3D u = Vector3D.CrossProduct(d, Math.Abs(d.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0)); u.Normalize();
        Vector3D v = Vector3D.CrossProduct(d, u); u *= w / 2; v *= w / 2;
        Point3D[] p = { a - u - v, a + u - v, a + u + v, a - u + v, b - u - v, b + u - v, b + u + v, b - u + v };
        Prism(p, c, handle);
    }
    public void Box(Point3D c0, double sx, double sy, double sz, Color c, object handle = null) {
        double x = sx / 2, y = sy / 2, z = sz / 2;
        Point3D[] p = { new(c0.X - x, c0.Y - y, c0.Z - z), new(c0.X + x, c0.Y - y, c0.Z - z), new(c0.X + x, c0.Y + y, c0.Z - z), new(c0.X - x, c0.Y + y, c0.Z - z),
                        new(c0.X - x, c0.Y - y, c0.Z + z), new(c0.X + x, c0.Y - y, c0.Z + z), new(c0.X + x, c0.Y + y, c0.Z + z), new(c0.X - x, c0.Y + y, c0.Z + z) };
        Prism(p, c, handle);
    }
    // 여덟 꼭짓점 상자 → 면마다 꼭짓점을 따로 둬서 면 노멀로 (각진 모양)
    void Prism(Point3D[] p, Color c, object handle) {
        int[] f = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
        float[] pos = new float[f.Length * 3], nor = new float[f.Length * 3], uv = new float[f.Length * 2]; int[] idx = new int[f.Length];
        for (int t = 0; t < f.Length; t += 3) {
            Point3D a = p[f[t]], b = p[f[t + 1]], cc = p[f[t + 2]];
            Vector3D n = Vector3D.CrossProduct(b - a, cc - a); n.Normalize();
            for (int k = 0; k < 3; k++) {
                Point3D q = p[f[t + k]]; int i = t + k;
                pos[i * 3] = (float)q.X; pos[i * 3 + 1] = (float)q.Y; pos[i * 3 + 2] = (float)q.Z;
                nor[i * 3] = (float)n.X; nor[i * 3 + 1] = (float)n.Y; nor[i * 3 + 2] = (float)n.Z;
                idx[i] = i;
            }
        }
        Mesh3D g = new Mesh3D { Pos = pos, Nor = nor, Uv = uv, Idx = idx };
        g.ComputeBounds();
        Group.Children.Add(new GeometryModel3D(g, Mat(c)) { TwoSided = true, Handle = handle });
    }
}
