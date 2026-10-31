using System;
using System.Collections.Generic;
using System.IO;

namespace Obj2x {

// 변환을 시작하기 전의 검사와 준비 (경로 확인, 옵션 만들기, 대상 obj 고르기)
public static class Conversion {
    public static string Clean(string p) {
        try { return Environment.ExpandEnvironmentVariables((p ?? "").Trim().Trim('"')); } catch (Exception) { return ""; }
    }

    static bool ExeOk(string path, string name) {
        string p = Clean(path);
        return p.Length > 0 && File.Exists(p) && string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase);
    }
    public static bool BlenderOk(Settings s) { return ExeOk(s.Blender, "blender.exe"); }
    public static bool ConvOk(Settings s) { return ExeOk(s.Conv, "OmsiXConv.exe"); }

    // 없는 도구 목록 ("Blender (blender.exe)" 등). 비어 있으면 모두 있음
    public static List<string> MissingTools(Settings s) {
        List<string> m = new List<string>();
        if (!BlenderOk(s)) m.Add("Blender (blender.exe)");
        if (!ConvOk(s)) m.Add("OmsiXConv (OmsiXConv.exe)");
        return m;
    }

    public static RunOptions BuildOptions(Settings s, string mode, string py) {
        RunOptions o = new RunOptions();
        o.Mode = mode;
        o.In = s.Inp.Trim().TrimEnd('\\');
        o.Out = s.Out.Trim().TrimEnd('\\');
        if (s.SameDir || mode == "fast") o.Out = o.In;
        o.Blender = Clean(s.Blender); o.Conv = Clean(s.Conv); o.Py = py;
        o.Par = s.Par; o.Sub = s.Sub; o.Inc = s.Inc; o.DelX = s.DelX;
        foreach (KeyValuePair<string, int> kv in s.Copt) o.Copt[kv.Key] = kv.Value;
        o.ExOpt = s.ExOpt;
        o.Verbose = s.DevMode && s.DevVerbose;
        return o;
    }

    // 변환 준비. 문제가 있으면 오류 문구 키를 error에 넣고 null을 돌려준다.
    // note에는 '변경된 것만' 선택 결과처럼 로그에 남길 문구가 들어간다.
    public static List<ObjItem> Prepare(Settings s, RunOptions o, out string error, out string note) {
        error = null; note = null;
        if (!Directory.Exists(o.In)) { error = "msg_err_in"; return null; }
        if (o.Out.Length == 0) { error = "msg_err_out"; return null; }
        if (o.Mode != "fast") {
            if (!BlenderOk(s)) { error = "msg_err_bl"; return null; }
            if (!File.Exists(o.Py)) { error = "msg_err_py"; return null; }
            if (o.Mode == "std" && !ConvOk(s)) { error = "msg_err_conv"; return null; }
        }
        try { if (!Directory.Exists(o.Out)) Directory.CreateDirectory(o.Out); } catch (Exception) { }
        List<ObjItem> objs = Engine.FindObjs(o.In, o.Sub);
        if (o.Inc) {
            List<ObjItem> sel = Engine.FilterChanged(objs, o.Out, o.Mode == "x" ? ".x" : ".o3d");
            note = L.T("m_changed", sel.Count, objs.Count);
            objs = sel;
        }
        if (objs.Count == 0) { error = "m_none"; return null; }
        return objs;
    }

    public static string TempJobFile { get { return Path.Combine(Path.GetTempPath(), "BusDepot_job.txt"); } }
    public static string TempLogFile { get { return Path.Combine(Path.GetTempPath(), "BusDepot_log.txt"); } }
}
}
