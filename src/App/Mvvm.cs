using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Obj2x.UI {

// 값이 바뀌면 화면에 알리는 기본 클래스
public class Observable : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    protected void Raise(string name) { PropertyChangedEventHandler h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(name)); }
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); return true;
    }
    // 모든 속성을 다시 읽게 한다 (언어를 바꿨을 때)
    public void RaiseAll() { Raise(null); }
}

public class Cmd : ICommand {
    readonly Action<object> run; readonly Func<object, bool> can;
    // 매개변수 없는 delegate { } 는 첫 번째로, delegate(object p) { } 는 두 번째로 간다
    public Cmd(Action run) : this(delegate(object p) { run(); }, null) { }
    public Cmd(Action<object> run, Func<object, bool> can = null) { this.run = run; this.can = can; }
    public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
    public bool CanExecute(object p) { return can == null || can(p); }
    public void Execute(object p) { run(p); }
}

// 문구표를 XAML에 연결한다. {ui:Tr 키} 로 쓰고, 언어를 바꾸면 화면 글자가 모두 바뀐다.
public class Loc : INotifyPropertyChanged {
    public static readonly Loc I = new Loc();
    public string this[string key] { get { return L.T(key); } }
    public event PropertyChangedEventHandler PropertyChanged;
    public static event Action Changed;
    public static void SetLang(string lang) {
        L.Lang = (lang == "en") ? "en" : "ko";
        if (I.PropertyChanged != null) I.PropertyChanged(I, new PropertyChangedEventArgs("Item[]"));
        if (Changed != null) Changed();
    }
}

public class Tr : MarkupExtension {
    public string Key { get; set; }
    public Tr() { }
    public Tr(string key) { Key = key; }
    public override object ProvideValue(IServiceProvider sp) {
        Binding b = new Binding("[" + Key + "]"); b.Source = Loc.I; b.Mode = BindingMode.OneWay;
        return b.ProvideValue(sp);
    }
}

// exe 안에 넣어 둔 XAML 화면을 읽는다 (Obj2x.Views.이름.xaml)
public static class Xaml {
    public static object Load(string name) {
        using (Stream s = typeof(Xaml).Assembly.GetManifestResourceStream("Obj2x.Views." + name)) {
            if (s == null) throw new FileNotFoundException("XAML resource not found: " + name);
            return XamlReader.Load(s);
        }
    }
    public static string[] Names() {
        List<string> r = new List<string>();
        foreach (string n in typeof(Xaml).Assembly.GetManifestResourceNames()) if (n.StartsWith("Obj2x.Views.")) r.Add(n.Substring(12));
        return r.ToArray();
    }
}

// 셀프 테스트/화면 캡처용: 쌓인 화면 작업을 처리한다
public static class Pump {
    public static void Run(int ms) {
        DateTime end = DateTime.Now.AddMilliseconds(ms);
        do {
            DispatcherFrame f = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { f.Continue = false; }));
            Dispatcher.PushFrame(f);
            if (DateTime.Now < end) System.Threading.Thread.Sleep(10);
        } while (DateTime.Now < end);
    }
}
}
