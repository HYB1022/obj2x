using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Obj2x {

public class UpdateInfo {
    public string Version = "", PageUrl = "", ZipUrl = "";
}

// GitHub 최신 릴리스 확인과 설치. 모두 호출한 스레드에서 막히므로 백그라운드에서 부른다.
public static class Updater {
    public static bool IsNewer(string latest, string current) {
        try { return new Version(latest).CompareTo(new Version(current)) > 0; }
        catch (Exception) { return latest != current; }
    }

    static string JsonStr(string json, string key) {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        return m.Success ? m.Groups[1].Value.Replace("\\/", "/").Replace("\\\"", "\"") : "";
    }

    static WebClient Client() {
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch (Exception) { }
        WebClient wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = AppInfo.Exe;
        wc.Encoding = Encoding.UTF8;
        return wc;
    }

    // 최신 릴리스. 실패하면 null
    public static UpdateInfo Latest() {
        string json;
        try { json = Client().DownloadString("https://api.github.com/repos/" + AppInfo.Repo + "/releases/latest"); }
        catch (Exception) { return null; }
        UpdateInfo u = new UpdateInfo();
        u.Version = JsonStr(json, "tag_name").TrimStart('v', 'V');
        if (u.Version.Length == 0) return null;
        u.PageUrl = JsonStr(json, "html_url");
        u.ZipUrl = JsonStr(json, "zipball_url");
        Match m = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.zip)\"");
        if (m.Success) u.ZipUrl = m.Groups[1].Value;
        return u;
    }

    // 내려받아 풀고, 실행 중인 exe는 이름을 바꿔 둔 뒤 새 파일로 교체한다. 성공하면 null, 실패하면 오류 문구.
    // exePath는 지금 실행 중인 exe, here는 그 폴더.
    public static string Install(UpdateInfo u, string here, string exePath) {
        if (u == null || !u.ZipUrl.StartsWith("https://")) return "no zip";
        string tmp = Path.Combine(Path.GetTempPath(), "BusDepot_upd_" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(tmp);
            string zip = Path.Combine(tmp, "update.zip");
            Client().DownloadFile(u.ZipUrl, zip);
            string ex = Path.Combine(tmp, "x");
            ZipFile.ExtractToDirectory(zip, ex);
            // 새 이름(BusDepot.exe)을 먼저, 없으면 예전 이름(obj2x.exe)이 있는 폴더를 쓴다
            string[] found = Directory.GetFiles(ex, AppInfo.Exe + ".exe", SearchOption.AllDirectories);
            if (found.Length == 0) found = Directory.GetFiles(ex, "obj2x.exe", SearchOption.AllDirectories);
            if (found.Length == 0) return L.T("upd_nofile");
            string root = Path.GetDirectoryName(found[0]);
            string bak = Path.Combine(here, AppInfo.Exe + "_backup");
            Directory.CreateDirectory(bak);
            foreach (string n in new string[] { "BusDepot.exe", "BusDepot.bat", "BusDepot.py", "obj2x.exe", "obj2x.bat", "obj2x.py", "obj2x_gui.ps1" }) {
                string src = Path.Combine(here, n);
                if (File.Exists(src)) File.Copy(src, Path.Combine(bak, n + ".v" + AppInfo.Version), true);
            }
            string old = exePath + ".old";
            try { if (File.Exists(old)) File.Delete(old); } catch (Exception) { }
            File.Move(exePath, old);
            CopyTree(root, here);
            return null;
        } catch (Exception e) {
            return e.Message;
        } finally {
            try { Directory.Delete(tmp, true); } catch (Exception) { }
        }
    }

    // 지난 업데이트가 남긴 exe.old 지우기 (시작할 때)
    public static void CleanupOld(string exePath) {
        try { string old = exePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch (Exception) { }
        // v2.7.2 이하에서 올라오면 그 업데이터가 남긴 obj2x.exe.old (예전 본체)도 지운다
        try { string old = Path.Combine(Path.GetDirectoryName(exePath), "obj2x.exe.old"); if (File.Exists(old)) File.Delete(old); } catch (Exception) { }
    }

    // obj2x(v2.7.2 이하)에서 올라온 뒤 남은 파일 정리: 설정은 BusDepot.ini로 옮긴 다음이고, 예전 바로 가기용 obj2x.exe(런처)만 남긴다
    static readonly string[] Obj2xLeftovers = { "obj2x.ini", "obj2x.bat", "obj2x_gui.ps1", "obj2x.py", "obj2x_selftest.txt", "obj2x_fps.txt" };
    public static void CleanupObj2x(string here) {
        if (!File.Exists(Path.Combine(here, AppInfo.Exe + ".exe"))) return;    // 개발 폴더 등 BusDepot.exe 옆이 아니면 건드리지 않는다
        if (File.Exists(Path.Combine(here, "obj2x.ini")) && !File.Exists(Path.Combine(here, AppInfo.Exe + ".ini"))) return;   // 아직 못 옮겼으면 남긴다
        foreach (string n in Obj2xLeftovers) { try { string p = Path.Combine(here, n); if (File.Exists(p)) File.Delete(p); } catch (Exception) { } }
        try { string b = Path.Combine(here, "obj2x_backup"); if (Directory.Exists(b)) Directory.Delete(b, true); } catch (Exception) { }
    }

    // 실행 중인 앱이 fonts 폴더의 글꼴 파일을 열고 있어서 덮어쓰지 못할 수 있다.
    // 내용이 같으면 건너뛰고, 글꼴처럼 앱 실행에 꼭 필요하지 않은 하위 폴더 파일은 못 바꿔도 업데이트를 계속한다.
    static void CopyTree(string src, string dst) { CopyTree(src, dst, true); }
    static void CopyTree(string src, string dst, bool top) {
        foreach (string f in Directory.GetFiles(src)) {
            string name = Path.GetFileName(f);
            if (string.Equals(name, "obj2x.ini", StringComparison.OrdinalIgnoreCase) || string.Equals(name, AppInfo.Exe + ".ini", StringComparison.OrdinalIgnoreCase)) continue;
            // 예전 버전 업데이터용 파일(zip 최상위)은 새 업데이터가 설치할 때는 다시 깔지 않는다. obj2x.exe 런처는 이미 있을 때(바로 가기용)만 바꾼다
            if (top && (string.Equals(name, "obj2x.bat", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "obj2x_gui.ps1", StringComparison.OrdinalIgnoreCase))) continue;
            if (top && string.Equals(name, "obj2x.exe", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(dst, name))) continue;
            string to = Path.Combine(dst, name);
            if (SameFile(f, to)) continue;
            try { File.Copy(f, to, true); }
            catch (IOException) { if (top) throw; }
            catch (UnauthorizedAccessException) { if (top) throw; }
        }
        foreach (string d in Directory.GetDirectories(src)) {
            string nd = Path.Combine(dst, Path.GetFileName(d));
            if (!Directory.Exists(nd)) Directory.CreateDirectory(nd);
            CopyTree(d, nd, false);
        }
    }
    static bool SameFile(string a, string b) {
        try {
            FileInfo fa = new FileInfo(a), fb = new FileInfo(b);
            if (!fb.Exists || fa.Length != fb.Length) return false;
            using (FileStream x = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (FileStream y = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                byte[] bx = new byte[65536], by = new byte[65536];
                while (true) {
                    int nx = x.Read(bx, 0, bx.Length), ny = y.Read(by, 0, by.Length);
                    if (nx != ny) return false;
                    if (nx == 0) return true;
                    for (int i = 0; i < nx; i++) if (bx[i] != by[i]) return false;
                }
            }
        } catch (Exception) { return false; }
    }

    public static void OpenUrl(string url) { try { Process.Start(url); } catch (Exception) { } }
}
}
