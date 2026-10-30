using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace Obj2x.UI {

// BusDepot.exe --selftest : 설정/문구/변환기/엔진(중지 포함)/XAML 화면/ViewModel을 자동으로 점검한다.
public static class SelfTest {
    static int fail = 0;
    static StringBuilder sb = new StringBuilder();
    static List<T> FindAll<T>(DependencyObject root) where T : DependencyObject {
        List<T> r = new List<T>();
        if (root == null) return r;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++) {
            DependencyObject c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (c is T) r.Add((T)c);
            r.AddRange(FindAll<T>(c));
        }
        return r;
    }

    static string InlineText(System.Windows.Documents.InlineCollection ic) {
        StringBuilder r = new StringBuilder();
        foreach (System.Windows.Documents.Inline i in ic) {
            System.Windows.Documents.Run run = i as System.Windows.Documents.Run;
            System.Windows.Documents.Span span = i as System.Windows.Documents.Span;
            if (run != null) r.Append(run.Text); else if (span != null) r.Append(InlineText(span.Inlines));
        }
        return r.ToString();
    }

    static int Count(string s, string sub) { int n = 0, i = 0; while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; } return n; }

    static void T(string name, bool ok, string detail) {
        string line = ok ? "PASS " + name : "FAIL " + name + ": " + detail;
        if (!ok) fail++;
        sb.AppendLine(line); Console.WriteLine(line);
    }

    public static int Run(string here, string outFile) {
        Motion.Off = true;
        string root = Path.Combine(Path.GetTempPath(), "BusDepot_selftest_cs");
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (Exception) { }
        Directory.CreateDirectory(root);

        // 1. 문구표
        string par = L.Parity();
        T("문구표: 모든 언어 키 일치", par.Length == 0, par);
        T("문구표: 키 개수", L.Count > 150, L.Count.ToString());
        L.Lang = "ko"; string k1 = L.T("m_fast_done", 3, 5, "1.5");
        L.Lang = "en"; string k2 = L.T("m_fast_done", 3, 5, "1.5");
        T("문구표: 서식 채우기", k1.Contains("3") && k2.Contains("3") && k1 != k2, k1 + " | " + k2);
        string ph = L.PlaceholderParity();
        T("문구표: 모든 언어의 {0} 자리 표시가 같음", ph.Length == 0, ph);
        L.Lang = "de"; string g1 = L.T("m_fast_done", 3, 5, "1.5");
        T("문구표: 독일어", g1.StartsWith("Fertig") && L.T("nav_settings") == "Einstellungen" && L.T("w_nomtl") == "mtl-Datei nicht gefunden", g1);
        L.Lang = "zh"; string z1 = L.T("nav_settings"); L.Lang = "ja"; string j1 = L.T("nav_settings");
        T("문구표: 중국어/일본어", z1 == "设置" && j1 == "設定" && L.BuiltinCount >= 5, z1 + " " + j1 + " builtin=" + L.BuiltinCount);
        L.Lang = "ko";
        // 사용자 언어 파일: lang 폴더에 새 언어와 기존 언어 덮어쓰기를 넣고 다시 읽는다
        string ud = L.UserDir; bool hadDir = Directory.Exists(ud);
        Directory.CreateDirectory(ud);
        string fx = Path.Combine(ud, "xx.json"), fd = Path.Combine(ud, "de.json"), fbad = Path.Combine(ud, "yy.json");
        bool hadDe = File.Exists(fd);
        string deBackup = hadDe ? File.ReadAllText(fd, Encoding.UTF8) : null;
        try {
            File.WriteAllText(fx, "{ \"_name\": \"Test-Sprache\", \"nav_settings\": \"Réglages\", \"m_fast_done\": \"ok {0}/{1} {2}\" }", new UTF8Encoding(false));
            if (!hadDe) File.WriteAllText(fd, "{ \"nav_info\": \"Über\" }", new UTF8Encoding(false));
            File.WriteAllText(fbad, "{ \"nav_info\": broken }", new UTF8Encoding(false));
            L.Load();
            bool hasXx = false; foreach (string[] l in L.Languages) if (l[0] == "xx" && l[1] == "Test-Sprache") hasXx = true;
            L.Lang = "xx";
            T("언어 파일: 새 언어 추가 + 빠진 문구는 영어로", hasXx && L.T("nav_settings") == "Réglages" && L.T("nav_wizard") == "Wizard", L.T("nav_settings") + " " + L.T("nav_wizard"));
            L.Lang = "de";
            T("언어 파일: 기존 언어 일부 덮어쓰기", hadDe || (L.T("nav_info") == "Über" && L.T("nav_settings") == "Einstellungen"), L.T("nav_info"));
            T("언어 파일: 잘못된 파일은 건너뛰고 이유 기록", L.LoadErrors.Count == 1 && L.LoadErrors[0].StartsWith("yy.json"), string.Join(" / ", L.LoadErrors.ToArray()));
            L.WriteTemplate();
            T("언어 파일: 번역용 틀 만들기", File.Exists(Path.Combine(ud, "_template.json")) && Json.ParseObject(File.ReadAllText(Path.Combine(ud, "_template.json"), Encoding.UTF8)).Count == L.Count + 2, "");
        } finally {
            try { File.Delete(fx); File.Delete(fbad); File.Delete(Path.Combine(ud, "_template.json")); if (!hadDe) File.Delete(fd); if (!hadDir) Directory.Delete(ud); } catch (Exception) { }
            L.Load(); L.Lang = "ko";
        }
        T("문구표: {0} 안 남음", !L.T("lbl_cores", 8).Contains("{0}") && !L.T("m_retry", "a", "b").Contains("{"), "");
        L.Lang = "ko";

        // 2. 설정
        string ini = Path.Combine(root, "t.ini");
        Settings s = Settings.Load(ini);
        T("설정: 기본 par 1~20", s.Par >= 1 && s.Par <= 20, s.Par.ToString());
        s.Blender = @"D:\B\blender.exe"; s.Inp = @"C:\in\"; s.Theme = "dark"; s.Accent = "rose"; s.Lang = "en";
        s.Copt["zup"] = 1; s.ExOpt = "A=1\r\nB=2"; s.Agreed = true; s.AgreedVer = "2.0"; s.Fast = true;
        for (int i = 0; i < 8; i++) s.AddRecent(@"C:\r" + i);
        s.AddRecent(@"C:\R5");
        s.Save();
        Settings s2 = Settings.Load(ini);
        T("설정: 왕복 저장", s2.Blender == s.Blender && s2.Theme == "dark" && s2.Accent == "rose" && s2.Lang == "en" && s2.Copt["zup"] == 1 && s2.Fast && s2.Inp == @"C:\in", s2.Blender + "|" + s2.Inp);
        T("설정: exopt 줄바꿈 복원", s2.ExOpt == "A=1\r\nB=2", s2.ExOpt);
        T("설정: 최근 폴더 5개/중복 제거", s2.Recent.Count == 5 && s2.Recent[0] == @"C:\R5" && s2.Recent.FindAll(delegate(string x) { return x.ToLower() == @"c:\r5"; }).Count == 1, string.Join("|", s2.Recent.ToArray()));
        Settings d = new Settings();
        d.Agreed = false; T("면책: 처음 실행이면 필요", d.NeedDisclaimer(), "");
        d.Agreed = true; d.AgreedVer = ""; T("면책: 버전 기록 없으면 필요", d.NeedDisclaimer(), "");
        d.AgreedVer = "1.9"; T("면책: 1.9에서 올라오면 필요", d.NeedDisclaimer(), "");
        d.AgreedVer = "1.10"; T("면책: 1.10도 2.0 미만", d.NeedDisclaimer(), "");
        d.AgreedVer = "2.0"; T("면책: 2.0이면 불필요", !d.NeedDisclaimer(), "");
        d.AgreedVer = "2.1"; T("면책: 2.1이면 불필요", !d.NeedDisclaimer(), "");
        T("업데이트: 버전 비교", Updater.IsNewer("2.1", "2.0") && !Updater.IsNewer("2.0", "2.0") && Updater.IsNewer("2.10", "2.9") && Updater.IsNewer("2.7.1", "2.7") && !Updater.IsNewer("2.7", "2.7.1"), "");

        // 3. FastO3D
        string od = Path.Combine(root, "obj"); Directory.CreateDirectory(Path.Combine(od, "sub"));
        string objText = "mtllib a.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nvn 0 0 1\nusemtl m1\nf 1/1/1 2/2/1 3/3/1 4/3/1\n";
        File.WriteAllText(Path.Combine(od, "a.obj"), objText, Encoding.ASCII);
        File.WriteAllText(Path.Combine(od, "a.mtl"), "newmtl m1\nKd 0.5 0.5 0.5\nmap_Kd tex.bmp\n", Encoding.ASCII);
        File.WriteAllText(Path.Combine(od, "sub", "b.obj"), objText.Replace("mtllib a.mtl\n", ""), Encoding.ASCII);
        int nv, nt, nm; string wn;
        FastO3D.ConvertEx(Path.Combine(od, "a.obj"), Path.Combine(od, "a.o3d"), out nv, out nt, out nm, out wn);
        byte[] ob = File.ReadAllBytes(Path.Combine(od, "a.o3d"));
        T("FastO3D: 헤더 84 19 01 17", ob.Length > 8 && ob[0] == 0x84 && ob[1] == 0x19 && ob[2] == 0x01 && ob[3] == 0x17, ob.Length.ToString());
        T("FastO3D: 사각형 -> 삼각형 2개", nt == 2 && nv == 4 && nm == 1, nv + "/" + nt + "/" + nm);
        T("목록: 하위 폴더 제외", Engine.FindObjs(od, false).Count == 1, "");
        T("목록: 하위 폴더 포함", Engine.FindObjs(od, true).Count == 2 && Engine.FindObjs(od, true)[1].Rel.Replace('\\', '/') == "sub/b", "");

        // 4. 엔진: FastO3D 전체 변환 + 중간에 중지
        string big = Path.Combine(root, "many"); Directory.CreateDirectory(big);
        for (int i = 0; i < 60; i++) File.WriteAllText(Path.Combine(big, "m" + i.ToString("00") + ".obj"), objText.Replace("mtllib a.mtl\n", ""), Encoding.ASCII);
        RunOptions ro = new RunOptions(); ro.Mode = "fast"; ro.In = big; ro.Out = big;
        Engine e1 = new Engine(); int rows1 = 0; bool fin1 = false, stopped1 = true;
        e1.OnRow = delegate(ResRow r) { rows1++; }; e1.OnFinish = delegate(bool st) { stopped1 = st; fin1 = true; };
        e1.Start(ro, Engine.FindObjs(big, false));
        for (int i = 0; i < 300 && !fin1; i++) Thread.Sleep(20);
        T("엔진(빠른 변환): 끝까지 변환", fin1 && !stopped1 && rows1 == 60 && File.Exists(Path.Combine(big, "m59.o3d")), rows1 + " fin=" + fin1);
        string big2 = Path.Combine(root, "many2"); Directory.CreateDirectory(big2);
        for (int i = 0; i < 80; i++) File.WriteAllText(Path.Combine(big2, "m" + i.ToString("00") + ".obj"), objText.Replace("mtllib a.mtl\n", ""), Encoding.ASCII);
        ro = new RunOptions(); ro.Mode = "fast"; ro.In = big2; ro.Out = big2;
        Engine e2 = new Engine(); int rows2 = 0; bool fin2 = false, stopped2 = false;
        e2.OnRow = delegate(ResRow r) { rows2++; if (rows2 == 5) e2.Stop(); };
        e2.OnFinish = delegate(bool st) { stopped2 = st; fin2 = true; };
        e2.Start(ro, Engine.FindObjs(big2, false));
        for (int i = 0; i < 300 && !fin2; i++) Thread.Sleep(20);
        T("엔진(빠른 변환): 중간에 중지", fin2 && stopped2 && rows2 == 5 && !File.Exists(Path.Combine(big2, "m10.o3d")), rows2 + " fin=" + fin2 + " stopped=" + stopped2);
        T("엔진: 중지 뒤 다시 시작 가능", !e2.Running, "");
        // 실패한 것만 다시: 빠른 변환에서 깨진 obj는 재시도 목록에 들어가고, 고친 뒤 다시 하면 비워진다
        string bad3 = Path.Combine(root, "bad3"); Directory.CreateDirectory(bad3);
        File.WriteAllText(Path.Combine(bad3, "ok.obj"), objText.Replace("mtllib a.mtl\n", ""), Encoding.ASCII);
        File.WriteAllText(Path.Combine(bad3, "broken.obj"), "v 0 0 0\n", Encoding.ASCII);
        ro = new RunOptions(); ro.Mode = "fast"; ro.In = bad3; ro.Out = bad3;
        Engine e3 = new Engine(); bool fin3 = false;
        e3.OnFinish = delegate(bool st) { fin3 = true; };
        e3.Start(ro, Engine.FindObjs(bad3, false));
        for (int i = 0; i < 300 && !fin3; i++) Thread.Sleep(20);
        bool listed = e3.FailCount == 1 && e3.FailedObjs[0].Name == "broken" && e3.LastMode == "fast";
        File.WriteAllText(Path.Combine(bad3, "broken.obj"), objText.Replace("mtllib a.mtl\n", ""), Encoding.ASCII);
        fin3 = false; int rows3 = 0; e3.OnRow = delegate(ResRow r) { rows3++; };
        e3.StartRetry(ro);
        for (int i = 0; i < 300 && !fin3; i++) Thread.Sleep(20);
        T("엔진: 실패한 것만 다시 (빠른 변환)", listed && fin3 && rows3 == 1 && e3.FailCount == 0 && File.Exists(Path.Combine(bad3, "broken.o3d")), listed + " rows=" + rows3 + " fail=" + e3.FailCount);

        // 4-1. 변환 전 텍스처 검사
        List<ObjItem> texObjs = Engine.FindObjs(od, false);
        List<string> texMiss = Omsi.MissingTextures(texObjs);
        File.WriteAllBytes(Path.Combine(od, "tex.bmp"), new byte[] { 0x42, 0x4D });
        T("텍스처 검사: mtl에 있는데 없는 텍스처", texMiss.Count == 1 && texMiss[0] == "a: tex.bmp" && Omsi.MissingTextures(texObjs).Count == 0, string.Join(", ", texMiss.ToArray()));

        // 4-2. OMSI 오브젝트(.sco): o3d의 텍스처 이름 읽기, 알파 판별, 모델/텍스처 복사
        string md = Path.Combine(root, "models"); Directory.CreateDirectory(md);
        File.WriteAllText(Path.Combine(md, "house.mtl"), "newmtl m1\nmap_Kd wall.bmp\nnewmtl m2\nmap_Kd glass.tga\n", Encoding.ASCII);
        File.WriteAllText(Path.Combine(md, "house.obj"), objText.Replace("a.mtl", "house.mtl") + "usemtl m2\nf 1/1/1 3/3/1 4/3/1\n", Encoding.ASCII);
        FastO3D.ConvertEx(Path.Combine(md, "house.obj"), Path.Combine(md, "house.o3d"), out nv, out nt, out nm, out wn);
        FastO3D.ConvertEx(Path.Combine(od, "a.obj"), Path.Combine(md, "fence.o3d"), out nv, out nt, out nm, out wn);
        File.Delete(Path.Combine(md, "house.obj"));
        File.WriteAllBytes(Path.Combine(md, "wall.bmp"), new byte[] { 0x42, 0x4D, 0, 0 });
        byte[] tga = new byte[18]; tga[2] = 2; tga[16] = 32; tga[17] = 8;
        Directory.CreateDirectory(Path.Combine(md, "texture"));
        File.WriteAllBytes(Path.Combine(md, "texture", "glass.tga"), tga);
        List<string> ht = Omsi.O3dTextures(Path.Combine(md, "house.o3d"));
        T("OMSI: o3d에서 텍스처 이름 읽기", ht.Count == 2 && ht[0] == "wall.bmp" && ht[1] == "glass.tga", string.Join(",", ht.ToArray()));
        T("OMSI: 알파 채널 판별 (tga 32비트 / bmp)", Omsi.HasAlpha(Path.Combine(md, "texture", "glass.tga")) && !Omsi.HasAlpha(Path.Combine(md, "wall.bmp")), "");
        string so = Path.Combine(root, "omsi", "Sceneryobjects", "test");
        ScoOptions sop = new ScoOptions(); sop.Alpha = 2;
        ScoResult sr = ScoMaker.Make(new List<string> { Path.Combine(md, "house.o3d"), Path.Combine(md, "fence.o3d") }, so, sop);
        string sco = File.Exists(Path.Combine(so, "house.sco")) ? File.ReadAllText(Path.Combine(so, "house.sco"), Encoding.Default) : "";
        T("OMSI: 모델마다 .sco + 모델/텍스처 복사, 없는 텍스처 보고", sr.Sco.Count == 2 && File.Exists(Path.Combine(so, "model", "house.o3d")) && File.Exists(Path.Combine(so, "texture", "glass.tga"))
            && File.Exists(Path.Combine(so, "texture", "wall.bmp")) && sr.Missing.Count == 1 && sr.Missing[0] == "fence: tex.bmp", sr.Sco.Count + " missing=" + string.Join(",", sr.Missing.ToArray()));
        T("OMSI: .sco 내용 (메시, 충돌, 투명 재질)", sco.Contains("[mesh]\r\nhouse.o3d") && sco.Contains("[collision_mesh]\r\nhouse.o3d") && sco.Contains("[matl]\r\nglass.tga\r\n0\r\n[matl_alpha]\r\n2")
            && !sco.Contains("[matl]\r\nwall.bmp") && sco.Contains("[groups]\r\n1\r\nBus Depot") && sco.Contains("[fixed]"), sco);
        sop.Combine = true; sop.Name = "Haus"; sop.Collision = false;
        ScoResult sr2 = ScoMaker.Make(new List<string> { Path.Combine(md, "house.o3d"), Path.Combine(md, "fence.o3d") }, so, sop);
        string sco2 = sr2.Sco.Count == 1 ? File.ReadAllText(sr2.Sco[0], Encoding.Default) : "";
        T("OMSI: 하나로 합친 .sco", sr2.Sco.Count == 1 && sr2.Sco[0].EndsWith("Haus.sco") && sco2.Contains("house.o3d") && sco2.Contains("fence.o3d") && !sco2.Contains("[collision_mesh]"), sco2);

        // 4-3. OMSI 스플라인(.sli)
        SplineSpec sp1 = new SplineSpec();
        string sli = sp1.Text("road.bmp", "walk.bmp");
        List<SplineSpec.PathDef> pth = sp1.Paths();
        T("OMSI: 스플라인 기본값 (인도 2 + 도로 1 프로필, 경로 4개, 오른쪽 통행)", Count(sli, "[profile]") == 3 && Count(sli, "[path]") == 4 && Count(sli, "[texture]") == 2
            && pth[0].Dir == 1 && pth[1].Dir == 0 && pth[2].Kind == 1 && pth[2].Dir == 2 && Math.Abs(sp1.TotalW - 10.5) < 1e-9, sli);
        sp1.Lanes = 3; sp1.OneWay = true; sp1.PerLane = true; sp1.WalkL = 0;
        string sli2 = sp1.Text("road.bmp", "walk.bmp");
        T("OMSI: 일방통행 3차선, 차선마다 텍스처, 왼쪽 인도 없음", Count(sli2, "[profile]") == 4 && sp1.Paths().TrueForAll(delegate(SplineSpec.PathDef q) { return q.Kind == 1 || q.Dir == 0; })
            && sli2.Contains("[profilepnt]\r\n-4.875\r\n0\r\n0\r\n0.2"), sli2);
        SplineSpec sp2 = SplineSpec.Unpack(sp1.Pack());
        T("OMSI: 스플라인 값 저장/복원", sp2.Lanes == 3 && sp2.OneWay && sp2.PerLane && sp2.WalkL == 0 && sp2.Pack() == sp1.Pack() && SplineSpec.Unpack("x|y").Lanes == 2, sp1.Pack());
        SplineSpec sp4 = new SplineSpec(); sp4.Lanes = 3;
        int backR = sp4.BackLanes; sp4.ExtraRight = false; int backL = sp4.BackLanes;
        SplineSpec sp5 = SplineSpec.Unpack(sp4.Pack());
        T("스플라인: 홀수 차선은 남는 차선을 왼쪽/오른쪽으로 (저장/복원 포함)", backR == 1 && backL == 2 && sp4.Paths()[1].Dir == 1 && sp4.Paths()[2].Dir == 0 && !sp5.ExtraRight
            && SplineSpec.Unpack("2|3.25|0|0|2|2|0.15|5|2||").ExtraRight, backR + "/" + backL);
        string sdir = Path.Combine(root, "omsi", "Splines", "BusDepot");
        sp1.WalkL = 2;
        string sf = sp1.Save(sdir, "Test Road");
        byte[] bm = File.Exists(Path.Combine(sdir, "texture", "busdepot_asphalt.bmp")) ? File.ReadAllBytes(Path.Combine(sdir, "texture", "busdepot_asphalt.bmp")) : new byte[0];
        T("OMSI: 스플라인 저장 + 기본 텍스처 (256x256 BMP)", File.Exists(sf) && sf.EndsWith("Test Road.sli") && bm.Length == 54 + 256 * 256 * 3 && bm[0] == 'B' && bm[1] == 'M'
            && File.Exists(Path.Combine(sdir, "texture", "busdepot_pavement.bmp")), sf + " " + bm.Length);
        string err0; SplineSpec sp3 = new SplineSpec(); sp3.LaneW = 0.2;
        T("OMSI: 잘못된 값 거르기", !sp3.Valid(out err0) && err0 == "spl_err_lanew", err0);

        // 4-4. 모델 보기: o3d 버전 1/7(긴 번호), 암호화 거부, cfg 읽기(LOD, [visible], [matl_alpha], 도색), obj 내보내기 방향
        O3dModel m1 = O3dModel.Load(Path.Combine(md, "house.o3d"));
        T("o3d 읽기: 버전 1 (정점/삼각형/재질)", m1.Version == 1 && m1.VCount == 4 && m1.TCount == 3 && m1.Mats.Count == 2 && m1.Mats[1].Tex == "glass.tga", m1.VCount + "/" + m1.TCount + "/" + m1.Mats.Count);
        string v7 = Path.Combine(md, "v7.o3d");
        using (BinaryWriter bw = new BinaryWriter(File.Create(v7))) {
            bw.Write(new byte[] { 0x84, 0x19, 0x07, 0x01 }); bw.Write(0xFFFFFFFFu);
            bw.Write((byte)0x17); bw.Write(3);
            float[][] vv = new float[][] { new float[] { 0, 0, 0 }, new float[] { 1, 0, 0 }, new float[] { 0, 1, 2 } };
            foreach (float[] q in vv) { bw.Write(q[0]); bw.Write(q[1]); bw.Write(q[2]); bw.Write(0f); bw.Write(0f); bw.Write(-1f); bw.Write(q[0]); bw.Write(q[1]); }
            bw.Write((byte)0x49); bw.Write(1); bw.Write(0); bw.Write(1); bw.Write(2); bw.Write((ushort)0);
            bw.Write((byte)0x26); bw.Write((ushort)1); for (int i = 0; i < 11; i++) bw.Write(1f); bw.Write((byte)8); bw.Write(Encoding.ASCII.GetBytes("wall.bmp"));
            bw.Write((byte)0x79); for (int i = 0; i < 16; i++) bw.Write(i % 5 == 0 ? 1f : 0f);
        }
        O3dModel m7 = O3dModel.Load(v7);
        T("o3d 읽기: 버전 7 + 32비트 번호", m7.Version == 7 && m7.VCount == 3 && m7.TCount == 1 && m7.T[2] == 2 && m7.V[8 * 2 + 2] == 2f && m7.Mats[0].Tex == "wall.bmp", m7.Version + " " + m7.VCount);
        byte[] enc7 = File.ReadAllBytes(v7); enc7[4] = 0x12; File.WriteAllBytes(Path.Combine(md, "enc.o3d"), enc7);
        string encErr = ""; try { O3dModel.Load(Path.Combine(md, "enc.o3d")); } catch (Exception ex) { encErr = ex.Message; }
        T("o3d 읽기: 암호화된 파일은 거부", encErr == L.T("o3d_err_encrypted"), encErr);
        T("o3d 읽기: 버전 7 텍스처 목록", Omsi.O3dTextures(v7).Count == 1, "");
        // 버스 폴더 모양: Vehicles\Bus\Model\model.cfg, Texture\, 도색 폴더
        string bus = Path.Combine(root, "Bus"), bm2 = Path.Combine(bus, "Model"), bt = Path.Combine(bus, "Texture"), bw2 = Path.Combine(bt, "Werbung");
        Directory.CreateDirectory(Path.Combine(bm2, "parts")); Directory.CreateDirectory(bw2);
        File.Copy(Path.Combine(md, "house.o3d"), Path.Combine(bm2, "parts", "body.o3d"), true);
        File.Copy(v7, Path.Combine(bm2, "parts", "wheel.o3d"), true);
        File.WriteAllBytes(Path.Combine(bt, "wall.bmp"), new byte[] { 0x42, 0x4D });
        File.WriteAllBytes(Path.Combine(bt, "glass.tga"), tga);
        File.WriteAllBytes(Path.Combine(bw2, "wall_Red.bmp"), new byte[] { 0x42, 0x4D });
        File.WriteAllText(Path.Combine(bm2, "model.cfg"), "[CTC]\r\nColorscheme\r\nTexture\\Werbung\r\n0\r\n[LOD]\r\n1\r\n[mesh]\r\nparts\\body.o3d\r\n[matl]\r\nglass.tga\r\n0\r\n[matl_alpha]\r\n2\r\n" +
            "[mesh]\r\nparts\\wheel.o3d\r\n[visible]\r\nwheel_type\r\n1\r\n[mesh]\r\nparts\\wheel.o3d\r\n[visible]\r\nwheel_type\r\n2\r\n[mesh]\r\nparts\\inside.o3d\r\n[viewpoint]\r\n2\r\n" +
            "[LOD]\r\n0.1\r\n[mesh]\r\nparts\\body.o3d\r\n", Encoding.ASCII);
        ModelCfg mc = ModelCfg.Load(Path.Combine(bm2, "model.cfg"));
        T("cfg 읽기: 부품, LOD, [visible], [viewpoint], [matl_alpha]", mc.Meshes.Count == 5 && mc.BestLod == 1 && mc.Meshes[1].VisVar == "wheel_type" && mc.Meshes[3].ViewPoint == 2
            && mc.Meshes[0].UsesAlpha("glass.tga") && !mc.Meshes[0].UsesAlpha("wall.bmp") && mc.Meshes[4].Lod == 2, mc.Meshes.Count + " best=" + mc.BestLod);
        T("cfg 읽기: 처음 보이는 부품 (자세한 LOD, 밖, 조건부는 첫 값만)", mc.DefaultOn(mc.Meshes[0]) && mc.DefaultOn(mc.Meshes[1]) && !mc.DefaultOn(mc.Meshes[2]) && !mc.DefaultOn(mc.Meshes[3]) && !mc.DefaultOn(mc.Meshes[4]), "");
        string ft = mc.FindTex("wall.bmp", mc.Meshes[0]), fg = mc.FindTex("glass.tga", mc.Meshes[0]);
        T("cfg 읽기: 도색 폴더의 텍스처를 먼저, 없으면 Texture", ft != null && ft.EndsWith("wall_Red.bmp") && fg != null && fg.EndsWith(Path.Combine("Texture", "glass.tga")), ft + " | " + fg);
        // cfg가 Model\cfg\ 안에 있고 [mesh]는 Model 기준인 차량 (실제 차량에서 흔한 모양)
        File.WriteAllText(Path.Combine(bus, "Bus.bus"), "[model]\r\nmodel\\cfg\\nested.cfg\r\n", Encoding.ASCII);
        Directory.CreateDirectory(Path.Combine(bm2, "cfg"));
        File.WriteAllText(Path.Combine(bm2, "cfg", "nested.cfg"), "[mesh]\r\nparts\\body.o3d\r\n[mesh]\r\nparts\\wheel.o3d\r\n[mesh]\r\nparts\\none.o3d\r\n", Encoding.ASCII);
        ModelCfg mn = ModelCfg.Load(Path.Combine(bm2, "cfg", "nested.cfg"));
        T("cfg 읽기: Model\\cfg\\ 안의 cfg도 차량 폴더를 찾아 부품/텍스처를 찾음", mn.VehicleDir == bus && mn.Meshes[0].Exists && mn.Meshes[1].Exists && !mn.Meshes[2].Exists
            && mn.FindTex("glass.tga", mn.Meshes[0]) != null && mn.FindTex("glass.tga", mn.Meshes[0]).EndsWith(Path.Combine("Texture", "glass.tga")), mn.VehicleDir + " | " + mn.Meshes[0].Path);
        // 도색(.cti): [CTCTexture] 자리 → 도색 파일, [setvar] → [visible]
        File.WriteAllText(Path.Combine(bm2, "paint.cfg"), "[CTC]\r\nColorscheme\r\nTexture\\Werbung\r\n0\r\n[CTCTexture]\r\nBody\r\nwall.bmp\r\n" +
            "[mesh]\r\nparts\\body.o3d\r\n[mesh]\r\nparts\\wheel.o3d\r\n[visible]\r\nwheel_type\r\n1\r\n[mesh]\r\nparts\\wheel.o3d\r\n[visible]\r\nwheel_type\r\n2\r\n", Encoding.ASCII);
        Directory.CreateDirectory(Path.Combine(bw2, "blue"));
        File.WriteAllBytes(Path.Combine(bw2, "blue", "body_blue.bmp"), new byte[] { 0x42, 0x4D });
        File.WriteAllText(Path.Combine(bw2, "Blue.cti"), "***\r\nBlue\r\n***\r\n[item]\r\nBlue\r\nBody\r\nblue\\body_blue.bmp\r\n\r\n[setvar]\r\nwheel_type\r\n2\r\n", Encoding.ASCII);
        ModelCfg mp = ModelCfg.Load(Path.Combine(bm2, "paint.cfg"));
        List<Repaint> rps = mp.Repaints();
        bool noPaintFirst = mp.DefaultOn(mp.Meshes[1]) && !mp.DefaultOn(mp.Meshes[2]);
        mp.Paint = rps.Count > 0 ? rps[0] : null;
        string pt = mp.FindTex("wall.bmp", mp.Meshes[0]);
        T("도색: cti의 텍스처로 바꾸고 [setvar]로 조건부 부품을 고름", rps.Count == 1 && rps[0].Name == "Blue" && pt != null && pt.EndsWith("body_blue.bmp")
            && noPaintFirst && !mp.DefaultOn(mp.Meshes[1]) && mp.DefaultOn(mp.Meshes[2]), rps.Count + " " + pt);
        mp.Paint = null;
        T("도색: 기본 텍스처로 돌리면 원래 텍스처 (cti가 있으면 이름 규칙으로 찾지 않음)", mp.FindTex("wall.bmp", mp.Meshes[0]).EndsWith(Path.Combine("Texture", "wall.bmp")), mp.FindTex("wall.bmp", mp.Meshes[0]));

        string sco3 = Path.Combine(so, "house.sco");
        ModelCfg ms = ModelCfg.Load(sco3);
        T("cfg 읽기: 오브젝트 .sco (model\\, texture\\ 기준)", ms.Meshes.Count == 1 && ms.Meshes[0].Exists && ms.FindTex("glass.tga", ms.Meshes[0]) != null, ms.Meshes.Count.ToString());
        List<string> exErr = new List<string>();
        string objOut = Path.Combine(root, "export", "bus.obj"); Directory.CreateDirectory(Path.GetDirectoryName(objOut));
        List<string> exMiss = ObjExport.Write(mc, new List<CfgMesh> { mc.Meshes[0], mc.Meshes[1], mc.Meshes[3] }, objOut, exErr);
        string objTxt = File.ReadAllText(objOut), mtlTxt = File.ReadAllText(Path.ChangeExtension(objOut, ".mtl"));
        // wheel.o3d의 세 번째 정점 (0, 1, 2) → obj에서는 z를 뒤집어 (0, 1, -2), UV v도 뒤집음, 면 방향 반대 (1 3 2)
        T("obj 내보내기: 부품마다 o, z 뒤집기, 면 방향, 텍스처 경로, 투명은 [matl_alpha]만", objTxt.Contains("o body") && objTxt.Contains("o wheel") && objTxt.Contains("v 0 1 -2")
            && objTxt.Contains("vt 0 0\n") == false && objTxt.Contains("f 5/5/5 7/7/7 6/6/6") && mtlTxt.Contains("map_Kd " + Path.Combine(bw2, "wall_Red.bmp")) && mtlTxt.Contains("map_d " + Path.Combine(bt, "glass.tga"))
            && Count(mtlTxt, "map_d") == 1 && exErr.Count == 1 && exErr[0].StartsWith(@"parts\inside.o3d"), objTxt.Length + " err=" + string.Join(",", exErr.ToArray()) + "\n" + mtlTxt);

        // 4-5. 텍스처 읽기: tga(무압축/RLE), dds(DXT1/DXT5)
        byte[] t2 = new byte[18 + 4 * 4]; t2[2] = 2; t2[12] = 2; t2[14] = 2; t2[16] = 32; t2[17] = 8 | 0x20;
        for (int i = 0; i < 4; i++) { t2[18 + i * 4] = (byte)(i * 60); t2[19 + i * 4] = 10; t2[20 + i * 4] = 20; t2[21 + i * 4] = 128; }
        byte[] t10 = new byte[] { 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 24, 0x20, 0x83, 1, 2, 3 };   // RLE: 같은 픽셀 4개
        BitmapSource b2 = TexLoad.Tga(t2), b10 = TexLoad.Tga(t10);
        byte[] px2 = new byte[16], px10 = new byte[16];
        b2.CopyPixels(px2, 8, 0); b10.CopyPixels(px10, 8, 0);
        T("텍스처: TGA 무압축 32비트 + RLE 24비트", px2[4] == 60 && px2[7] == 128 && px10[12] == 1 && px10[13] == 2 && px10[14] == 3 && px10[15] == 255, px2[4] + " " + px10[12]);
        byte[] dds = new byte[128 + 8]; Encoding.ASCII.GetBytes("DDS ").CopyTo(dds, 0);
        BitConverter.GetBytes(4).CopyTo(dds, 12); BitConverter.GetBytes(4).CopyTo(dds, 16); BitConverter.GetBytes(4).CopyTo(dds, 80); Encoding.ASCII.GetBytes("DXT1").CopyTo(dds, 84);
        dds[128] = 0x00; dds[129] = 0xF8; dds[130] = 0x1F; dds[131] = 0x00;   // color0 = 빨강(F800), color1 = 파랑(001F), 모든 픽셀 0번 = 빨강
        BitmapSource bd = TexLoad.Dds(dds); byte[] pd = new byte[64]; bd.CopyPixels(pd, 16, 0);
        T("텍스처: DDS DXT1", bd.PixelWidth == 4 && pd[2] == 255 && pd[1] == 0 && pd[0] == 0 && pd[3] == 255, pd[0] + "," + pd[1] + "," + pd[2]);

        // 5. 변환 준비 (Core)
        Settings cs = new Settings(); cs.Inp = @"C:\no\such\folder"; cs.Blender = ""; cs.Conv = "";
        string err, note;
        T("준비: 없는 입력 폴더", Conversion.Prepare(cs, Conversion.BuildOptions(cs, "fast", ""), out err, out note) == null && err == "msg_err_in", err);
        cs.Inp = od;
        T("준비: Blender 없으면 표준 방식 막힘", Conversion.Prepare(cs, Conversion.BuildOptions(cs, "std", ""), out err, out note) == null && err == "msg_err_bl", err);
        List<ObjItem> prep = Conversion.Prepare(cs, Conversion.BuildOptions(cs, "fast", ""), out err, out note);
        T("준비: 빠른 변환은 도구 없이 가능", prep != null && prep.Count == 1, err);
        T("준비: 없는 도구 목록", Conversion.MissingTools(cs).Count == 2, "");

        // 5-1. 글꼴: fonts 폴더가 있으면 나눔스퀘어 네오 / Noto Sans KR, 중국어/일본어 글자는 대체 글꼴이 그린다
        foreach (string[] fn in new string[][] { new string[] { "nanum", "NanumSquare Neo" }, new string[] { "noto", "Noto Sans KR" } }) {
            if (!Theme.BundledFont(fn[0])) { T("글꼴: " + fn[1] + " 파일", false, "fonts 폴더 없음"); continue; }
            System.Windows.Media.GlyphTypeface gt, gb;
            bool reg = new System.Windows.Media.Typeface(Theme.MakeFont(fn[0]), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out gt)
                && gt.FamilyNames.Values.Contains(fn[1]);
            bool bold = new System.Windows.Media.Typeface(Theme.MakeFont(fn[0]), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal).TryGetGlyphTypeface(out gb) && gb.Weight == FontWeights.Bold;
            T("글꼴: " + fn[1] + " (보통/굵게)", reg && bold, reg + " " + bold);
        }

        // 5-2. 환영 화면: 인사말이 모든 언어로
        try {
            Settings ws = new Settings(); ws.Path = Path.Combine(root, "wel.ini"); ws.Lang = "ko";
            WelcomeWindow ww = new WelcomeWindow(ws);
            string[] gs = ww.Greetings;
            T("환영 화면: 모든 언어로 인사 (" + gs.Length + "개)", gs.Length >= 5 && gs[0] == "안녕하세요" && Array.IndexOf(gs, "こんにちは") > 0 && Array.IndexOf(gs, "你好") > 0 && Array.IndexOf(gs, "Hallo") > 0,
                string.Join(", ", gs));
            ww.Close();
        } catch (Exception ex) { T("환영 화면", false, ex.Message); }

        // 5-3. MiSans: 설치돼 있으면 그것, 없으면 나눔스퀘어 네오로 대신 (글꼴 파일은 배포하지 않는다)
        {
            FontFamily mi = Theme.MiSans(), mf = Theme.MakeFont("misans");
            bool ok = mi != null ? mf.Source.StartsWith(mi.Source) : mf.Source.Contains("NanumSquare Neo");
            T("글꼴: MiSans " + (mi != null ? "설치됨 (" + mi.Source + ")" : "없음 → 나눔스퀘어 네오"), ok, mf.Source);
        }

        // 6. XAML 화면
        List<string> bad = new List<string>();
        foreach (string n in Xaml.Names()) { try { Xaml.Load(n); } catch (Exception ex) { bad.Add(n + ": " + ex.Message); } }
        T("XAML: 모든 화면 읽기 (" + Xaml.Names().Length + "개)", bad.Count == 0, string.Join(" / ", bad.ToArray()));

        // 7. 메인 창 + ViewModel
        try {
            Settings us = new Settings(); us.Path = Path.Combine(root, "ui.ini"); us.Lang = "ko"; us.Theme = "light"; us.Inp = big; us.AddRecent(big); us.AddRecent(@"C:\no\such\folder");
            us.Blender = ""; us.Conv = ""; us.UpdCheck = false;
            Loc.SetLang("ko"); Theme.Apply("light", "indigo", "nanum");
            MainWindow f = new MainWindow(us, root, false);
            f.FixClientSize(1040, 686); f.Show(); Pump.Run(200);
            foreach (string pg in new string[] { "wizard", "object", "spline", "viewer", "advanced", "settings", "about", "dashboard" }) { f.VM.Page = pg; Pump.Run(50); }
            T("화면: 모든 페이지 전환", f.VM.Page == "dashboard", f.VM.Page);
            T("작업 표시줄: 진행률 표시 준비", f.TaskbarItemInfo != null, "");
            // 끌어다 놓기: obj 폴더는 마법사 경로 단계로, o3d 파일은 오브젝트 만들기로
            f.VM.DropPath(Path.Combine(od, "a.obj")); Pump.Run(60);
            T("끌어다 놓기: obj 파일 → 마법사 경로 단계", f.VM.Page == "wizard" && f.VM.WizStep == 2 && f.VM.InPath == od, f.VM.Page + " " + f.VM.WizStep + " " + f.VM.InPath);
            f.VM.DropPath(Path.Combine(md, "house.o3d")); Pump.Run(60);
            T("끌어다 놓기: o3d → 오브젝트 만들기 + 모델 목록", f.VM.Page == "object" && f.VM.ObjModels == md && f.VM.Models.Count == 4 && f.VM.ObjFolder == "models",
                f.VM.Page + " " + f.VM.Models.Count + " " + f.VM.ObjFolder);
            // 스플라인 화면: 값을 바꾸면 미리보기가 다시 그려진다
            f.VM.Page = "spline"; Pump.Run(80);
            SplinePreview spv = FindAll<SplinePreview>(f.Host).Find(delegate(SplinePreview q) { return q.IsVisible; });
            int v0 = spv == null ? -1 : spv.Version;
            f.VM.SplLanes = 4; f.VM.SplLaneW = "3,5"; Pump.Run(30);
            T("스플라인 화면: 미리보기 + 값 바꾸면 다시 그림", spv != null && spv.ActualWidth > 300 && spv.Version > v0 && f.VM.SplSpec.Lanes == 4 && Math.Abs(f.VM.SplSpec.LaneW - 3.5) < 1e-9
                && f.VM.SplSummary.Contains("14"), spv == null ? "없음" : spv.ActualWidth + " " + f.VM.SplSummary);
            f.VM.SplLaneW = "abc"; Pump.Run(10);
            T("스플라인 화면: 숫자가 아니면 이전 값 유지, 입력은 그대로", Math.Abs(f.VM.SplSpec.LaneW - 3.5) < 1e-9 && f.VM.SplLaneW == "abc", f.VM.SplLaneW);
            // 모델 보기: 다른 스레드에서 읽은 부품이 3D 화면에 보이고, 묶음을 바꾸면 켜진 부품이 바뀐다
            f.VM.DropPath(Path.Combine(bm2, "model.cfg")); Pump.Run(60);
            for (int i = 0; i < 200 && f.VM.ViewBusy; i++) Pump.Run(30);
            Pump.Run(100);
            ModelView mv3 = FindAll<ModelView>(f.Host).Find(delegate(ModelView q) { return q.IsVisible; });
            int onParts = 0; foreach (PartItem pi in f.VM.Parts) if (pi.Checked) onParts++;
            T("모델 보기: cfg를 끌어다 놓으면 열림 + 3D에 부품 표시", f.VM.Page == "viewer" && f.VM.Parts.Count == 5 && onParts == 2 && mv3 != null && mv3.Visible == 2 && f.VM.ViewStatus.Contains("2 / 5"),
                f.VM.Page + " parts=" + f.VM.Parts.Count + " on=" + onParts + " vis=" + (mv3 == null ? -1 : mv3.Visible) + " " + f.VM.ViewStatus);
            f.VM.ViewPreset = "all"; Pump.Run(50);
            T("모델 보기: '전체'는 자세한 LOD의 조건부 부품까지", mv3 != null && mv3.Visible == 3, mv3 == null ? "없음" : mv3.Visible.ToString());
            f.VM.Parts[0].Checked = false; Pump.Run(30);
            T("모델 보기: 부품 스위치를 끄면 3D에서 사라짐", mv3 != null && mv3.Visible == 2, mv3 == null ? "없음" : mv3.Visible.ToString());
            f.VM.InPath = big; f.VM.Page = "dashboard"; Pump.Run(60);   // 끌어다 놓기로 바꾼 입력 폴더를 되돌린다 (아래 마법사 점검용)
            foreach (string th in new string[] { "dark", "light", "system" }) foreach (string ac in new string[] { "indigo", "teal", "rose" }) {
                f.VM.ThemeName = th; f.VM.AccentName = ac; Pump.Run(20);
            }
            f.VM.ThemeName = "light"; f.VM.AccentName = "indigo";
            f.VM.Lang = "en"; Pump.Run(50);
            T("언어 전환: 상태/배너 문구도 바뀜", f.VM.StatusText == L.T("status_blender_missing") && f.VM.StatusText.StartsWith("Required") && f.VM.BannerText.StartsWith("Missing"), f.VM.StatusText + " | " + f.VM.BannerText);
            f.VM.Lang = "de"; Pump.Run(50);
            T("언어 전환: 독일어 상태 문구", f.VM.StatusText == "Programmpfad fehlt", f.VM.StatusText);
            f.VM.Lang = "en"; Pump.Run(20);
            bool en = f.VM.NextText == "Next" || L.Lang == "en";
            f.VM.Lang = "ko"; Pump.Run(50);
            T("화면: 테마 9가지 + 언어 전환(재시작 없음)", en && L.Lang == "ko", L.Lang);

            // 마법사
            f.VM.Page = "wizard"; Pump.Run(50);
            T("마법사: 들어오면 1단계, 도구 없으면 빠른 변환", f.VM.WizStep == 1 && f.VM.IsFast && !f.VM.StdEnabled, f.VM.WizStep + " " + f.VM.WizMode);
            f.VM.WizNextCmd.Execute(null); Pump.Run(30);
            T("마법사: 2단계 + 최근 폴더 2개(없는 폴더 표시)", f.VM.WizStep == 2 && f.VM.Recent.Count == 2 && !f.VM.Recent[0].Exists && f.VM.Recent[1].Exists, f.VM.Recent.Count.ToString());
            T("마법사: 빠른 변환이면 출력 폴더 잠김", f.VM.OutLocked && f.VM.OutPath == big, f.VM.OutPath);
            f.VM.WizNextCmd.Execute(null); Pump.Run(50);
            bool parShown = false;
            foreach (NumBox nbx in FindAll<NumBox>(f.Host)) if (nbx.IsVisible) parShown = true;
            T("마법사: 3단계, 빠른 변환이면 OmsiXConv 동시 실행 숨김", f.VM.WizStep == 3 && !parShown, f.VM.WizStep + " " + parShown);
            f.VM.WizNextCmd.Execute(null); Pump.Run(30);
            T("마법사: 4단계 요약", f.VM.WizStep == 4 && f.VM.Summary[1] == big && f.VM.NextText == L.T("btn_run"), f.VM.Summary[1]);
            f.VM.WizNextCmd.Execute(null);
            for (int i = 0; i < 300 && f.VM.Running; i++) Pump.Run(20);
            Pump.Run(100);
            T("로그: 모아 보낸 줄이 화면 로그에 그대로",  f.LogText.Length > 0 && f.LogText == f.VM.LogContent, f.LogText.Length + " / " + f.VM.LogContent.Length);
            T("마법사: 실행하면 대시보드 결과 표에 60줄", f.VM.Page == "dashboard" && f.VM.Results.Count == 60 && f.VM.Tab == "res" && !f.VM.Running, f.VM.Results.Count + " " + f.VM.Page);

            // 면책 오버레이
            f.Disclaimer(false, null); Pump.Run(50);
            DialogHost dh = f.LastDialog;
            T("면책: 같은 창 안 오버레이로 표시", f.OverlayCount == 1 && dh != null, f.OverlayCount.ToString());
            if (dh != null) {
                string credits = null;
                foreach (TextBlock tb in FindAll<TextBlock>(dh)) {
                    string tx = InlineText(tb.Inlines);   // 링크(Hyperlink) 안의 글자까지
                    if (tx.Contains("qbasys08")) credits = tx;
                }
                T("면책: 맨 아래 크레딧 (HYB1022 · qbasys08 · Claude)", credits != null && credits.Contains("HYB1022") && !credits.Contains("Junyoung") && credits.Contains("Claude"), credits ?? "없음");
                Button okb = dh.Btn[dh.Btn.Length - 1];
                T("면책: 체크 전에는 계속 버튼 비활성", !okb.IsEnabled, "");
                dh.Check.IsChecked = true; Pump.Run(20);
                T("면책: 체크하면 계속 버튼 활성", okb.IsEnabled, "");
                okb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); Pump.Run(50);
                T("면책: 계속 누르면 오버레이 사라지고 동의 기록", f.OverlayCount == 0 && us.Agreed && us.AgreedVer == AppInfo.Version, f.OverlayCount.ToString());
            }

            // 숫자 상자 / 언어 드롭다운
            f.VM.Page = "advanced"; Pump.Run(50);
            NumBox nb = UiTree.Find<NumBox>(f.Host);
            f.VM.Par = 7; Pump.Run(20);
            T("고급: 동시 실행 숫자 상자가 설정과 연결", nb != null && nb.Value == 7, nb == null ? "없음" : nb.Value.ToString());
            f.VM.Page = "settings"; Pump.Run(50);
            ComboBox lc = UiTree.Find<ComboBox>(f.Host);
            T("설정: 언어 드롭다운 (" + L.Languages.Length + "개 언어)", lc != null && lc.Items.Count == L.Languages.Length && (string)lc.SelectedValue == "ko", lc == null ? "없음" : lc.Items.Count.ToString());
            if (lc != null) { lc.SelectedValue = "en"; Pump.Run(30); }
            T("설정: 드롭다운으로 언어 전환", L.Lang == "en" && us.Lang == "en", L.Lang);
            f.VM.Lang = "ko"; Pump.Run(20);
            f.VM.FontName = "noto"; Pump.Run(30);
            bool noto = us.Font == "noto" && Theme.Font.Source.Contains("Noto Sans KR");
            f.VM.FontName = "system"; Pump.Run(30);
            bool sys = us.Font == "system" && !Theme.Font.Source.Contains("./#");
            f.VM.FontName = "bogus"; f.VM.FontName = "nanum"; Pump.Run(30);
            T("설정: 글꼴 드롭다운 (나눔/Noto/시스템)", noto && sys && us.Font == "nanum" && f.VM.Fonts.Count == 4, noto + " " + sys + " " + us.Font);
            ComboBox fc = FindAll<ComboBox>(f.Host).Find(delegate(ComboBox c) { return c.IsVisible && c.ItemsSource == f.VM.Fonts; });
            f.VM.Lang = "en"; Pump.Run(60);
            string fcEn = fc == null ? null : fc.SelectedItem as TrItem == null ? null : ((TrItem)fc.SelectedItem).Name;
            f.VM.Lang = "ko"; Pump.Run(60);
            string fcKo = fc == null ? null : fc.SelectedItem as TrItem == null ? null : ((TrItem)fc.SelectedItem).Name;
            f.VM.Lang = "ja"; Pump.Run(80);
            bool aligned = true; int segs = 0;
            foreach (Segmented sg in FindAll<Segmented>(f.Host)) if (sg.IsVisible) { segs++; if (!sg.PillAligned) aligned = false; }
            f.VM.Lang = "ko"; Pump.Run(80);
            foreach (Segmented sg in FindAll<Segmented>(f.Host)) if (sg.IsVisible && !sg.PillAligned) aligned = false;
            T("설정: 언어를 바꿔도 테마/강조 색 선택 표시가 칸에 맞음", segs == 2 && aligned, segs + " " + aligned);
            T("설정: 언어를 바꿔도 글꼴 드롭다운이 비지 않음", fc != null && (string)fc.SelectedValue == "nanum" && fcEn == "NanumSquare Neo" && fcKo == "나눔스퀘어 네오",
                (fc == null ? "없음" : fc.SelectedValue + "") + " / " + fcEn + " / " + fcKo);

            // 개발자 옵션: 정보 화면의 버전을 7번 누르면 메뉴에 생긴다
            FrameworkElement navDev = LogicalTreeHelper.FindLogicalNode(f, "Nav_dev") as FrameworkElement;
            bool hidden = navDev != null && navDev.Visibility != Visibility.Visible;
            f.VM.Page = "about"; Pump.Run(30);
            for (int i = 0; i < 6; i++) f.VM.VersionTapCmd.Execute(null);
            bool notYet = !f.VM.DevMode && f.LastToast == L.T("toast_dev_steps", 1);
            f.VM.VersionTapCmd.Execute(null); Pump.Run(30);
            T("개발자: 버전 7번 → 메뉴 생김 + 알림", hidden && notYet && f.VM.DevMode && us.DevMode && navDev.Visibility == Visibility.Visible && f.LastToast == L.T("toast_dev_on"),
                hidden + " " + notYet + " " + f.VM.DevMode + " " + f.LastToast);
            f.VM.Page = "dev"; Pump.Run(60);
            ComboBox anim = null;
            foreach (ComboBox cb in FindAll<ComboBox>(f.Host)) if (cb.IsVisible) anim = cb;
            T("개발자: 화면 열기 + 애니메이션 배율 선택", f.VM.Page == "dev" && anim != null && (string)anim.SelectedValue == "1" && anim.Items.Count == 7, anim == null ? "없음" : anim.Items.Count.ToString());
            // 애니메이션 배율: 드롭다운으로 고르면 XAML 스위치 움직임도 느려진다
            if (anim != null) { anim.SelectedValue = "10"; Pump.Run(30); }
            bool scaled = us.DevAnim == 10 && Motion.Scale == 10;
            CheckBox fpsBox = null;
            foreach (CheckBox cb in FindAll<CheckBox>(f.Host)) if (cb.IsVisible && cb.GetBindingExpression(CheckBox.IsCheckedProperty) != null
                && cb.GetBindingExpression(CheckBox.IsCheckedProperty).ParentBinding.Path.Path == "DevFps") fpsBox = cb;
            System.Windows.Shapes.Ellipse knob = fpsBox == null ? null : FindAll<System.Windows.Shapes.Ellipse>(fpsBox).Find(delegate(System.Windows.Shapes.Ellipse el) { return el.Name == "knob"; });
            bool off = Motion.Off; Motion.Off = false;   // 이 점검만 애니메이션을 켠다
            f.VM.DevFps = true; Pump.Run(300);
            TranslateTransform kt = knob == null ? null : knob.RenderTransform as TranslateTransform;
            double midX = kt == null ? -1 : kt.X;      // 0.18초 × 10배 = 1.8초 중 0.3초 → 아직 가는 중
            T("개발자: 애니메이션 배율이 스위치에도 적용 (드롭다운 10x)", scaled && midX > 0.5 && midX < 19, scaled + " x=" + midX.ToString("0.0"));
            double popOp = -1;
            if (anim != null) {                     // 드롭다운 목록도 배율대로 천천히 나타난다
                anim.IsDropDownOpen = true; Pump.Run(150);
                System.Windows.Controls.Primitives.Popup pp = anim.Template.FindName("PART_Popup", anim) as System.Windows.Controls.Primitives.Popup;
                if (pp != null && pp.Child != null) popOp = pp.Child.Opacity;
                anim.IsDropDownOpen = false; Pump.Run(30);
            }
            T("개발자: 드롭다운 목록도 배율 적용 (10x에서 아직 나타나는 중)", popOp > 0 && popOp < 0.6, popOp.ToString("0.00"));
            if (anim != null) { anim.SelectedValue = "1"; Pump.Run(30); }
            f.VM.DevFps = false; Pump.Run(450);
            T("개발자: 배율 1x면 스위치가 0.18초 안에 끝까지", Motion.Scale == 1 && kt != null && Math.Abs(kt.X) < 0.5, kt == null ? "없음" : kt.X.ToString("0.0"));
            Motion.Off = off;
            Button devRow = FindAll<Button>(f.Host).Find(delegate(Button b) { return b.IsVisible && b.Command == f.VM.BugReportCmd; });
            T("개발자: 스위치/값 선택은 줄 전체가 눌림 + 물결", fpsBox != null && fpsBox.ActualWidth > 400 && FindAll<Ripple>(fpsBox).Count == 1
                && anim != null && anim.ActualWidth > 400 && FindAll<Ripple>(anim).Count == 1, fpsBox == null ? "없음" : fpsBox.ActualWidth + " / " + (anim == null ? 0 : anim.ActualWidth));
            ContentControl infoRow = FindAll<ContentControl>(f.Host).Find(delegate(ContentControl c) { return c.IsVisible && c.Style == f.Host.TryFindResource("DevStatic"); });
            T("누름 효과: 기능 없는 줄(앱 정보 등)에도 물결 층", infoRow != null && FindAll<Ripple>(infoRow).Count == 1, infoRow == null ? "없음" : "");
            T("누름 효과: 버튼 템플릿에 물결 층", devRow != null && FindAll<Ripple>(devRow).Count == 1 && FindAll<Ripple>(f.Host).Count > 3, "");
            f.VM.DevFps = true; f.VM.DevAnim = "0"; Pump.Run(30);
            T("개발자: FPS 표시 + 애니메이션 끄기", f.FpsVisible && Motion.Scale == 0 && Motion.Disabled, f.FpsVisible + " " + Motion.Scale);
            f.VM.DevMode = false; Pump.Run(60);
            T("개발자: 끄면 메뉴 사라지고 정보로 돌아감", f.VM.Page == "about" && navDev.Visibility != Visibility.Visible && !f.FpsVisible && Motion.Scale == 1,
                f.VM.Page + " " + navDev.Visibility + " " + Motion.Scale);
            f.VM.DevAnim = "1"; f.VM.DevFps = false;

            // 업데이트 알림: 창 없이 메뉴 표시
            f.VM.FakeUpdate("9.9"); Pump.Run(20);
            T("업데이트: 정보 메뉴에 [업데이트] 표시, 대화 상자 없음", f.VM.UpdNavBadge == "about_pill_update" && f.OverlayCount == 0, "");
            T("업데이트: 정보 화면 줄에는 '업데이트' 글자만 (버전 없음)", f.VM.UpdValue == L.T("about_pill_update"), f.VM.UpdValue);
            f.Close();
        } catch (Exception ex) { T("화면 구성", false, ex.ToString()); }

        sb.AppendLine(fail == 0 ? "ALL PASS" : (fail + " FAILED"));
        Console.WriteLine(fail == 0 ? "ALL PASS" : (fail + " FAILED"));
        try { File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(true)); } catch (Exception) { }
        return fail == 0 ? 0 : 1;
    }
}
}
