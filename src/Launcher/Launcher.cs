using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Bus Depot (obj2x launcher)")]
[assembly: AssemblyDescription("Starts BusDepot.exe. Kept so that shortcuts and the auto-update of obj2x v2.7.2 and older keep working.")]
[assembly: AssemblyProduct("Bus Depot")]
[assembly: AssemblyCompany("HYB1022")]
[assembly: AssemblyCopyright("Copyright (C) 2026 Hyunwoo Bang (HYB1022), Junyoung. GPL-3.0-or-later.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: ComVisible(false)]

// obj2x.exe: 이름을 Bus Depot으로 바꾸기 전 버전과 이어 주는 작은 실행 파일.
// v2.7.2 이하의 자동 업데이트는 zip 안의 obj2x.exe로 자기를 바꾼 뒤 obj2x.exe를 다시 실행하고, 바로 가기도 obj2x.exe를 가리킨다.
// 같은 폴더의 BusDepot.exe를 받은 인수 그대로 띄우고 끝난다 (자기를 복사하거나 창 없이 띄우는 일은 하지 않는다).
static class Launcher {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxW(IntPtr h, string text, string caption, uint type);

    [STAThread]
    static int Main(string[] args) {
        string here = AppDomain.CurrentDomain.BaseDirectory;
        string exe = Path.Combine(here, "BusDepot.exe");
        if (!File.Exists(exe)) { MessageBoxW(IntPtr.Zero, "BusDepot.exe was not found next to obj2x.exe.", "Bus Depot", 0x10); return 1; }
        string a = "";
        foreach (string s in args) a += (a.Length > 0 ? " " : "") + "\"" + s.Replace("\"", "\\\"") + "\"";
        ProcessStartInfo psi = new ProcessStartInfo(exe, a);
        psi.WorkingDirectory = here;
        psi.UseShellExecute = true;
        Process.Start(psi);
        return 0;
    }
}
