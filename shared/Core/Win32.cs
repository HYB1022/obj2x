using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Obj2x {
// OmsiXConv 창을 찾아 조작하는 Win32 도우미
public static class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc p, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="SendMessageW")] static extern IntPtr SendMessageStr(IntPtr h, int m, IntPtr w, string l);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h, int id);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern int GetDlgCtrlID(IntPtr h);
    public static List<IntPtr> Top(uint pid) {
        List<IntPtr> r = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h)) r.Add(h);
            return true; }, IntPtr.Zero);
        return r;
    }
    public static List<IntPtr> TopAll(uint pid) {
        List<IntPtr> r = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid) r.Add(h);
            return true; }, IntPtr.Zero);
        return r;
    }
    public static void Hide(IntPtr h) { SetWindowPos(h, IntPtr.Zero, -32000, -32000, 0, 0, 0x0015); }
    public static int Id(IntPtr h) { return GetDlgCtrlID(h); }
    public static List<IntPtr> Kids(IntPtr hw) {
        List<IntPtr> r = new List<IntPtr>();
        EnumChildWindows(hw, delegate(IntPtr h, IntPtr l) { r.Add(h); return true; }, IntPtr.Zero);
        return r;
    }
    public static string Text(IntPtr h) { StringBuilder s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
    public static string Cls(IntPtr h) { StringBuilder s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
    public static int Check(IntPtr h) { return (int)SendMessage(h, 0x00F0, IntPtr.Zero, IntPtr.Zero); }
    public static void Click(IntPtr h) { PostMessage(h, 0x00F5, IntPtr.Zero, IntPtr.Zero); }
    public static void SetText(IntPtr h, string t) { SendMessageStr(h, 0x000C, IntPtr.Zero, t); }
    public static IntPtr Dlg(IntPtr h, int id) { return GetDlgItem(h, id); }
}
}
