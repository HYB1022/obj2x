using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Obj2x {
// 문구표. 언어마다 JSON 파일 하나(키 → 문구, "_name"은 그 언어로 쓴 언어 이름).
//  1) 기본 언어는 저장소의 lang/*.json을 exe에 넣어 둔 것 (리소스 Obj2x.Lang.<코드>.json)
//  2) exe 옆 lang 폴더의 <코드>.json을 그 위에 덮어쓴다. 새 코드면 언어가 추가되고, 있는 코드면 그 문구만 바뀐다.
// 빠진 문구는 영어 → 한국어 순으로 대신 보여 준다.
public static class L {
    public static string Lang = "ko";
    // { 코드, 그 언어로 쓴 이름 } — 기본 언어가 먼저, 그다음 사용자 언어(코드 순)
    public static string[][] Languages { get; private set; }
    // 읽지 못한 사용자 언어 파일과 이유
    public static readonly List<string> LoadErrors = new List<string>();
    public static string UserDir { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang"); } }
    static readonly string[] BuiltinOrder = new string[] { "ko", "en", "de", "ja", "zh" };

    static readonly Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>();
    static readonly Dictionary<string, string> names = new Dictionary<string, string>();
    static readonly List<string> builtin = new List<string>();
    static Dictionary<string, string> ko = new Dictionary<string, string>(), en = new Dictionary<string, string>();

    static L() { Load(); }

    public static void Load() {
        tables.Clear(); names.Clear(); builtin.Clear(); LoadErrors.Clear();
        System.Reflection.Assembly asm = typeof(L).Assembly;
        foreach (string res in asm.GetManifestResourceNames()) {
            if (!res.StartsWith("Obj2x.Lang.") || !res.EndsWith(".json")) continue;
            string code = res.Substring(11, res.Length - 16);
            if (code.StartsWith("_")) continue;              // _template 같은 것은 언어가 아니다
            using (Stream s = asm.GetManifestResourceStream(res))
            using (StreamReader r = new StreamReader(s, Encoding.UTF8)) Merge(code, Json.ParseObject(r.ReadToEnd()));
            builtin.Add(code);
        }
        try {
            if (Directory.Exists(UserDir)) {
                string[] files = Directory.GetFiles(UserDir, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files) {
                    string code = Path.GetFileNameWithoutExtension(f);
                    if (code.StartsWith("_")) continue;          // _template.json 같은 것은 건너뛴다
                    try { Merge(code.ToLowerInvariant(), Json.ParseObject(File.ReadAllText(f, Encoding.UTF8))); }
                    catch (Exception ex) { LoadErrors.Add(Path.GetFileName(f) + ": " + ex.Message); }
                }
            }
        } catch (Exception ex) { LoadErrors.Add(ex.Message); }
        if (!tables.ContainsKey("ko")) tables["ko"] = new Dictionary<string, string>();
        if (!tables.ContainsKey("en")) tables["en"] = new Dictionary<string, string>();
        ko = tables["ko"]; en = tables["en"];

        List<string> codes = new List<string>(tables.Keys);
        codes.Sort(delegate(string a, string b) {
            int ia = Array.IndexOf(BuiltinOrder, a), ib = Array.IndexOf(BuiltinOrder, b);
            if (ia < 0) ia = 100; if (ib < 0) ib = 100;
            return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a, b);
        });
        List<string[]> list = new List<string[]>();
        foreach (string c in codes) { string n; list.Add(new string[] { c, names.TryGetValue(c, out n) && n.Length > 0 ? n : c }); }
        Languages = list.ToArray();
        if (!Supported(Lang)) Lang = "ko";
    }

    static void Merge(string code, Dictionary<string, string> d) {
        Dictionary<string, string> t;
        if (!tables.TryGetValue(code, out t)) { t = new Dictionary<string, string>(); tables[code] = t; }
        foreach (KeyValuePair<string, string> kv in d) {
            if (kv.Key == "_name") names[code] = kv.Value;
            else if (!kv.Key.StartsWith("_")) t[kv.Key] = kv.Value;
        }
    }

    // 현재 언어 → 영어 → 한국어 순으로 찾는다
    public static string T(string key) {
        string v;
        Dictionary<string, string> d;
        if (tables.TryGetValue(Lang, out d) && d.TryGetValue(key, out v)) return v;
        if (en.TryGetValue(key, out v)) return v;
        if (ko.TryGetValue(key, out v)) return v;
        return key;
    }

    // 그 언어 파일에 있는 문구만 (없으면 null, 다른 언어로 대신하지 않는다)
    public static string TIn(string lang, string key) {
        Dictionary<string, string> d; string v;
        return tables.TryGetValue(lang, out d) && d.TryGetValue(key, out v) ? v : null;
    }

    public static string T(string key, params object[] args) {
        string v = T(key);
        try { return string.Format(v, args); } catch (FormatException) { return v; }
    }

    // 기본 언어들의 키가 한국어와 같은지 (다르면 빠진/남는 키 목록). 사용자 언어 파일은 일부만 있어도 된다.
    public static string Parity() {
        List<string> miss = new List<string>();
        foreach (string code in builtin) {
            Dictionary<string, string> t = tables[code];
            if (t == ko) continue;
            foreach (string k in ko.Keys) { if (!t.ContainsKey(k)) miss.Add(code + ":" + k); }
            foreach (string k in t.Keys) { if (!ko.ContainsKey(k)) miss.Add("ko:" + k + "(" + code + ")"); }
        }
        return string.Join(",", miss.ToArray());
    }
    public static int Count { get { return ko.Count; } }
    public static int BuiltinCount { get { return builtin.Count; } }
    public static bool Has(string key) { return ko.ContainsKey(key); }

    // 문구 안의 {0} 같은 자리 표시가 모든 언어(사용자 파일 포함)에서 한국어와 같은지 (다르면 키 목록)
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

    public static bool Supported(string lang) { return lang != null && tables.ContainsKey(lang); }

    // 번역용 틀: lang 폴더에 _template.json(영어 문구 + 설명)을 만든다. 만든(또는 이미 있는) 폴더를 돌려준다.
    public static string WriteTemplate() {
        Directory.CreateDirectory(UserDir);
        List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
        items.Add(new KeyValuePair<string, string>("_name", "Language name in that language (e.g. Français)"));
        items.Add(new KeyValuePair<string, string>("_help", "Copy this file as <code>.json (e.g. fr.json), translate the values, keep {0} {1} placeholders, then restart obj2x. Missing keys fall back to English."));
        foreach (string k in ko.Keys) { string v; items.Add(new KeyValuePair<string, string>(k, en.TryGetValue(k, out v) ? v : ko[k])); }
        File.WriteAllText(Path.Combine(UserDir, "_template.json"), Json.WriteObject(items), new UTF8Encoding(false));
        return UserDir;
    }
}

// 문구표용 아주 작은 JSON: 문자열 값만 있는 평평한 객체 하나를 읽고 쓴다
public static class Json {
    public static Dictionary<string, string> ParseObject(string s) {
        Dictionary<string, string> d = new Dictionary<string, string>();
        int i = 0;
        Ws(s, ref i);
        if (i < s.Length && s[i] == '﻿') { i++; Ws(s, ref i); }
        Expect(s, ref i, '{');
        Ws(s, ref i);
        if (i < s.Length && s[i] == '}') return d;
        while (true) {
            Ws(s, ref i);
            string k = Str(s, ref i);
            Ws(s, ref i); Expect(s, ref i, ':'); Ws(s, ref i);
            d[k] = Str(s, ref i);
            Ws(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; Ws(s, ref i); if (i < s.Length && s[i] == '}') { i++; break; } continue; }
            Expect(s, ref i, '}');
            break;
        }
        return d;
    }
    static void Ws(string s, ref int i) { while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '﻿')) i++; }
    static void Expect(string s, ref int i, char c) {
        if (i >= s.Length || s[i] != c) throw new FormatException(string.Format("'{0}' expected at {1}", c, Pos(s, i)));
        i++;
    }
    static string Pos(string s, int i) {
        int line = 1, col = 1;
        for (int k = 0; k < i && k < s.Length; k++) { if (s[k] == '\n') { line++; col = 1; } else col++; }
        return "line " + line + ", column " + col;
    }
    static string Str(string s, ref int i) {
        Expect(s, ref i, '"');
        StringBuilder b = new StringBuilder();
        while (true) {
            if (i >= s.Length) throw new FormatException("unterminated string at " + Pos(s, i));
            char c = s[i++];
            if (c == '"') break;
            if (c != '\\') { b.Append(c); continue; }
            if (i >= s.Length) throw new FormatException("bad escape at " + Pos(s, i));
            char e = s[i++];
            switch (e) {
                case 'n': b.Append("\r\n"); break;           // 화면에서는 Windows 줄바꿈
                case 't': b.Append('\t'); break;
                case 'r': break;
                case 'b': b.Append('\b'); break;
                case 'f': b.Append('\f'); break;
                case 'u': b.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: b.Append(e); break;                 // \" \\ \/
            }
        }
        return b.ToString();
    }
    public static string WriteObject(List<KeyValuePair<string, string>> items) {
        StringBuilder sb = new StringBuilder("{\n");
        for (int i = 0; i < items.Count; i++) {
            sb.Append("  \"").Append(Esc(items[i].Key)).Append("\": \"").Append(Esc(items[i].Value)).Append('"');
            sb.Append(i < items.Count - 1 ? ",\n" : "\n");
        }
        return sb.Append("}\n").ToString();
    }
    static string Esc(string s) {
        StringBuilder b = new StringBuilder();
        foreach (char c in s) {
            switch (c) {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': break;
                case '\t': b.Append("\\t"); break;
                default: if (c < 0x20) b.AppendFormat("\\u{0:x4}", (int)c); else b.Append(c); break;
            }
        }
        return b.ToString();
    }
}
}
