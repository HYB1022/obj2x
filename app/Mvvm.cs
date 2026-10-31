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
    public static void Register() { }
}
