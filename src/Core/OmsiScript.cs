using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Obj2x {

// OMSI 차량 스크립트(.osc)를 아주 간단하게 실행해서 [visible] 변수 값을 구한다 (모델 보기용).
// 도색(.cti)의 [setvar] 값을 넣고 모든 스크립트의 {init}, 그다음 {frame}을 한 번 돌린다.
// 계산(+ - * / 비교 && || !), {if}{else}{endif}, 매크로, 지역 변수(L.L/S.L), 상수(C.L), 레지스터(s0~7/l0~7)만 다루고,
// 시스템 값(L.S, M.V 등)이나 모르는 낱말은 0으로 본다. 게임과 똑같지는 않지만 '연식에 따라 앞문 모양' 같은 표시 옵션은 맞는다.
public class OmsiScript {
    readonly Dictionary<string, List<string>> macros = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    readonly List<List<string>> inits = new List<List<string>>(), frames = new List<List<string>>();
    readonly Dictionary<string, double> consts = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> Vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // 스크립트가 값을 넣은 변수
    readonly double[] reg = new double[8];
    long steps;
    const long MaxSteps = 5000000;

    // cfg를 [model]로 쓰는 .bus/.ovh를 차량 폴더에서 찾아 그 [script], [constfile]을 읽는다. 없으면 null
    public static OmsiScript ForModel(string vehicleDir, string cfgPath) {
        string[] files;
        try { files = Directory.GetFiles(vehicleDir, "*.bus"); } catch (Exception) { return null; }
        try { string[] ovh = Directory.GetFiles(vehicleDir, "*.ovh"); if (ovh.Length > 0) { List<string> all = new List<string>(files); all.AddRange(ovh); files = all.ToArray(); } } catch (Exception) { }
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        string want = Path.GetFullPath(cfgPath);
        foreach (string bus in files) {
            string[] lines;
            try { lines = Omsi.AnsiLines(bus); } catch (Exception) { continue; }
            bool match = false;
            for (int i = 0; i + 1 < lines.Length; i++) {
                if (lines[i].Trim().ToLowerInvariant() != "[model]") continue;
                try { match = string.Equals(Path.GetFullPath(Path.Combine(vehicleDir, lines[i + 1].Trim())), want, StringComparison.OrdinalIgnoreCase); } catch (Exception) { }
                break;
            }
            if (!match) continue;
            OmsiScript s = new OmsiScript();
            foreach (string f in List(lines, "[script]")) s.LoadOsc(Path.Combine(vehicleDir, f));
            foreach (string f in List(lines, "[constfile]")) s.LoadConsts(Path.Combine(vehicleDir, f));
            return s;
        }
        return null;
    }

    // [키] 다음 줄의 개수만큼 파일 이름
    static List<string> List(string[] lines, string key) {
        List<string> r = new List<string>();
        for (int i = 0; i < lines.Length; i++) {
            if (lines[i].Trim().ToLowerInvariant() != key) continue;
            int n; if (i + 1 >= lines.Length || !int.TryParse(lines[i + 1].Trim(), out n)) continue;
            for (int k = 0; k < n && i + 2 + k < lines.Length; k++) { string f = lines[i + 2 + k].Trim(); if (f.Length > 0) r.Add(f); }
        }
        return r;
    }

    void LoadConsts(string path) {
        string[] l; try { l = Omsi.AnsiLines(path); } catch (Exception) { return; }
        for (int i = 0; i + 2 < l.Length; i++) {
            if (l[i].Trim().ToLowerInvariant() != "[const]") continue;
            double v; if (double.TryParse(l[i + 2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) consts[l[i + 1].Trim()] = v;
        }
    }

    // {init} / {frame} / {macro:이름} ... {end}. 작은따옴표 뒤는 주석
    void LoadOsc(string path) {
        string[] l; try { l = Omsi.AnsiLines(path); } catch (Exception) { return; }
        List<string> cur = null;
        foreach (string raw in l) {
            string line = raw;
            int q = line.IndexOf('\''); if (q >= 0) line = line.Substring(0, q);
            foreach (string tok in Regex.Split(line.Trim(), @"\s+")) {
                if (tok.Length == 0) continue;
                string t = tok.ToLowerInvariant();
                if (t == "{init}") { cur = new List<string>(); inits.Add(cur); continue; }
                if (t == "{frame}") { cur = new List<string>(); frames.Add(cur); continue; }
                if (t.StartsWith("{macro:")) { cur = new List<string>(); macros[tok.Substring(7).TrimEnd('}')] = cur; continue; }
                if (t.StartsWith("{trigger:") || t.StartsWith("{frame_ai}") || t.StartsWith("{init_ai}")) { cur = null; continue; }
                if (t == "{end}") { cur = null; continue; }
                if (cur != null) cur.Add(tok);
            }
        }
    }

    // 도색 변수로 시작해서 {init} → {frame} 두 번 (frame에서 정한 값을 다른 계산이 다시 쓰는 경우)
    public void Run(Dictionary<string, string> setvars) {
        Vars.Clear(); Assigned.Clear(); steps = 0;
        if (setvars != null) foreach (KeyValuePair<string, string> kv in setvars) { double v; if (double.TryParse(kv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Vars[kv.Key] = v; }
        try {
            foreach (List<string> b in inits) Exec(b, 0);
            for (int k = 0; k < 2; k++) foreach (List<string> b in frames) Exec(b, 0);
        } catch (Exception) { }
    }

    double Get(string v) { double x; return Vars.TryGetValue(v, out x) ? x : 0; }

    void Exec(List<string> code, int depth) {
        if (depth > 40) return;
        List<double> st = new List<double>();
        Func<double> pop = delegate { if (st.Count == 0) return 0; double v = st[st.Count - 1]; st.RemoveAt(st.Count - 1); return v; };
        Func<double> top = delegate { return st.Count == 0 ? 0 : st[st.Count - 1]; };
        for (int i = 0; i < code.Count; i++) {
            if (++steps > MaxSteps) throw new Exception("too long");
            string t = code[i], lt = t.ToLowerInvariant();
            if (lt == "{if}") {
                if (pop() != 0) continue;
                i = Skip(code, i, true);   // else가 있으면 else 다음으로, 없으면 endif로
                continue;
            }
            if (lt == "{else}") { i = Skip(code, i, false); continue; }   // if 쪽을 다 했으면 endif로
            if (lt == "{endif}") continue;
            double num;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out num)) { st.Add(num); continue; }
            if (t.StartsWith("(") && t.EndsWith(")")) {
                string inner = t.Substring(1, t.Length - 2), li = inner.ToLowerInvariant();
                if (li.StartsWith("l.l.")) { st.Add(Get(inner.Substring(4))); continue; }
                if (li.StartsWith("s.l.")) { string n = inner.Substring(4); Vars[n] = top(); Assigned.Add(n); continue; }
                if (li.StartsWith("c.l.")) { double c; st.Add(consts.TryGetValue(inner.Substring(4), out c) ? c : 0); continue; }
                if (li.StartsWith("m.l.")) { List<string> m; if (macros.TryGetValue(inner.Substring(4), out m)) Exec(m, depth + 1); continue; }
                if (li.Length == 2 && li[0] == 's' && char.IsDigit(li[1])) { reg[(li[1] - '0') & 7] = top(); continue; }
                if (li.Length == 2 && li[0] == 'l' && char.IsDigit(li[1])) { st.Add(reg[(li[1] - '0') & 7]); continue; }
                if (li.StartsWith("s.")) continue;          // 다른 저장(S.S 문자열, S.G 전역 등): 무시
                if (li.StartsWith("t.")) continue;          // 트리거/소리: 무시
                st.Add(0);                                  // 시스템 값(L.S, M.V, L.V ...): 0
                continue;
            }
            if (t.StartsWith("\"")) { st.Add(0); continue; }   // 문자열
            double b, a;
            switch (lt) {
                case "+": b = pop(); a = pop(); st.Add(a + b); break;
                case "-": b = pop(); a = pop(); st.Add(a - b); break;
                case "*": b = pop(); a = pop(); st.Add(a * b); break;
                case "/": b = pop(); a = pop(); st.Add(b == 0 ? 0 : a / b); break;
                case "%": b = pop(); a = pop(); st.Add(b == 0 ? 0 : a % b); break;
                case "<": b = pop(); a = pop(); st.Add(a < b ? 1 : 0); break;
                case ">": b = pop(); a = pop(); st.Add(a > b ? 1 : 0); break;
                case "<=": b = pop(); a = pop(); st.Add(a <= b ? 1 : 0); break;
                case ">=": b = pop(); a = pop(); st.Add(a >= b ? 1 : 0); break;
                case "=": b = pop(); a = pop(); st.Add(a == b ? 1 : 0); break;
                case "&&": b = pop(); a = pop(); st.Add(a != 0 && b != 0 ? 1 : 0); break;
                case "||": b = pop(); a = pop(); st.Add(a != 0 || b != 0 ? 1 : 0); break;
                case "!": a = pop(); st.Add(a == 0 ? 1 : 0); break;
                case "min": b = pop(); a = pop(); st.Add(Math.Min(a, b)); break;
                case "max": b = pop(); a = pop(); st.Add(Math.Max(a, b)); break;
                case "abs": st.Add(Math.Abs(pop())); break;
                case "trunc": st.Add(Math.Truncate(pop())); break;
                case "round": st.Add(Math.Round(pop())); break;
                case "d": st.Add(top()); break;
                case "$=": pop(); pop(); st.Add(0); break;
                default: break;                              // 모르는 낱말: 무시
            }
        }
    }

    // {if}가 거짓이면: 짝이 맞는 {else} 다음 또는 {endif}로. {else}에 닿으면(참 쪽을 다 함): 짝이 맞는 {endif}로
    static int Skip(List<string> code, int i, bool toElse) {
        int depth = 0;
        for (int k = i + 1; k < code.Count; k++) {
            string t = code[k].ToLowerInvariant();
            if (t == "{if}") depth++;
            else if (t == "{endif}") { if (depth == 0) return k; depth--; }
            else if (t == "{else}" && depth == 0 && toElse) return k;
        }
        return code.Count;
    }
}
}
