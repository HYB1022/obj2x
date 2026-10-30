using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Obj2x.UI {

// obj2x.exe --selftest : 설정/문구/변환기/엔진(중지 포함)/XAML 화면/ViewModel을 자동으로 점검한다.
public static class SelfTest {
    static int fail = 0;
    static StringBuilder sb = new StringBuilder();
    static void T(string name, bool ok, string detail) {
        string line = ok ? "PASS " + name : "FAIL " + name + ": " + detail;
        if (!ok) fail++;
        sb.AppendLine(line); Console.WriteLine(line);
    }

    public static int Run(string here) {
        Motion.Off = true;
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
        T("업데이트: 버전 비교", Updater.IsNewer("2.1", "2.0") && !Updater.IsNewer("2.0", "2.0") && Updater.IsNewer("2.10", "2.9"), "");

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

        // 5. 변환 준비 (Core)
        Settings cs = new Settings(); cs.Inp = @"C:\no\such\folder"; cs.Blender = ""; cs.Conv = "";
        string err, note;
        T("준비: 없는 입력 폴더", Conversion.Prepare(cs, Conversion.BuildOptions(cs, "fast", ""), out err, out note) == null && err == "msg_err_in", err);
        cs.Inp = od;
        T("준비: Blender 없으면 표준 방식 막힘", Conversion.Prepare(cs, Conversion.BuildOptions(cs, "std", ""), out err, out note) == null && err == "msg_err_bl", err);
        List<ObjItem> prep = Conversion.Prepare(cs, Conversion.BuildOptions(cs, "fast", ""), out err, out note);
        T("준비: 빠른 변환은 도구 없이 가능", prep != null && prep.Count == 1, err);
        T("준비: 없는 도구 목록", Conversion.MissingTools(cs).Count == 2, "");

        // 6. XAML 화면
        List<string> bad = new List<string>();
        foreach (string n in Xaml.Names()) { try { Xaml.Load(n); } catch (Exception ex) { bad.Add(n + ": " + ex.Message); } }
        T("XAML: 모든 화면 읽기 (" + Xaml.Names().Length + "개)", bad.Count == 0, string.Join(" / ", bad.ToArray()));

        // 7. 메인 창 + ViewModel
        try {
            Settings us = new Settings(); us.Path = Path.Combine(root, "ui.ini"); us.Lang = "ko"; us.Theme = "light"; us.Inp = big; us.AddRecent(big); us.AddRecent(@"C:\no\such\folder");
            us.Blender = ""; us.Conv = ""; us.UpdCheck = false;
            Loc.SetLang("ko"); Theme.Apply("light", "indigo", false);
            MainWindow f = new MainWindow(us, root, false);
            f.FixClientSize(1040, 686); f.Show(); Pump.Run(200);
            foreach (string pg in new string[] { "wizard", "advanced", "settings", "about", "dashboard" }) { f.VM.Page = pg; Pump.Run(50); }
            T("화면: 모든 페이지 전환", f.VM.Page == "dashboard", f.VM.Page);
            foreach (string th in new string[] { "dark", "light", "system" }) foreach (string ac in new string[] { "indigo", "teal", "rose" }) {
                f.VM.ThemeName = th; f.VM.AccentName = ac; Pump.Run(20);
            }
            f.VM.ThemeName = "light"; f.VM.AccentName = "indigo";
            f.VM.Lang = "en"; Pump.Run(50);
            bool en = f.VM.NextText == "Next" || L.Lang == "en";
            f.VM.Lang = "ko"; Pump.Run(50);
            T("화면: 테마 9가지 + 언어 전환(재시작 없음)", en && L.Lang == "ko", L.Lang);

            // 마법사
            f.VM.Page = "wizard"; Pump.Run(50);
            T("마법사: 들어오면 1단계, 도구 없으면 빠른 변환", f.VM.WizStep == 1 && f.VM.IsFast && !f.VM.StdEnabled, f.VM.WizStep + " " + f.VM.WizMode);
            f.VM.WizNextCmd.Execute(null); Pump.Run(30);
            T("마법사: 2단계 + 최근 폴더 2개(없는 폴더 표시)", f.VM.WizStep == 2 && f.VM.Recent.Count == 2 && !f.VM.Recent[0].Exists && f.VM.Recent[1].Exists, f.VM.Recent.Count.ToString());
            T("마법사: 빠른 변환이면 출력 폴더 잠김", f.VM.OutLocked && f.VM.OutPath == big, f.VM.OutPath);
            f.VM.WizNextCmd.Execute(null); f.VM.WizNextCmd.Execute(null); Pump.Run(30);
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

            // 업데이트 알림: 창 없이 메뉴 표시
            f.VM.FakeUpdate("9.9"); Pump.Run(20);
            T("업데이트: 정보 메뉴에 [업데이트] 표시, 대화 상자 없음", f.VM.UpdNavBadge == "about_pill_update" && f.OverlayCount == 0, "");
            f.Close();
        } catch (Exception ex) { T("화면 구성", false, ex.ToString()); }

        sb.AppendLine(fail == 0 ? "ALL PASS" : (fail + " FAILED"));
        Console.WriteLine(fail == 0 ? "ALL PASS" : (fail + " FAILED"));
        try { File.WriteAllText(Path.Combine(here, "obj2x_selftest.txt"), sb.ToString(), new UTF8Encoding(true)); } catch (Exception) { }
        return fail == 0 ? 0 : 1;
    }
}
}
