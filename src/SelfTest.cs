using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Obj2x {

// obj2x.exe --selftest : 설정/문구/변환기/엔진(중지 포함)/화면 구성을 자동으로 점검한다.
public static class SelfTest {
    static int fail = 0;
    static StringBuilder sb = new StringBuilder();
    static void T(string name, bool ok, string detail) {
        string line = ok ? "PASS " + name : "FAIL " + name + ": " + detail;
        if (!ok) fail++;
        sb.AppendLine(line); Console.WriteLine(line);
    }
    static void Click(Control c) {
        typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, new object[] { EventArgs.Empty });
    }
    static List<T> Find<T>(Control root) where T : Control {
        List<T> r = new List<T>();
        foreach (Control c in root.Controls) { if (c is T) r.Add((T)c); r.AddRange(Find<T>(c)); }
        return r;
    }
    static void Pump(int ms) {
        DateTime end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(10); }
    }

    public static int Run(string here) {
        Anim.Off = true;
        string root = Path.Combine(Path.GetTempPath(), "obj2x_selftest_cs");
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (Exception) { }
        Directory.CreateDirectory(root);

        // 1. 문구표
        string par = L.Parity();
        T("문구표: ko/en 키 일치", par.Length == 0, par);
        T("문구표: 키 개수", L.Count > 150, L.Count.ToString());
        L.Lang = "ko"; string k1 = L.T("m_fast_done", 3, 5, "1.5");
        L.Lang = "en"; string k2 = L.T("m_fast_done", 3, 5, "1.5");
        T("문구표: 서식 채우기", k1.Contains("3") && k2.Contains("3") && k1 != k2, k1 + " | " + k2);
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
        T("업데이트: 버전 비교", MainForm.IsNewer("2.1", "2.0") && !MainForm.IsNewer("2.0", "2.0") && MainForm.IsNewer("2.10", "2.9"), "");

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

        // 5. 엔진: Blender 단계 중지 (유닉스에서만, 가짜 blender 스크립트)
        if (Environment.OSVersion.Platform == PlatformID.Unix) {
            string fake = Path.Combine(root, "blender.sh");
            File.WriteAllText(fake, "#!/bin/sh\necho 'OK one'\nsleep 30\n");
            Process.Start("chmod", "+x \"" + fake + "\"").WaitForExit();
            ro = new RunOptions(); ro.Mode = "x"; ro.In = od; ro.Out = od; ro.Blender = fake; ro.Py = Path.Combine(root, "obj2x.py");
            Engine e3 = new Engine(); bool fin3 = false, stopped3 = false; DateTime st0 = DateTime.Now;
            e3.OnSay = delegate(string t) { if (t.StartsWith("OK")) e3.Stop(); };
            e3.OnFinish = delegate(bool st) { stopped3 = st; fin3 = true; };
            e3.Start(ro, Engine.FindObjs(od, true));
            for (int i = 0; i < 200 && !fin3; i++) Thread.Sleep(20);
            T("엔진(Blender 단계): 중간에 중지", fin3 && stopped3 && (DateTime.Now - st0).TotalSeconds < 8, "fin=" + fin3 + " stopped=" + stopped3);
        }

        // 6. 화면
        try {
            Settings us = new Settings(); us.Path = Path.Combine(root, "ui.ini"); us.Lang = "ko"; us.Theme = "light"; us.Inp = od; us.AddRecent(od); us.AddRecent(@"C:\no\such\folder");
            MainForm f = new MainForm(us);
            f.WindowState = FormWindowState.Normal; f.Show(); Pump(200);
            foreach (string pg in new string[] { "wizard", "advanced", "settings", "about", "dashboard" }) { f.SwitchPage(pg); Pump(50); }
            T("화면: 모든 페이지 전환", true, "");
            foreach (string th in new string[] { "dark", "light", "system" }) foreach (string ac in new string[] { "indigo", "teal", "rose" }) {
                f.S.Theme = th; f.S.Accent = ac; f.ApplyTheme(); Pump(20);
            }
            f.S.Lang = "en"; f.ApplyLanguage(); Pump(50); f.S.Lang = "ko"; f.ApplyLanguage(); Pump(50);
            T("화면: 테마 9가지 + 언어 전환(재시작 없음)", !f.IsDisposed, "");
            // 면책 오버레이
            bool? result = null;
            f.ShowDisclaimer(false);
            Pump(50);
            List<DialogOverlay> ovs = Find<DialogOverlay>(f);
            T("면책: 같은 창 안 오버레이로 표시", ovs.Count == 1 && ovs[0].Parent == f, ovs.Count.ToString());
            if (ovs.Count == 1) {
                RButton okb = ovs[0].Btn[ovs[0].Btn.Length - 1];
                T("면책: 체크 전에는 계속 버튼 비활성", okb != null && !okb.Enabled, "");
                List<Check> cks = Find<Check>(ovs[0]);
                cks[0].Checked = true; Pump(20);
                T("면책: 체크하면 계속 버튼 활성", okb.Enabled, "");
                Click(okb); Pump(50);
                T("면책: 계속 누르면 오버레이 사라지고 동의 기록", Find<DialogOverlay>(f).Count == 0 && f.S.Agreed && f.S.AgreedVer == Program.AppVersion, "");
            }
            f.Close();
        } catch (Exception ex) { T("화면 구성", false, ex.ToString()); }

        sb.AppendLine(fail == 0 ? "ALL PASS" : (fail + " FAILED"));
        Console.WriteLine(fail == 0 ? "ALL PASS" : (fail + " FAILED"));
        try { File.WriteAllText(Path.Combine(here, "obj2x_selftest.txt"), sb.ToString(), new UTF8Encoding(true)); } catch (Exception) { }
        return fail == 0 ? 0 : 1;
    }
}
}
