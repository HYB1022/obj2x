using System;
using System.Collections.Generic;
using System.IO;

namespace Obj2x {

// 변환할 obj 목록과 결과 줄. Engine(OmsiXConv 창 조작, Windows 전용)과 떼어 두어 맥 버전도 쓴다
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

public static class ObjList {
    public static List<ObjItem> Find(string dir, bool sub) {
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
}
}
