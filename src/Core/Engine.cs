using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Obj2x {

public class ObjItem {
    public string Path, Rel, Name;
    public DateTime Mtime;
    public long Length;
}

public class ResRow {
    public string Name, V, T, M, Sec, Status;
    public int Kind; // 0 ok, 1 warn, 2 fail
}

public class RunOptions {
    public string Mode = "std";      // std | fast | x
    public string In = "", Out = "", Blender = "", Conv = "", Py = "";
    public int Par = 4;
    public bool Sub, Inc, DelX;
    public Dictionary<string, int> Copt = new Dictionary<string, int>();
    public string ExOpt = "";
    public bool Verbose;             // Blender 출력을 거르지 않고 모두 로그에 (개발자 옵션)
}

// OmsiXConv 한 개를 자동으로 조작하는 작업 상태
class Job {
    public string X, Name, O3d;
    public long Length;
    public string State = "launch";
    public Process Proc;
    public IntPtr Conv = IntPtr.Zero;
    public Dictionary<string, IntPtr> Checks = new Dictionary<string, IntPtr>();
    public DateTime T0 = DateTime.Now, Start = DateTime.Now, Wait = DateTime.Now, DlgAt = DateTime.Now, T00 = DateTime.Now;
    public int Tries, Stable, DlgTries, OkReads;
    public long Last = -1;
    public double TSetup, Timeout;
}

// 변환 엔진. 모든 작업은 백그라운드 스레드에서 돌고, 언제든 Stop()으로 중간에 멈출 수 있다.
public class Engine {
    public Action<string> OnSay;
    public Action<double> OnProgress;
    public Action<string> OnStat;
    public Action<ResRow> OnRow;
    public Action<bool> OnFinish;      // true면 사용자가 중지함
    public Action OnFailedChanged;

    public volatile bool Cancel;
    public volatile bool Running;
    public List<string> Failed = new List<string>();   // 재시도용 .x 경로
    Thread th;
    Process blender;
    readonly object procLock = new object();
    List<Job> active = new List<Job>();

    static readonly string[][] OptPat = new string[][] {
        new string[] { "zup", "z up" }, new string[] { "sep", "Separate Files" }, new string[] { "det", "Show Details" },
        new string[] { "spec", "Remove Specular" }, new string[] { "mat", "Opt. Materials" }, new string[] { "vert", "Opt. Vertices" },
        new string[] { "nrm", "All Normals Up" }, new string[] { "sub", "Submesh" } };

    void Say(string s) { if (OnSay != null) OnSay(s); }
    void Prog(double f) { if (OnProgress != null) OnProgress(f); }
    void Stat(string s) { if (OnStat != null) OnStat(s); }
    void Row(ResRow r) { if (OnRow != null) OnRow(r); }

    // ------------------------------------------------------------ 시작/중지
    public void Start(RunOptions o, List<ObjItem> objs) {
        if (Running) return;
        Cancel = false; Running = true;
        th = new Thread(delegate() {
            bool stopped = false;
            try {
                if (o.Mode == "fast") RunFast(o, objs);
                else RunBlender(o, objs);
            } catch (Exception ex) { Say(ex.Message); }
            stopped = Cancel;
            if (stopped) Say(L.T("m_stopped"));
            Running = false;
            if (OnFinish != null) OnFinish(stopped);
        });
        th.IsBackground = true;
        th.Start();
    }

    public void StartRetry(RunOptions o) {
        if (Running) return;
        List<string> files = new List<string>(Failed);
        Cancel = false; Running = true;
        th = new Thread(delegate() {
            try { O3dStage(o, files); } catch (Exception ex) { Say(ex.Message); }
            bool stopped = Cancel;
            if (stopped) Say(L.T("m_stopped"));
            Running = false;
            if (OnFinish != null) OnFinish(stopped);
        });
        th.IsBackground = true;
        th.Start();
    }

    public void Stop() {
        Cancel = true;
        KillAll();
    }

    public void KillAll() {
        lock (procLock) {
            try { if (blender != null && !blender.HasExited) blender.Kill(); } catch (Exception) { }
            foreach (Job j in active) { try { if (j.Proc != null && !j.Proc.HasExited) j.Proc.Kill(); } catch (Exception) { } }
        }
    }

    // ------------------------------------------------------------ obj 목록
    public static List<ObjItem> FindObjs(string dir, bool sub) {
        List<ObjItem> r = new List<ObjItem>();
        string[] files;
        try { files = Directory.GetFiles(dir, "*.obj", sub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly); }
        catch (Exception) { return r; }
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        string root = dir.TrimEnd('\\', '/');
        foreach (string f in files) {
            if (!f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)) continue;
            FileInfo fi = new FileInfo(f);
            ObjItem it = new ObjItem();
            it.Path = f; it.Length = fi.Length; it.Mtime = fi.LastWriteTime;
            string rel = f.Substring(root.Length).TrimStart('\\', '/');
            it.Rel = rel.Substring(0, rel.Length - 4);
            it.Name = System.IO.Path.GetFileNameWithoutExtension(f);
            r.Add(it);
        }
        return r;
    }

    // 변경된 것만 (결과 파일보다 obj가 새로운 것)
    public static List<ObjItem> FilterChanged(List<ObjItem> objs, string outDir, string ext) {
        List<ObjItem> sel = new List<ObjItem>();
        foreach (ObjItem o in objs) {
            string rf = System.IO.Path.Combine(outDir, o.Rel + ext);
            if (!File.Exists(rf) || o.Mtime > File.GetLastWriteTime(rf)) sel.Add(o);
        }
        return sel;
    }

    // ------------------------------------------------------------ FastO3D
    void RunFast(RunOptions o, List<ObjItem> objs) {
        DateTime t0 = DateTime.Now;
        int ok = 0, warn = 0, i = 0;
        Prog(0);
        foreach (ObjItem it in objs) {
            if (Cancel) break;
            i++;
            Stat(L.T("m_fast_prog", i, objs.Count, it.Rel));
            string dst = System.IO.Path.Combine(o.Out, it.Rel + ".o3d");
            try {
                string dd = System.IO.Path.GetDirectoryName(dst);
                if (!Directory.Exists(dd)) Directory.CreateDirectory(dd);
                DateTime tf = DateTime.Now;
                int nv, nt, nm; string wn;
                FastO3D.ConvertEx(it.Path, dst, out nv, out nt, out nm, out wn);
                ok++;
                string sec = (DateTime.Now - tf).TotalSeconds.ToString("0.00");
                ResRow r = new ResRow();
                r.Name = it.Rel + ".o3d"; r.V = nv.ToString(); r.T = nt.ToString(); r.M = nm.ToString(); r.Sec = sec;
                if (wn.Length > 0) { warn++; r.Status = L.T("r_warn", wn); r.Kind = 1; } else { r.Status = "OK"; r.Kind = 0; }
                Row(r);
                Say(L.T("m_fast_ok", r.Name, nv, nt, nm, wn.Length > 0 ? L.T("m_fast_warn", wn) : ""));
            } catch (Exception ex) {
                string msg = ex.Message;
                ResRow r = new ResRow();
                r.Name = it.Rel + ".o3d"; r.V = "-"; r.T = "-"; r.M = "-"; r.Sec = "-"; r.Status = L.T("r_fail", msg); r.Kind = 2;
                Row(r);
                Say(L.T("m_fast_fail", it.Name, msg));
            }
            Prog((double)i / Math.Max(1, objs.Count));
        }
        double tot = Math.Round((DateTime.Now - t0).TotalSeconds, 1);
        if (!Cancel) {
            Say(L.T("m_fast_done", ok, objs.Count, tot.ToString("0.0")));
            Stat(L.T("m_fast_stat", ok, objs.Count, tot.ToString("0.0"), warn > 0 ? L.T("m_warn_suffix", warn) : ""));
        } else {
            Stat(L.T("status_stopped"));
        }
    }

    // ------------------------------------------------------------ Blender (obj -> x)
    static bool KeepLine(string l) {
        if (l == null) return false;
        if (Regex.IsMatch(l, "^(OK|DONE|obj files|ignored|Traceback|  File)")) return true;
        return l.IndexOf("rror", StringComparison.Ordinal) >= 0;
    }

    void RunBlender(RunOptions o, List<ObjItem> objs) {
        DateTime t0 = DateTime.Now;
        List<string> names = new List<string>();
        List<string> jl = new List<string>();
        foreach (ObjItem it in objs) { names.Add(it.Rel); jl.Add("obj:" + it.Rel); }
        foreach (string ln in o.ExOpt.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (Regex.IsMatch(ln.Trim(), "^[^=]+=")) jl.Add("opt:" + ln.Trim());
        }
        string jobFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "obj2x_job.txt");
        File.WriteAllLines(jobFile, jl.ToArray(), new UTF8Encoding(true));
        lock (Failed) { Failed.Clear(); }
        if (OnFailedChanged != null) OnFailedChanged();

        Prog(0);
        Stat(L.T("m_blender_run"));
        Say(L.T("m_blender_run"));
        ProcessStartInfo psi = new ProcessStartInfo(o.Blender);
        psi.Arguments = "-b -P \"" + o.Py + "\" -- \"" + o.In + "\" \"" + o.Out + "\" \"" + jobFile + "\"";
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        int okLines = 0;
        Process p = new Process();
        p.StartInfo = psi;
        DataReceivedEventHandler h = delegate(object s, DataReceivedEventArgs e) {
            if (o.Verbose ? e.Data != null : KeepLine(e.Data)) {
                Say(e.Data);
                if (e.Data.StartsWith("OK")) {
                    okLines++;
                    Prog((double)okLines / Math.Max(1, names.Count));
                    Stat(L.T("m_x_stat", okLines + " / " + names.Count));
                }
            }
        };
        p.OutputDataReceived += h;
        p.ErrorDataReceived += h;
        lock (procLock) { blender = p; }
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        while (!p.HasExited) {
            if (Cancel) { try { p.Kill(); } catch (Exception) { } break; }
            Thread.Sleep(150);
        }
        try { p.WaitForExit(2000); } catch (Exception) { }
        lock (procLock) { blender = null; }
        if (Cancel) return;

        List<string> xs = new List<string>();
        foreach (string n in names) {
            string xp = System.IO.Path.Combine(o.Out, n + ".x");
            if (File.Exists(xp)) xs.Add(xp);
        }
        Say(L.T("m_x_done", xs.Count, names.Count, Math.Round((DateTime.Now - t0).TotalSeconds, 1).ToString("0.0")));
        if (o.Mode == "std" && xs.Count > 0) {
            if (!File.Exists(o.Conv)) { Say(L.T("m_conv_skip")); }
            else { O3dStage(o, xs); return; }
        }
        Stat(L.T("m_done"));
        Say(L.T("m_done"));
    }

    // ------------------------------------------------------------ OmsiXConv 병렬 자동화
    void O3dStage(RunOptions o, List<string> xfiles) {
        Queue<string> queue = new Queue<string>();
        List<string> sorted = new List<string>(xfiles);
        sorted.Sort(delegate(string a, string b) {
            long la = 0, lb = 0;
            try { la = new FileInfo(a).Length; } catch (Exception) { }
            try { lb = new FileInfo(b).Length; } catch (Exception) { }
            return lb.CompareTo(la);
        });
        foreach (string x in sorted) queue.Enqueue(x);
        int total = sorted.Count, done = 0, okN = 0;
        Dictionary<string, int> attempts = new Dictionary<string, int>();
        DateTime start = DateTime.Now, lastLaunch = DateTime.MinValue;
        lock (Failed) { Failed.Clear(); }
        if (OnFailedChanged != null) OnFailedChanged();
        int par = Math.Max(1, Math.Min(20, o.Par));
        Prog(0); Stat("OmsiXConv 0 / " + total);
        Say(L.T("m_o3_start", total, par));
        lock (procLock) { active.Clear(); }

        while (queue.Count > 0 || active.Count > 0) {
            if (Cancel) { KillAll(); lock (procLock) { active.Clear(); } return; }
            while (active.Count < par && queue.Count > 0 && (DateTime.Now - lastLaunch).TotalMilliseconds > 700) {
                lastLaunch = DateTime.Now;
                string x = queue.Dequeue();
                Job j = new Job();
                j.X = x; j.Name = System.IO.Path.GetFileNameWithoutExtension(x);
                j.O3d = System.IO.Path.ChangeExtension(x, ".o3d");
                try { j.Length = new FileInfo(x).Length; } catch (Exception) { }
                j.Timeout = 60 + 40 * (j.Length / 1048576.0);
                lock (procLock) { active.Add(j); }
            }
            foreach (Job j in new List<Job>(active)) {
                if (Cancel) break;
                string r;
                try { r = StepJob(j, o); } catch (Exception ex) { r = "fail:" + ex.Message; }
                if (r.StartsWith("retry:")) {
                    int n = 0; attempts.TryGetValue(j.X, out n);
                    attempts[j.X] = n + 1;
                    if (n < 3) {
                        lock (procLock) { active.Remove(j); }
                        queue.Enqueue(j.X);
                        Say(L.T("m_retry", j.Name, r.Substring(6)));
                        continue;
                    }
                    r = "fail:" + r.Substring(6);
                }
                if (r != "run") {
                    lock (procLock) { active.Remove(j); }
                    done++;
                    Prog((double)done / Math.Max(1, total));
                    Stat("OmsiXConv " + done + " / " + total);
                    if (r == "ok") {
                        okN++;
                        Say(L.T("m_o3_ok", done, total, j.Name, j.TSetup.ToString("0.0"), (DateTime.Now - j.T0).TotalSeconds.ToString("0.0")));
                        if (o.DelX) { try { File.Delete(j.X); } catch (Exception) { } }
                    } else {
                        lock (Failed) { Failed.Add(j.X); }
                        if (OnFailedChanged != null) OnFailedChanged();
                        Say(L.T("m_o3_fail", done, total, j.Name, FailText(r.Substring(5))));
                    }
                }
            }
            Thread.Sleep(100);
        }
        Say(L.T("m_o3_end", okN, total, Math.Round((DateTime.Now - start).TotalSeconds, 1).ToString("0.0")));
        if (Failed.Count > 0) Say(L.T("m_failed_hint", Failed.Count));
        Stat(L.T("m_o3_stat", okN, total));
        Say(L.T("m_done"));
    }

    // "f_opt| (zup)" 같은 사유를 현재 언어 문구로
    static string FailText(string r) {
        int i = r.IndexOf('|');
        string key = i >= 0 ? r.Substring(0, i) : r;
        string tail = i >= 0 ? r.Substring(i + 1) : "";
        if (L.Has(key)) return L.T(key) + tail;
        return r;
    }

    static string FailInfo(uint pid) {
        List<string> r = new List<string>();
        foreach (IntPtr h in W.TopAll(pid)) r.Add(W.Cls(h) + "[" + W.Text(h) + "]");
        return string.Join(", ", r.ToArray());
    }

    static bool FillDialog(IntPtr h, string x) {
        IntPtr ed = IntPtr.Zero, okb = IntPtr.Zero, okb2 = IntPtr.Zero;
        foreach (IntPtr k in W.Kids(h)) {
            string cl = W.Cls(k);
            if (cl == "Edit" && ed == IntPtr.Zero) ed = k;
            if (cl.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0) {
                if (W.Id(k) == 1 && okb == IntPtr.Zero) okb = k;
                string t = W.Text(k);
                if ((t.IndexOf("열기") >= 0 || t.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0) && okb2 == IntPtr.Zero) okb2 = k;
            }
        }
        if (okb == IntPtr.Zero) okb = okb2;
        if (okb == IntPtr.Zero) okb = W.Dlg(h, 1);
        if (ed == IntPtr.Zero || okb == IntPtr.Zero) return false;
        W.Hide(h);
        W.SetText(ed, x);
        W.Click(okb);
        return true;
    }

    static bool OptsOk(Job j, RunOptions o) {
        foreach (KeyValuePair<string, IntPtr> kv in j.Checks) {
            int want = 0; o.Copt.TryGetValue(kv.Key, out want);
            if (W.Check(kv.Value) != want) return false;
        }
        return true;
    }

    static void KillJob(Job j) { try { if (j.Proc != null && !j.Proc.HasExited) j.Proc.Kill(); } catch (Exception) { } }

    // OmsiXConv 한 개의 상태를 한 걸음 진행. "run" | "ok" | "fail:..." | "retry:..."
    string StepJob(Job j, RunOptions o) {
        DateTime now = DateTime.Now;
        if (j.Wait > now) return "run";

        if (j.State == "launch") {
            try { if (File.Exists(j.O3d)) File.Delete(j.O3d); } catch (Exception) { }
            j.Start = now; j.T0 = now; j.T00 = now;
            ProcessStartInfo psi = new ProcessStartInfo(o.Conv);
            psi.UseShellExecute = true;
            j.Proc = Process.Start(psi);
            j.State = "findwin";
            return "run";
        }

        uint pid = (uint)j.Proc.Id;
        if (j.Proc.HasExited && j.State != "waito3d") return "fail:f_exited";

        if (j.State == "findwin") {
            foreach (IntPtr h in W.TopAll(pid)) {
                IntPtr c = IntPtr.Zero;
                Dictionary<string, IntPtr> chks = new Dictionary<string, IntPtr>();
                foreach (IntPtr k in W.Kids(h)) {
                    string t = W.Text(k), cl = W.Cls(k);
                    if (t.IndexOf("Convert", StringComparison.OrdinalIgnoreCase) >= 0 && cl.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0) c = k;
                    foreach (string[] pat in OptPat) {
                        if (t.IndexOf(pat[1], StringComparison.OrdinalIgnoreCase) >= 0 && cl.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) < 0) chks[pat[0]] = k;
                    }
                }
                if (c != IntPtr.Zero && chks.ContainsKey("zup")) {
                    j.Conv = c; j.Checks = chks; W.Hide(h);
                    j.State = "setchecks"; j.Wait = now.AddMilliseconds(800);
                    return "run";
                }
            }
            if ((now - j.T0).TotalSeconds > 30) { KillJob(j); return "fail:f_nowin"; }
            return "run";
        }

        if (j.State == "setchecks") {
            foreach (string key in new List<string>(j.Checks.Keys)) {
                int want = 0; o.Copt.TryGetValue(key, out want);
                if (W.Check(j.Checks[key]) != want) {
                    if (j.Tries >= 20) { KillJob(j); return "fail:f_opt| (" + key + ")"; }
                    j.Tries++; j.OkReads = 0;
                    W.Click(j.Checks[key]);
                    j.Wait = now.AddMilliseconds(500);
                    return "run";
                }
            }
            j.OkReads++;
            if (j.OkReads < 3) { j.Wait = now.AddMilliseconds(300); return "run"; }
            W.Click(j.Conv);
            j.State = "finddlg"; j.T0 = now;
            return "run";
        }

        if (j.State == "finddlg") {
            foreach (IntPtr h in W.TopAll(pid)) {
                if (W.Cls(h) != "#32770") continue;
                IntPtr ed = IntPtr.Zero, okb = IntPtr.Zero, okb2 = IntPtr.Zero;
                foreach (IntPtr k in W.Kids(h)) {
                    string cl = W.Cls(k);
                    if (cl == "Edit" && ed == IntPtr.Zero) ed = k;
                    if (cl.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0) {
                        if (W.Id(k) == 1 && okb == IntPtr.Zero) okb = k;
                        string t = W.Text(k);
                        if ((t.IndexOf("열기") >= 0 || t.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0) && okb2 == IntPtr.Zero) okb2 = k;
                    }
                }
                if (okb == IntPtr.Zero) okb = okb2;
                if (okb == IntPtr.Zero) okb = W.Dlg(h, 1);
                if (ed != IntPtr.Zero && okb != IntPtr.Zero) {
                    if (!OptsOk(j, o)) {
                        IntPtr cb = W.Dlg(h, 2);
                        if (cb != IntPtr.Zero) W.Click(cb);
                        j.Tries++; j.OkReads = 0;
                        if (j.Tries >= 20) { KillJob(j); return "fail:f_opt"; }
                        j.State = "setchecks"; j.Wait = now.AddMilliseconds(800);
                        return "run";
                    }
                    W.Hide(h);
                    W.SetText(ed, j.X);
                    j.Wait = now.AddMilliseconds(300);
                    W.Click(okb);
                    j.DlgAt = now;
                    j.TSetup = (now - j.T00).TotalSeconds;
                    j.State = "waito3d"; j.T0 = now;
                    return "run";
                }
            }
            if ((now - j.T0).TotalSeconds > 25) {
                string info = FailInfo(pid);
                KillJob(j);
                return "fail:f_nofile| (" + info + ")";
            }
            return "run";
        }

        if (j.State == "waito3d") {
            foreach (IntPtr h in W.TopAll(pid)) {
                string cls = W.Cls(h);
                if (cls == "#32770") {
                    bool hasEdit = false; string msg = "";
                    foreach (IntPtr k in W.Kids(h)) {
                        string kc = W.Cls(k);
                        if (kc == "Edit") hasEdit = true;
                        if (kc == "Static") msg += W.Text(k) + " ";
                    }
                    if (!hasEdit && Regex.IsMatch(msg, "Fehler|rror|103")) { KillJob(j); return "retry:" + msg.Trim(); }
                    if ((now - j.DlgAt).TotalSeconds > 4 && j.DlgTries < 4) {
                        if (FillDialog(h, j.X)) { j.DlgTries++; j.DlgAt = now; }
                    }
                }
                if (cls.IndexOf("MessageForm", StringComparison.OrdinalIgnoreCase) >= 0) {
                    W.Hide(h);
                    foreach (IntPtr k in W.Kids(h)) { if (W.Cls(k).IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0) { W.Click(k); break; } }
                }
            }
            if (File.Exists(j.O3d)) {
                FileInfo it = new FileInfo(j.O3d);
                if (it.LastWriteTime >= j.Start.AddSeconds(-1)) {
                    if (it.Length > 0 && it.Length == j.Last) j.Stable++; else { j.Stable = 0; j.Last = it.Length; }
                    if (j.Stable >= 3) { KillJob(j); return "ok"; }
                }
            }
            if ((now - j.T0).TotalSeconds > j.Timeout) { KillJob(j); return "fail:f_timeout"; }
            if (j.Proc.HasExited && !File.Exists(j.O3d)) return "fail:f_noo3d";
            return "run";
        }
        return "run";
    }
}
}
