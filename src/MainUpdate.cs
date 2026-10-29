using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Obj2x {

public partial class MainForm {
    string updText = null;            // 링크 라벨에 보여줄 문구 키 (없으면 저장소 주소)
    string updLatest = "", updUrl = "", updZip = "";
    bool updBusy, updInstalling;

    string RepoUrl() { return "https://github.com/" + Program.Repo; }

    void UpdateLinkText() {
        if (hero != null) hero.Invalidate();
        NavButton nb;
        if (navs.TryGetValue("about", out nb) && nb.Badge != (updLatest.Length > 0)) { nb.Badge = updLatest.Length > 0; nb.Invalidate(); }
    }

    static string JsonStr(string json, string key) {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        return m.Success ? m.Groups[1].Value.Replace("\\/", "/").Replace("\\\"", "\"") : "";
    }

    public static bool IsNewer(string latest, string current) {
        try { return new Version(latest).CompareTo(new Version(current)) > 0; }
        catch (Exception) { return latest != current; }
    }

    // GitHub 최신 릴리스 조회 (백그라운드). 새 버전이 있으면 창을 띄우지 않고 정보 탭의 업데이트 줄과 메뉴 점으로만 알린다.
    public void StartUpdateCheck(bool manual) {
        if (updBusy) return;
        updBusy = true;
        Thread th = new Thread(delegate() {
            string json = null; string err = null;
            try {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch (Exception) { }
                WebClient wc = new WebClient();
                wc.Headers[HttpRequestHeader.UserAgent] = "obj2x";
                wc.Encoding = Encoding.UTF8;
                json = wc.DownloadString("https://api.github.com/repos/" + Program.Repo + "/releases/latest");
            } catch (Exception ex) { err = ex.Message; }
            UI(delegate {
                updBusy = false;
                if (json == null) { updText = "upd_link_fail"; UpdateLinkText(); return; }
                string tag = JsonStr(json, "tag_name").TrimStart('v', 'V');
                if (tag.Length == 0) { updText = "upd_link_fail"; UpdateLinkText(); return; }
                if (IsNewer(tag, Program.AppVersion)) {
                    updLatest = tag; updUrl = JsonStr(json, "html_url"); updZip = JsonStr(json, "zipball_url");
                    Match m = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.zip)\"");
                    if (m.Success) updZip = m.Groups[1].Value;
                    updText = "upd_link_new"; UpdateLinkText();
                } else { updText = "upd_link_latest"; UpdateLinkText(); }
            });
        });
        th.IsBackground = true; th.Start();
    }

    // 개발용(--shots): 새 버전이 있는 것처럼 표시한다
    public void FakeUpdate(string ver) { updLatest = ver; updText = "upd_link_new"; UpdateLinkText(); }

    // 내려받아 풀고, 실행 중인 exe는 이름을 바꿔 둔 뒤 새 파일로 교체하고 다시 시작한다.
    void DoUpdate() {
        if (updInstalling) return;
        if (running) { Dialogs.Msg(this, L.T("upd_title"), L.T("upd_busy")); return; }
        if (!updZip.StartsWith("https://")) { try { Process.Start(updUrl); } catch (Exception) { } return; }
        updInstalling = true; UpdateLinkText();
        Cursor = Cursors.WaitCursor;
        Say(L.T("upd_dl", updLatest));
        string zipUrl = updZip, here = Here, ver = Program.AppVersion;
        SaveAll();
        Thread th = new Thread(delegate() {
            string tmp = Path.Combine(Path.GetTempPath(), "obj2x_upd_" + Guid.NewGuid().ToString("N"));
            string err = null;
            try {
                Directory.CreateDirectory(tmp);
                string zip = Path.Combine(tmp, "update.zip");
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch (Exception) { }
                WebClient wc = new WebClient();
                wc.Headers[HttpRequestHeader.UserAgent] = "obj2x";
                wc.DownloadFile(zipUrl, zip);
                string ex = Path.Combine(tmp, "x");
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath '" + zip + "' -DestinationPath '" + ex + "' -Force\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) { p.WaitForExit(); }
                string[] found = Directory.GetFiles(ex, "obj2x.exe", SearchOption.AllDirectories);
                if (found.Length == 0) throw new Exception(L.T("upd_nofile"));
                string root = Path.GetDirectoryName(found[0]);
                string bak = Path.Combine(here, "obj2x_backup");
                Directory.CreateDirectory(bak);
                foreach (string n in new string[] { "obj2x.exe", "obj2x.bat", "obj2x.py", "obj2x_gui.ps1" }) {
                    string src = Path.Combine(here, n);
                    if (File.Exists(src)) File.Copy(src, Path.Combine(bak, n + ".v" + ver), true);
                }
                string myExe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string old = myExe + ".old";
                try { if (File.Exists(old)) File.Delete(old); } catch (Exception) { }
                File.Move(myExe, old);
                CopyTree(root, here, root);
            } catch (Exception e) { err = e.Message; }
            try { Directory.Delete(tmp, true); } catch (Exception) { }
            UI(delegate {
                Cursor = Cursors.Default;
                if (err != null) {
                    updInstalling = false; UpdateLinkText();
                    Dialogs.Msg(this, L.T("upd_title"), L.T("upd_error", err));
                    try { if (updUrl.Length > 0) Process.Start(updUrl); } catch (Exception) { }
                    return;
                }
                Say(L.T("upd_installed"));
                try { Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location); } catch (Exception) { }
                Close();
            });
        });
        th.IsBackground = true; th.Start();
    }

    static void CopyTree(string src, string dst, string root) {
        foreach (string f in Directory.GetFiles(src)) {
            string name = Path.GetFileName(f);
            if (string.Equals(name, "obj2x.ini", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(f, Path.Combine(dst, name), true);
        }
        foreach (string d in Directory.GetDirectories(src)) {
            string nd = Path.Combine(dst, Path.GetFileName(d));
            if (!Directory.Exists(nd)) Directory.CreateDirectory(nd);
            CopyTree(d, nd, root);
        }
    }
}
}
