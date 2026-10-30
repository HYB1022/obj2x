using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Obj2x {
// 문구표. 언어마다 표를 하나씩 들고 있고, 현재 언어에 맞는 문구를 돌려준다.
// 언어를 늘리려면: Languages에 한 줄, Fill<언어>(StringsXx.cs), 아래 FillExtra와 TrFast에 그 언어 줄을 넣는다.
public static partial class L {
    public static string Lang = "ko";
    // 지원 언어: { 코드, 그 언어로 쓴 이름 }
    public static readonly string[][] Languages = new string[][] {
        new string[] { "ko", "한국어" }, new string[] { "en", "English" }, new string[] { "de", "Deutsch" } };

    static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
    static Dictionary<string, string> ko, en, de;

    static L() {
        ko = Table("ko"); en = Table("en"); de = Table("de");
        FillKo(ko);
        FillEn(en);
        FillDe(de);
        FillExtra();
    }
    static Dictionary<string, string> Table(string lang) { Dictionary<string, string> d = new Dictionary<string, string>(); tables[lang] = d; return d; }

    // 한 키를 세 언어로 한 번에
    static void X(string key, string k, string e, string g) { ko[key] = k; en[key] = e; de[key] = g; }

    static void FillExtra() {
        X("upd_nofile", "내려받은 파일에서 obj2x.exe 를 찾지 못했습니다.", "Could not find obj2x.exe in the downloaded file.", "obj2x.exe wurde in der heruntergeladenen Datei nicht gefunden.");
        X("wiz_avail_head", "경로 누락/오류: {0}. ", "Missing/invalid: {0}. ", "Fehlend/ungültig: {0}. ");
        X("wiz_avail_noblender", "Blender가 없어 DirectX → O3D와 .X 단독 방식이 비활성화되었습니다. ", "DirectX → O3D and .X-only are disabled. ", "DirectX → O3D und nur .X sind deaktiviert. ");
        X("wiz_avail_noconv", "OmsiXConv가 없어 DirectX → O3D 방식이 비활성화되었습니다. ", "DirectX → O3D is disabled. ", "DirectX → O3D ist deaktiviert. ");
        X("wiz_avail_tail", "설정에서 경로를 지정하세요. FastO3D는 실험적 기능으로 계속 사용할 수 있습니다.", "Set the paths in Settings. FastO3D is experimental and remains available.", "Legen Sie die Pfade in den Einstellungen fest. FastO3D ist experimentell und bleibt verfügbar.");
        X("err_title", "obj2x", "obj2x", "obj2x");
        X("lbl_cores_hint", "이 PC 논리 코어 {0}개", "{0} logical cores on this PC", "{0} logische Kerne auf diesem PC");
        X("m_stopping", "중지하는 중...", "Stopping...", "Wird angehalten...");
        X("m_x_stat", "Blender 변환 중... {0}", "Converting with Blender... {0}", "Konvertierung mit Blender... {0}");
        X("lbl_recent_empty", "아직 선택한 폴더가 없습니다.", "No folders chosen yet.", "Noch keine Ordner ausgewählt.");
        X("sum_yes", "예", "Yes", "Ja");
        X("sum_no", "아니오", "No", "Nein");
        X("sum_mode_std", "DirectX → O3D (권장)", "DirectX → O3D (Recommended)", "DirectX → O3D (empfohlen)");
        X("sum_mode_fast", "FastO3D (실험적)", "FastO3D (experimental)", "FastO3D (experimentell)");
        X("sum_mode_x", "Blender .x 단독", "Blender .x only", "Nur Blender .x");
        X("sum_opt_inc", "변경된 것만", "changed only", "nur geänderte");
        X("sum_opt_sub", "하위 폴더 포함", "subfolders", "Unterordner");
        X("sum_opt_par", "동시 {0}개", "{0} at a time", "{0} gleichzeitig");
        X("sum_opt_none", "기본", "default", "Standard");
        X("upd_prompt",
            "새 버전 v{0} 이(가) 있습니다. (현재 v{1})\r\n\r\n지금 다운로드해서 설치할까요?\r\n설정(obj2x.ini)은 그대로 유지되고, 기존 파일은 obj2x_backup 폴더에 보관됩니다.",
            "Version v{0} is available (current v{1}).\r\n\r\nDownload and install it now?\r\nYour settings (obj2x.ini) are kept and the old files are backed up to the obj2x_backup folder.",
            "Version v{0} ist verfügbar (aktuell v{1}).\r\n\r\nJetzt herunterladen und installieren?\r\nIhre Einstellungen (obj2x.ini) bleiben erhalten, die alten Dateien werden im Ordner obj2x_backup gesichert.");
        X("upd_btn_install", "설치", "Install", "Installieren");
        X("about_pill_update", "업데이트", "Update", "Update");
        X("about_row_version", "버전", "Version", "Version");
        X("about_row_update", "업데이트", "Updates", "Updates");
        X("about_row_repo", "GitHub 저장소", "GitHub repository", "GitHub-Repository");
        X("about_upd_idle", "눌러서 확인", "Click to check", "Zum Prüfen klicken");
        X("about_upd_new", "새 버전 v{0} 설치 가능", "v{0} available - click to install", "v{0} verfügbar – zum Installieren klicken");
        X("about_upd_row_title", "새 버전 v{0}", "Version v{0}", "Version v{0}");
        X("about_upd_row_sub", "현재 v{0} · 설정은 그대로 유지됩니다", "Current v{0} · your settings are kept", "Aktuell v{0} · Ihre Einstellungen bleiben erhalten");
        X("about_upd_installing", "설치 중...", "Installing...", "Wird installiert...");
        X("err_selftest", "자가 검사 실패", "Self-test failed", "Selbsttest fehlgeschlagen");
    }

    // 현재 언어 → 영어 → 한국어 순으로 찾는다 (새 언어에 빠진 문구가 있어도 빈칸이 되지 않게)
    public static string T(string key) {
        string v;
        Dictionary<string, string> d;
        if (tables.TryGetValue(Lang, out d) && d.TryGetValue(key, out v)) return v;
        if (en.TryGetValue(key, out v)) return v;
        if (ko.TryGetValue(key, out v)) return v;
        return key;
    }

    public static string T(string key, params object[] args) {
        string v = T(key);
        try { return string.Format(v, args); } catch (FormatException) { return v; }
    }

    // 모든 언어의 문구표 키가 한국어와 같은지 확인 (다르면 빠진/남는 키 목록)
    public static string Parity() {
        List<string> miss = new List<string>();
        foreach (KeyValuePair<string, Dictionary<string, string>> t in tables) {
            if (t.Value == ko) continue;
            foreach (string k in ko.Keys) { if (!t.Value.ContainsKey(k)) miss.Add(t.Key + ":" + k); }
            foreach (string k in t.Value.Keys) { if (!ko.ContainsKey(k)) miss.Add("ko:" + k + "(" + t.Key + ")"); }
        }
        return string.Join(",", miss.ToArray());
    }
    public static int Count { get { return ko.Count; } }

    public static bool Has(string key) { return ko.ContainsKey(key); }

    // 문구 안의 {0} 같은 자리 표시가 모든 언어에서 같은지 (다르면 키 목록)
    public static string PlaceholderParity() {
        List<string> bad = new List<string>();
        foreach (KeyValuePair<string, Dictionary<string, string>> t in tables) {
            if (t.Value == ko) continue;
            foreach (KeyValuePair<string, string> kv in ko) {
                string o;
                if (!t.Value.TryGetValue(kv.Key, out o)) continue;
                if (Holes(kv.Value) != Holes(o)) bad.Add(t.Key + ":" + kv.Key);
            }
        }
        return string.Join(",", bad.ToArray());
    }
    static string Holes(string s) {
        List<string> h = new List<string>();
        foreach (Match m in Regex.Matches(s, @"\{\d+\}")) if (!h.Contains(m.Value)) h.Add(m.Value);
        h.Sort();
        return string.Join("", h.ToArray());
    }

    // OmsiXConv 실패 사유 키를 현재 언어 문구로
    public static string Fail(string reason) {
        return T(reason);
    }

    // 지원하는 언어 코드인지
    public static bool Supported(string lang) {
        foreach (string[] l in Languages) if (l[0] == lang) return true;
        return false;
    }

    // FastO3D 경고/오류 원문(한국어)을 현재 언어로 바꿔 준다.
    public static string TrFast(string s) {
        if (s == null) return "";
        if (Lang == "en") {
            s = s.Replace("mtl 파일 없음", "mtl file not found");
            s = Regex.Replace(s, "재질 '(.*?)'이 mtl에 없음", "material '$1' is not in the mtl");
            s = Regex.Replace(s, @"오브젝트 (\d+)개가 하나로 합쳐짐", "$1 objects merged into one");
            s = s.Replace("면(f)이 없음", "no faces (f) found");
            s = Regex.Replace(s, @"정점 (\d+)개: 65535개를 넘어 지원하지 않음", "$1 vertices: more than 65535 is not supported");
            s = Regex.Replace(s, @"삼각형 (\d+)개: 65535개를 넘어 지원하지 않음", "$1 triangles: more than 65535 is not supported");
        } else if (Lang == "de") {
            s = s.Replace("mtl 파일 없음", "mtl-Datei nicht gefunden");
            s = Regex.Replace(s, "재질 '(.*?)'이 mtl에 없음", "Material '$1' ist nicht in der mtl");
            s = Regex.Replace(s, @"오브젝트 (\d+)개가 하나로 합쳐짐", "$1 Objekte zu einem zusammengeführt");
            s = s.Replace("면(f)이 없음", "keine Flächen (f) gefunden");
            s = Regex.Replace(s, @"정점 (\d+)개: 65535개를 넘어 지원하지 않음", "$1 Eckpunkte: mehr als 65535 werden nicht unterstützt");
            s = Regex.Replace(s, @"삼각형 (\d+)개: 65535개를 넘어 지원하지 않음", "$1 Dreiecke: mehr als 65535 werden nicht unterstützt");
        }
        return s;
    }
}
}
