using System;
using System.Collections.Generic;
using System.Globalization;

namespace Obj2x {
// 문구표. 한국어/영어 두 벌을 들고 있고, 현재 언어에 맞는 문구를 돌려준다.
public static partial class L {
    public static string Lang = "ko";
    static Dictionary<string, string> ko = new Dictionary<string, string>();
    static Dictionary<string, string> en = new Dictionary<string, string>();

    static L() {
        FillKo(ko);
        FillEn(en);
        FillExtra();
    }

    static void FillExtra() {
        ko["upd_nofile"] = "내려받은 파일에서 obj2x.exe 를 찾지 못했습니다.";
        en["upd_nofile"] = "Could not find obj2x.exe in the downloaded file.";
        ko["wiz_avail_head"] = "경로 누락/오류: {0}. ";
        en["wiz_avail_head"] = "Missing/invalid: {0}. ";
        ko["wiz_avail_noblender"] = "Blender가 없어 DirectX → O3D와 .X 단독 방식이 비활성화되었습니다. ";
        en["wiz_avail_noblender"] = "DirectX → O3D and .X-only are disabled. ";
        ko["wiz_avail_noconv"] = "OmsiXConv가 없어 DirectX → O3D 방식이 비활성화되었습니다. ";
        en["wiz_avail_noconv"] = "DirectX → O3D is disabled. ";
        ko["wiz_avail_tail"] = "설정에서 경로를 지정하세요. FastO3D는 실험적 기능으로 계속 사용할 수 있습니다.";
        en["wiz_avail_tail"] = "Set the paths in Settings. FastO3D is experimental and remains available.";
        ko["err_title"] = "obj2x";
        en["err_title"] = "obj2x";
        ko["lbl_cores_hint"] = "이 PC 논리 코어 {0}개";
        en["lbl_cores_hint"] = "{0} logical cores on this PC";
        ko["m_stopping"] = "중지하는 중...";
        en["m_stopping"] = "Stopping...";
        ko["m_x_stat"] = "Blender 변환 중... {0}";
        en["m_x_stat"] = "Converting with Blender... {0}";
        ko["lbl_recent_empty"] = "아직 선택한 폴더가 없습니다.";
        en["lbl_recent_empty"] = "No folders chosen yet.";
        ko["sum_yes"] = "예";
        en["sum_yes"] = "Yes";
        ko["sum_no"] = "아니오";
        en["sum_no"] = "No";
        ko["sum_mode_std"] = "DirectX → O3D (권장)";
        en["sum_mode_std"] = "DirectX → O3D (Recommended)";
        ko["sum_mode_fast"] = "FastO3D (실험적)";
        en["sum_mode_fast"] = "FastO3D (experimental)";
        ko["sum_mode_x"] = "Blender .x 단독";
        en["sum_mode_x"] = "Blender .x only";
        ko["sum_opt_inc"] = "변경된 것만";
        en["sum_opt_inc"] = "changed only";
        ko["sum_opt_sub"] = "하위 폴더 포함";
        en["sum_opt_sub"] = "subfolders";
        ko["sum_opt_par"] = "동시 {0}개";
        en["sum_opt_par"] = "{0} at a time";
        ko["sum_opt_none"] = "기본";
        en["sum_opt_none"] = "default";
        ko["upd_prompt"] = "새 버전 v{0} 이(가) 있습니다. (현재 v{1})\r\n\r\n지금 다운로드해서 설치할까요?\r\n설정(obj2x.ini)은 그대로 유지되고, 기존 파일은 obj2x_backup 폴더에 보관됩니다.";
        en["upd_prompt"] = "Version v{0} is available (current v{1}).\r\n\r\nDownload and install it now?\r\nYour settings (obj2x.ini) are kept and the old files are backed up to the obj2x_backup folder.";
        ko["upd_btn_install"] = "설치"; en["upd_btn_install"] = "Install";
        ko["about_pill_update"] = "업데이트"; en["about_pill_update"] = "Update";
        ko["about_row_version"] = "버전"; en["about_row_version"] = "Version";
        ko["about_row_update"] = "업데이트"; en["about_row_update"] = "Updates";
        ko["about_row_repo"] = "GitHub 저장소"; en["about_row_repo"] = "GitHub repository";
        ko["about_upd_idle"] = "눌러서 확인"; en["about_upd_idle"] = "Click to check";
        ko["about_upd_new"] = "새 버전 v{0} 설치 가능"; en["about_upd_new"] = "v{0} available - click to install";
        ko["about_upd_row_title"] = "새 버전 v{0}"; en["about_upd_row_title"] = "Version v{0}";
        ko["about_upd_row_sub"] = "현재 v{0} · 설정은 그대로 유지됩니다"; en["about_upd_row_sub"] = "Current v{0} · your settings are kept";
        ko["about_upd_installing"] = "설치 중..."; en["about_upd_installing"] = "Installing...";
        ko["err_selftest"] = "자가 검사 실패";
        en["err_selftest"] = "Self-test failed";
    }

    public static string T(string key) {
        string v;
        Dictionary<string, string> d = (Lang == "en") ? en : ko;
        if (d.TryGetValue(key, out v)) return v;
        if (ko.TryGetValue(key, out v)) return v;
        return key;
    }

    public static string T(string key, params object[] args) {
        string v = T(key);
        try { return string.Format(v, args); } catch (FormatException) { return v; }
    }

    // 한국어/영어 문구표의 키가 서로 같은지 확인 (다르면 빠진 키 목록)
    public static string Parity() {
        List<string> miss = new List<string>();
        foreach (string k in ko.Keys) { if (!en.ContainsKey(k)) miss.Add("en:" + k); }
        foreach (string k in en.Keys) { if (!ko.ContainsKey(k)) miss.Add("ko:" + k); }
        return string.Join(",", miss.ToArray());
    }
    public static int Count { get { return ko.Count; } }

    public static bool Has(string key) { return ko.ContainsKey(key); }

    // OmsiXConv 실패 사유 키를 현재 언어 문구로
    public static string Fail(string reason) {
        return T(reason);
    }

    // FastO3D 경고/오류 원문(한국어)을 영어로 바꿔 준다.
    public static string TrFast(string s) {
        if (s == null) return "";
        if (Lang != "en") return s;
        s = s.Replace("mtl 파일 없음", "mtl file not found");
        s = System.Text.RegularExpressions.Regex.Replace(s, "재질 '(.*?)'이 mtl에 없음", "material '$1' is not in the mtl");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"오브젝트 (\d+)개가 하나로 합쳐짐", "$1 objects merged into one");
        s = s.Replace("면(f)이 없음", "no faces (f) found");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"정점 (\d+)개: 65535개를 넘어 지원하지 않음", "$1 vertices: more than 65535 is not supported");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"삼각형 (\d+)개: 65535개를 넘어 지원하지 않음", "$1 triangles: more than 65535 is not supported");
        return s;
    }
}
}
