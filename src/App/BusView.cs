using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Obj2x.UI {

// ---------------------------------------------------------------- [newanim] 계산 (OMSI 좌표: x 오른쪽, y 앞, z 위)
// 블록마다: 피벗(origin_trans 또는 메시 기준점)을 중심으로, origin_rot로 돌린 좌표계의 x축을 회전축으로
// anim_rot = 값 × factor 도 회전, anim_trans = 값 × factor 만큼 축 방향 이동. 블록은 적힌 순서대로 겹쳐 적용한다.
// (Claude로 만들었던 OMSI 애니메이션 시뮬레이터와 같은 계산. 행렬은 열 벡터 기준 4x4, m[r*4+c])
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

    // OMSI 좌표 (x, y, z) → 화면(WPF) 좌표 (x, z, -y)
    public static Point3D P(double[] p) { return new Point3D(p[0], p[2], -p[1]); }
    public static Point3D Apply(double[] M, double[] p) {
        return P(new double[] { M[0] * p[0] + M[1] * p[1] + M[2] * p[2] + M[3], M[4] * p[0] + M[5] * p[1] + M[6] * p[2] + M[7], M[8] * p[0] + M[9] * p[1] + M[10] * p[2] + M[11] });
    }
    public static double[] Dir(double[] M, double[] v) {
        return new double[] { M[0] * v[0] + M[1] * v[1] + M[2] * v[2], M[4] * v[0] + M[5] * v[1] + M[6] * v[2], M[8] * v[0] + M[9] * v[1] + M[10] * v[2] };
    }

    // OMSI 좌표의 변환 M을 화면 좌표로: W = C·M·C⁻¹ (C: (x,y,z)→(x,z,-y)), WPF Matrix3D는 행 벡터라 전치해서 넣는다
    public static Matrix3D ToWpf(double[] M) {
        double[] C = { 1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1 }, Ci = { 1, 0, 0, 0, 0, 0, -1, 0, 0, 1, 0, 0, 0, 0, 0, 1 };
        double[] W = Mul(Mul(C, M), Ci);
        return new Matrix3D(W[0], W[4], W[8], W[12], W[1], W[5], W[9], W[13], W[2], W[6], W[10], W[14], W[3], W[7], W[11], W[15]);
    }
}

// ---------------------------------------------------------------- 3D 위에 그리는 표시: 사람 길(점, 연결), 좌석, 피벗과 회전축
public class Overlay3D {
    readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
    public readonly Model3DGroup Group = new Model3DGroup();

    Material Mat(Color c) {
        Material m;
        if (mats.TryGetValue(c, out m)) return m;
        MaterialGroup g = new MaterialGroup();
        g.Children.Add(new DiffuseMaterial(new SolidColorBrush(c)));
        g.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(150, c.R, c.G, c.B))));
        g.Freeze(); mats[c] = g; return g;
    }

    // 화면 좌표의 점 a→b를 잇는 굵기 w의 막대 (네모 기둥)
    public void Rod(Point3D a, Point3D b, double w, Color c) {
        Vector3D d = b - a; if (d.Length < 1e-6) return;
        d.Normalize();
        Vector3D u = Vector3D.CrossProduct(d, Math.Abs(d.Y) < 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0)); u.Normalize();
        Vector3D v = Vector3D.CrossProduct(d, u); u *= w / 2; v *= w / 2;
        Point3D[] p = { a - u - v, a + u - v, a + u + v, a - u + v, b - u - v, b + u - v, b + u + v, b - u + v };
        Prism(p, c);
    }
    public void Box(Point3D c0, double sx, double sy, double sz, Color c) {
        double x = sx / 2, y = sy / 2, z = sz / 2;
        Point3D[] p = { new Point3D(c0.X - x, c0.Y - y, c0.Z - z), new Point3D(c0.X + x, c0.Y - y, c0.Z - z), new Point3D(c0.X + x, c0.Y + y, c0.Z - z), new Point3D(c0.X - x, c0.Y + y, c0.Z - z),
                        new Point3D(c0.X - x, c0.Y - y, c0.Z + z), new Point3D(c0.X + x, c0.Y - y, c0.Z + z), new Point3D(c0.X + x, c0.Y + y, c0.Z + z), new Point3D(c0.X - x, c0.Y + y, c0.Z + z) };
        Prism(p, c);
    }
    void Prism(Point3D[] p, Color c) {
        MeshGeometry3D g = new MeshGeometry3D();
        foreach (Point3D q in p) g.Positions.Add(q);
        int[] f = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
        foreach (int i in f) g.TriangleIndices.Add(i);
        g.Freeze();
        Material m = Mat(c);
        GeometryModel3D gm = new GeometryModel3D(g, m); gm.BackMaterial = m; gm.Freeze();
        Group.Children.Add(gm);
    }
}
}
