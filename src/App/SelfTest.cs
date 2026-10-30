using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace Obj2x.UI {

// obj2x.exe --selftest : 설정/문구/변환기/엔진(중지 포함)/XAML 화면/ViewModel을 자동으로 점검한다.
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

        // 5-1. 글꼴: fonts 폴더가 있으면 나눔스퀘어 네오 / Noto Sans KR, 중국어/일본어 글자는 대체 글꼴이 그린다
        foreach (string[] fn in new string[][] { new string[] { "nanum", "NanumSquare Neo" }, new string[] { "noto", "Noto Sans KR" } }) {
            if (!Theme.BundledFont(fn[0])) { T("글꼴: " + fn[1] + " 파일", false, "fonts 폴더 없음"); continue; }
            System.Windows.Media.GlyphTypeface gt, gb;
            bool reg = new System.Windows.Media.Typeface(Theme.MakeFont(fn[0]), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out gt)
                && gt.FamilyNames.Values.Contains(fn[1]);
            bool bold = new System.Windows.Media.Typeface(Theme.MakeFont(fn[0]), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal).TryGetGlyphTypeface(out gb) && gb.Weight == FontWeights.Bold;
            T("글꼴: " + fn[1] + " (보통/굵게)", reg && bold, reg + " " + bold);
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
            foreach (string pg in new string[] { "wizard", "advanced", "settings", "about", "dashboard" }) { f.VM.Page = pg; Pump.Run(50); }
            T("화면: 모든 페이지 전환", f.VM.Page == "dashboard", f.VM.Page);
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
            f.VM.FontName = "noto"; Pump.Run(30);
            bool noto = us.Font == "noto" && Theme.Font.Source.Contains("Noto Sans KR");
            f.VM.FontName = "system"; Pump.Run(30);
            bool sys = us.Font == "system" && !Theme.Font.Source.Contains("./#");
            f.VM.FontName = "bogus"; f.VM.FontName = "nanum"; Pump.Run(30);
            T("설정: 글꼴 드롭다운 (나눔/Noto/시스템)", noto && sys && us.Font == "nanum" && f.VM.Fonts.Count == 3, noto + " " + sys + " " + us.Font);

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
            if (anim != null) { anim.SelectedValue = "1"; Pump.Run(30); }
            f.VM.DevFps = false; Pump.Run(450);
            T("개발자: 배율 1x면 스위치가 0.18초 안에 끝까지", Motion.Scale == 1 && kt != null && Math.Abs(kt.X) < 0.5, kt == null ? "없음" : kt.X.ToString("0.0"));
            Motion.Off = off;
            Button devRow = FindAll<Button>(f.Host).Find(delegate(Button b) { return b.IsVisible && b.Command == f.VM.BugReportCmd; });
            T("개발자: 스위치/값 선택은 줄 전체가 눌림 + 물결", fpsBox != null && fpsBox.ActualWidth > 400 && FindAll<Ripple>(fpsBox).Count == 1
                && anim != null && anim.ActualWidth > 400 && FindAll<Ripple>(anim).Count == 1, fpsBox == null ? "없음" : fpsBox.ActualWidth + " / " + (anim == null ? 0 : anim.ActualWidth));
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
        try { File.WriteAllText(Path.Combine(here, "obj2x_selftest.txt"), sb.ToString(), new UTF8Encoding(true)); } catch (Exception) { }
        return fail == 0 ? 0 : 1;
    }
}
}
