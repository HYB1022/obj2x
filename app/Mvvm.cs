using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Obj2x.UI;

// 값이 바뀌면 화면에 알리는 기본 클래스
public class Observable : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    protected void Raise(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); Cmd.Requery(); }
    protected void Raise(params string[] names) { foreach (string n in names) Raise(n); }
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); return true;
    }
    // 모든 속성을 다시 읽게 한다 (언어를 바꿨을 때)
    public void RaiseAll() { Raise((string)null); }
}

// WPF의 CommandManager처럼 버튼이 CanExecute를 다시 묻게 한다. 값이 바뀔 때마다 부르지만 한 프레임에 한 번만 알린다.
public class Cmd : ICommand {
    readonly Action<object> run; readonly Func<object, bool> can;
    public Cmd(Action run) : this(delegate (object p) { run(); }, null) { }
    public Cmd(Action<object> run, Func<object, bool> can = null) { this.run = run; this.can = can; }
    static event EventHandler RequeryAll;
    static bool queued;
    public static void Requery() {
        if (queued) return;
        queued = true;
        Dispatcher.UIThread.Post(delegate { queued = false; RequeryAll?.Invoke(null, EventArgs.Empty); }, DispatcherPriority.Background);
    }
    public event EventHandler CanExecuteChanged { add { if (can != null) RequeryAll += value; } remove { if (can != null) RequeryAll -= value; } }
    public bool CanExecute(object p) { return can == null || can(p); }
    public void Execute(object p) { if (CanExecute(p)) run(p); }
}

// 문구표를 XAML에 연결한다. {ui:Tr 키} 로 쓰고, 언어를 바꾸면 화면 글자가 모두 바뀐다.
public class Loc : INotifyPropertyChanged {
    public static readonly Loc I = new Loc();
    public string this[string key] { get { return L.T(key); } }
    public event PropertyChangedEventHandler PropertyChanged;
    public static event Action Changed;
    public static void SetLang(string lang) {
        L.Lang = "ko";
        foreach (string[] l in L.Languages) if (l[0] == lang) L.Lang = lang;
        if (I.PropertyChanged != null) {
            I.PropertyChanged(I, new PropertyChangedEventArgs("Item"));
            I.PropertyChanged(I, new PropertyChangedEventArgs("Item[]"));
        }
        Changed?.Invoke();
    }
}

public class Tr : MarkupExtension {
    public string Key { get; set; }
    public Tr() { }
    public Tr(string key) { Key = key; }
    public override object ProvideValue(IServiceProvider sp) {
        return new Binding("[" + Key + "]") { Source = Loc.I, Mode = BindingMode.OneWay };
    }
}

// 화면 XAML 읽기 (avares://BusDepot/Views/이름.axaml, 컴파일된 XAML)
public static class Xaml {
    public static object Load(string name) {
        if (name.EndsWith(".xaml")) name = name.Substring(0, name.Length - 5) + ".axaml";
        return AvaloniaXamlLoader.Load(new Uri("avares://BusDepot/Views/" + name));
    }
    public static string[] Names() {
        List<string> r = new List<string>();
        foreach (Uri u in AssetLoader.GetAssets(new Uri("avares://BusDepot/Views/"), null))
            if (u.AbsolutePath.EndsWith(".axaml")) r.Add(Path.GetFileName(u.AbsolutePath));
        return r.ToArray();
    }
}

// 화면 트리에서 보이는 T 하나 찾기 (셀프 테스트/캡처)
public static class UiTree {
    public static T Find<T>(Visual root) where T : Visual {
        foreach (Visual c in root.GetVisualChildren()) {
            if (!c.IsVisible) continue;
            if (c is T t) return t;
            T x = Find<T>(c); if (x != null) return x;
        }
        return null;
    }
}

// 셀프 테스트/화면 캡처용: 쌓인 화면 작업을 처리한다
public static class Pump {
    // 플랫폼 메시지 루프를 그 시간만큼 돌린다 (타이머·애니메이션·그리기까지 진행된다)
    public static void Run(int ms) {
        DispatcherFrame frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(Math.Max(1, ms)), DispatcherPriority.Background);
        Dispatcher.UIThread.PushFrame(frame);
    }
}

// 함께 넣은 글꼴 (나눔스퀘어 네오, Noto Sans KR. SIL OFL 1.1): exe 안에 넣어 두고 FontFamily("avares://BusDepot/Assets/Fonts#이름")으로 쓴다.
// MiSans는 라이선스상 넣지 않는다 (사용자가 시스템에 설치한 것만 쓴다)
public static class FileFonts {
    public const string Prefix = "avares://BusDepot/Assets/Fonts#";
    static readonly string[] names = { "NanumSquare Neo", "Noto Sans KR" };
    public static bool Has(string family) { return Array.IndexOf(names, family) >= 0; }

    // 사용자가 넣은 글꼴 폴더 (MiSans처럼 함께 배포할 수 없는 글꼴): Windows는 exe 옆 fonts, 맥은 앱 번들 안과 ~/Library/Application Support/BusDepot/fonts
    public const string DirKey = "fonts:BusDepotDir";
    static DirFontCollection dirFonts;
    public static IEnumerable<string> Dirs() {
        yield return Path.Combine(Program.Here, "fonts");
        if (Platform.Mac) yield return Path.Combine(Platform.DataDir, "fonts");
    }
    public static void Register() {
        try {
            dirFonts = new DirFontCollection(new Uri(DirKey), Dirs());
            FontManager.Current.AddFontCollection(dirFonts);
        } catch (Exception) { dirFonts = null; }
    }
    public static IEnumerable<string> Describe() { return dirFonts != null ? dirFonts.Describe() : new string[0]; }
    // fonts 폴더에서 읽은 글꼴 이름(대소문자 무시)이면 그 이름, 없으면 null
    public static string DirFamily(params string[] want) {
        if (dirFonts == null) return null;
        foreach (string w in want) foreach (string f in dirFonts.Families) if (string.Equals(f, w, StringComparison.OrdinalIgnoreCase)) return f;
        return null;
    }
}

// 폴더의 ttf/otf를 읽는 글꼴 모음. Avalonia의 EmbeddedFontCollection은 avares:// 안의 것만 읽어서 따로 만든다.
// 굵기마다 파일이 따로인 글꼴(MiSans-Regular/Bold/...)은 같은 이름으로 묶고, 고를 때 가장 가까운 굵기를 준다.
public class DirFontCollection : Avalonia.Media.Fonts.FontCollectionBase {
    readonly Uri key; readonly List<string> dirs;
    readonly List<FontFamily> families = new();
    readonly Dictionary<string, List<IGlyphTypeface>> faces = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<IGlyphTypeface, string> styleOf = new();     // 파일 이름의 굵기: MiSans-Regular.ttf → Regular (Regular와 Bold는 글꼴 이름이 둘 다 "MiSans")
    public DirFontCollection(Uri key, IEnumerable<string> dirs) { this.key = key; this.dirs = new List<string>(dirs); }
    public override Uri Key => key;
    public override int Count => families.Count;
    public override FontFamily this[int index] => families[index];
    public override IEnumerator<FontFamily> GetEnumerator() => families.GetEnumerator();
    public IEnumerable<string> Families { get { foreach (string n in faces.Keys) yield return n; } }
    // --fonttest용: 읽은 글꼴 (모음 이름 · 글꼴 이름 · 굵기)
    public IEnumerable<string> Describe() { foreach (var kv in faces) foreach (IGlyphTypeface g in kv.Value) yield return kv.Key + " · " + g.FamilyName + " (" + styleOf.GetValueOrDefault(g) + ") · " + (int)g.Weight + " " + g.Style; }

    public override void Initialize(IFontManagerImpl fontManager) {
        foreach (string d in dirs) {
            string[] files;
            try { if (!Directory.Exists(d)) continue; files = Directory.GetFiles(d); } catch (Exception) { continue; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string f in files) {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".ttf" && ext != ".otf") continue;
                try {
                    using FileStream s = File.OpenRead(f);
                    MemoryStream m = new MemoryStream(); s.CopyTo(m); m.Position = 0;
                    IGlyphTypeface gt = Create(fontManager, m);
                    if (gt == null) continue;
                    // 굵기마다 이름이 다른 글꼴(MiSans Normal, MiSans Bold ...)도 대표 이름(TypographicFamilyName)으로 묶는다
                    string fam = Typographic(gt) ?? gt.FamilyName;
                    string stem = Path.GetFileNameWithoutExtension(f); int dash = stem.LastIndexOfAny(new[] { '-', '_', ' ' });
                    styleOf[gt] = dash >= 0 ? stem.Substring(dash + 1) : "";
                    Add(fam, gt);
                    if (!string.Equals(fam, gt.FamilyName, StringComparison.OrdinalIgnoreCase)) Add(gt.FamilyName, gt);
                } catch (Exception) { }
            }
        }
    }
    // Avalonia 11.2는 스트림에서 글꼴을 만드는 함수(IFontManagerImpl.TryCreateGlyphTypeface(Stream, ...))와 대표 이름(IGlyphTypeface2)을
    // 내부용으로 막아 두었다. EmbeddedFontCollection이 쓰는 바로 그 함수를 리플렉션으로 부른다
    const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    static IGlyphTypeface Create(IFontManagerImpl fm, Stream s) {
        var mi = typeof(IFontManagerImpl).GetMethod("TryCreateGlyphTypeface", Any, null, new[] { typeof(Stream), typeof(FontSimulations), typeof(IGlyphTypeface).MakeByRefType() }, null);
        if (mi == null) return null;
        object[] a = { s, FontSimulations.None, null };
        return mi.Invoke(fm, a) is true ? a[2] as IGlyphTypeface : null;
    }
    static string Typographic(IGlyphTypeface gt) {
        try {
            Type t = typeof(IGlyphTypeface).Assembly.GetType("Avalonia.Media.IGlyphTypeface2");
            string n = t != null && t.IsInstanceOfType(gt) ? t.GetProperty("TypographicFamilyName", Any)?.GetValue(gt) as string : null;
            return string.IsNullOrEmpty(n) ? null : n;
        } catch (Exception) { return null; }
    }
    void Add(string fam, IGlyphTypeface gt) {
        if (!faces.TryGetValue(fam, out List<IGlyphTypeface> l)) { faces[fam] = l = new List<IGlyphTypeface>(); families.Add(new FontFamily(key, fam)); }
        l.Add(gt);
    }

    public override bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, out IGlyphTypeface glyphTypeface) {
        glyphTypeface = null;
        if (!faces.TryGetValue(familyName, out List<IGlyphTypeface> l) || l.Count == 0) return false;
        // MiSans처럼 굵기 숫자가 표준과 다른 글꼴(Regular 330, Bold 630, Heavy 700 ...)이 있어서 굵기 이름을 먼저 본다
        string[] names = (int)weight switch {
            >= 350 and < 450 => new[] { "Regular", "Normal" }, >= 450 and < 550 => new[] { "Medium" },
            >= 550 and < 650 => new[] { "Semibold", "SemiBold", "Demibold" }, >= 650 and < 750 => new[] { "Bold" },
            >= 750 => new[] { "Heavy", "Black", "ExtraBold" }, _ => new string[0] };
        foreach (string n in names) foreach (IGlyphTypeface g in l)
            if (g.Style == style && (string.Equals(styleOf.GetValueOrDefault(g), n, StringComparison.OrdinalIgnoreCase) || g.FamilyName.EndsWith(" " + n, StringComparison.OrdinalIgnoreCase))) { glyphTypeface = g; return true; }
        int best = int.MaxValue;
        foreach (IGlyphTypeface g in l) {
            int score = Math.Abs((int)g.Weight - (int)weight) + (g.Style == style ? 0 : 1000);
            if (score < best) { best = score; glyphTypeface = g; }
        }
        return glyphTypeface != null;
    }
}
