using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Obj2x;

namespace BusDepotMac;

// --selftest: 화면 없이 맵/버스/스플라인/빠른 변환과 맥 쪽 처리(백슬래시 경로, 인코딩, Mods 폴더)를 점검한다.
// Windows에서도 돌릴 수 있다 (경로 변환은 맥에서만 실제로 일어나므로, 맥에서 한 번 더 돌려 보면 좋다)
public static class MacSelfTest {
    static readonly List<string> lines = new List<string>();
    static int fail;
    static void T(string name, bool ok, string detail = "") { lines.Add((ok ? "PASS " : "FAIL ") + name + (ok || detail.Length == 0 ? "" : ": " + detail)); if (!ok) fail++; }

    public static int Run(string outFile) {
        string root = Path.Combine(Path.GetTempPath(), "BusDepot_mac_selftest");
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (Exception) { }
        Directory.CreateDirectory(root);
        try {
            T("인코딩: 코드 페이지 949/1252 사용 가능", Encoding.GetEncoding(949) != null && Encoding.GetEncoding(1252) != null);
            T("문구: 맥 전용 문구가 모든 언어에", Check("mac_nav_convert") && Check("mac_about_desc") && Check("mac_make_root"));
            T("경로: OMSI 파일 속 백슬래시 경로", Omsi.IsWindows ? Omsi.Rel(@"a\b.o3d") == @"a\b.o3d" : Omsi.Rel(@"a\b.o3d") == "a/b.o3d");

            // 빠른 변환
            string od = Path.Combine(root, "obj"); Directory.CreateDirectory(Path.Combine(od, "sub"));
            File.WriteAllText(Path.Combine(od, "a.mtl"), "newmtl m\nKd 1 0 0\n");
            File.WriteAllText(Path.Combine(od, "a.obj"), "mtllib a.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nvn 0 0 1\nusemtl m\nf 1//1 2//1 3//1 4//1\n");
            File.Copy(Path.Combine(od, "a.obj"), Path.Combine(od, "sub", "b.obj"));
            File.Copy(Path.Combine(od, "a.mtl"), Path.Combine(od, "sub", "a.mtl"));
            List<ObjItem> objs = ObjList.Find(od, true);
            int nv, nt, nm; string w;
            FastO3D.ConvertEx(objs[0].Path, Path.Combine(od, objs[0].Rel + ".o3d"), out nv, out nt, out nm, out w);
            O3dModel m = O3dModel.Load(Path.Combine(od, objs[0].Rel + ".o3d"));
            T("빠른 변환: obj 목록 + 사각형 → 삼각형 2개", objs.Count == 2 && nt == 2 && m.TCount == 2, objs.Count + " " + nt);

            // 버스: .bus의 백슬래시 경로, paths/cabin, [newanim]
            string vb = Path.Combine(root, "Bus"), vm = Path.Combine(vb, "model");
            Directory.CreateDirectory(vm);
            File.Copy(Path.Combine(od, "a.o3d"), Path.Combine(vm, "door.o3d"));
            File.WriteAllText(Path.Combine(vb, "a.bus"), "[friendlyname]\r\nMaker\r\nCity\r\n[model]\r\nmodel\\m.cfg\r\n[paths]\r\nmodel\\p.cfg\r\n[passengercabin]\r\nmodel\\c.cfg\r\n");
            File.WriteAllText(Path.Combine(vm, "m.cfg"), "[mesh]\r\ndoor.o3d\r\n[newanim]\r\norigin_trans\r\n1\r\n0\r\n0\r\nanim_rot\r\ndoor_0\r\n90\r\n");
            File.WriteAllText(Path.Combine(vm, "p.cfg"), "[pathpnt]\r\n0\r\n0\r\n0.5\r\n[pathpnt]\r\n0\r\n-2\r\n0.5\r\n[pathlink]\r\n0\r\n1\r\n");
            File.WriteAllText(Path.Combine(vm, "c.cfg"), "[passpos]\r\n0.5\r\n1\r\n1.2\r\n0.4\r\n0\r\n[entry]\r\n0\r\n");
            BusFile bf = BusFile.Load(Path.Combine(vb, "a.bus"));
            ModelCfg cfg = ModelCfg.Load(bf.Model);
            PathsCfg pc = PathsCfg.Load(bf.Paths); CabinCfg cc = CabinCfg.Load(bf.Cabin);
            T("버스: .bus → model/paths/cabin (백슬래시 경로)", bf.Name == "Maker City" && File.Exists(bf.Model) && pc.Points.Count == 2 && cc.Seats.Count == 1, bf.Model);
            T("버스: 부품 파일 + [newanim]", cfg.Meshes.Count == 1 && cfg.Meshes[0].Exists && cfg.Meshes[0].Anims.Count == 1 && cfg.Meshes[0].Anims[0].Entries[0].Factor == 90, cfg.Meshes.Count > 0 ? cfg.Meshes[0].Path : "");
            pc.SetPoint(pc.Points[1], 1, -3); pc.Doc.Save();
            T("버스: 값만 바꿔 저장 (.bak)", PathsCfg.Load(bf.Paths).Points[1].P[1] == -3 && File.Exists(bf.Paths + ".bak"));

            // 맵: Mods 폴더에 있는 오브젝트는 없는 것으로 치지 않는다
            string om = Path.Combine(root, "OMSI 2"), mods = Path.Combine(root, "Mods"), md = Path.Combine(om, "maps", "T");
            Directory.CreateDirectory(md); Directory.CreateDirectory(Path.Combine(om, "Sceneryobjects", "a")); Directory.CreateDirectory(Path.Combine(mods, "MyMod", "Sceneryobjects", "b"));
            File.WriteAllText(Path.Combine(om, "Omsi.exe"), "");
            File.WriteAllText(Path.Combine(om, "Sceneryobjects", "a", "x.sco"), "");
            File.WriteAllText(Path.Combine(mods, "MyMod", "Sceneryobjects", "b", "y.sco"), "");
            File.WriteAllText(Path.Combine(md, "global.cfg"), "[name]\r\nT\r\n[map]\r\n0\r\n0\r\ntile_0_0.map\r\n", Encoding.Unicode);
            File.WriteAllText(Path.Combine(md, "tile_0_0.map"), "[object]\r\n0\r\nSceneryobjects\\a\\x.sco\r\n1\r\n1\r\n1\r\n0\r\n0\r\n\r\n[object]\r\n0\r\nSceneryobjects\\b\\y.sco\r\n2\r\n2\r\n2\r\n0\r\n0\r\n\r\n[object]\r\n0\r\nSceneryobjects\\c\\z.sco\r\n3\r\n3\r\n3\r\n0\r\n0\r\n", Encoding.Unicode);
            List<string> keep = MapInfo.ExtraRoots;
            MapInfo.ExtraRoots = new List<string> { mods, Path.Combine(mods, "MyMod") };
            MapInfo mi = MapInfo.Load(md, om, null);
            MapInfo.ExtraRoots = keep;
            T("맵: OMSI + Mods 폴더 기준으로 없는 오브젝트", mi.Objects.Count == 3 && mi.MissingObjects.Count == 1 && mi.MissingObjects[0].Path.EndsWith("z.sco"), mi.MissingObjects.Count.ToString());

            // 스플라인 만들기 → 읽기
            SplineSpec sp = new SplineSpec();
            string sli = sp.Save(Path.Combine(root, "Splines", "BD"), "road");
            SliFile sf = SliFile.Load(sli);
            T("스플라인: 만들기 + 다시 읽기 (텍스처 찾기)", sf.Profiles.Count == sp.Profiles().Count && sf.Paths.Count == sp.Paths().Count && sf.FindTex(0) != null, sli);

            // HOF 목록
            string vv = Path.Combine(om, "Vehicles");
            foreach (string d in new[] { "A", "B" }) { Directory.CreateDirectory(Path.Combine(vv, d)); File.WriteAllText(Path.Combine(vv, d, "x.hof"), "[name]\r\nHof\r\n"); }
            List<string[]> hl = MapPage.HofList(new List<string> { om });
            T("HOF: 같은 hof는 하나로", hl.Count == 1 && hl[0][1] == "Hof", hl.Count.ToString());
        } catch (Exception ex) { T("예외 없이 끝남", false, ex.ToString()); }
        try { Directory.Delete(root, true); } catch (Exception) { }
        File.WriteAllLines(outFile, lines, new UTF8Encoding(true));
        return fail == 0 ? 0 : 1;
    }

    static bool Check(string key) {
        string keep = L.Lang; bool ok = true;
        foreach (string[] l in L.Languages) { L.Lang = l[0]; string t = L.T(key); if (string.IsNullOrEmpty(t) || t == key) ok = false; }
        L.Lang = keep;
        return ok;
    }
}
