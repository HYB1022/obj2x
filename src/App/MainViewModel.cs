using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace Obj2x.UI {

// ViewModel이 화면에 부탁하는 일 (대화 상자, 파일 선택 등). MainWindow가 구현한다.
public interface IUi {
    void Msg(string text);
    void Disclaimer(bool review, Action<bool> done);
    string BrowseFolder(string start);
    string PickFile(string title, string filter, string start);
    string SaveFile(string name, string filter);
    void Post(Action a);              // 화면 스레드에서 실행
    void ApplyLook();                 // 테마/글꼴이 바뀜
    void Exit();
}

// 결과 표 한 줄
public class ResItem {
    public string Name { get; set; }
    public string V { get; set; }
    public string T { get; set; }
    public string M { get; set; }
    public string Sec { get; set; }
    public string Status { get; set; }
    public int Kind { get; set; }    // 0 ok, 1 warn, 2 fail
}

// 최근 폴더 한 줄
public class RecentItem {
    public string Path { get; set; }
    public bool Exists { get; set; }
    public string Missing { get { return L.T("recent_missing"); } }
}

// 언어 드롭다운 한 줄
public class LangItem {
    public string Code { get; private set; }
    public string Name { get; private set; }
    public LangItem(string code, string name) { Code = code; Name = name; }
    public override string ToString() { return Name; }   // 드롭다운 닫힌 상자에 보이는 글자
}

// 고급: OmsiXConv 체크박스 한 개
public class CoptItem : Observable {
    readonly Settings s; readonly string key;
    public string Name { get; private set; }
    public CoptItem(Settings s, string key, string name) { this.s = s; this.key = key; Name = name; }
    public bool Checked { get { return s.Copt[key] == 1; } set { s.Copt[key] = value ? 1 : 0; Raise("Checked"); } }
}

// 메인 화면의 상태와 동작. 화면 컨트롤은 모르고, 속성/명령만 내놓는다.
public partial class MainViewModel : Observable {
    public readonly Settings S;
    public readonly Engine E = new Engine();
    public readonly string Here, PyPath;
    readonly IUi ui;

    public event Action<string> PageChanged;
    public event Action<string> LogAppended;
    public event Action LogCleared;

    public MainViewModel(Settings s, string here, IUi ui) {
        S = s; Here = here; this.ui = ui;
        PyPath = Path.Combine(here, "obj2x.py");
        NavCmd = new Cmd(delegate(object p) { Page = (string)p; });
        InitDashboard();
        InitWizard();
        InitSettings();
        InitUpdate();
        WireEngine();
        Loc.Changed += delegate { RaiseAll(); RecentRefresh(); };
        RefreshStatus();
    }

    // ------------------------------------------------------------ 페이지
    string page = "dashboard";
    public ICommand NavCmd { get; private set; }
    public string Page {
        get { return page; }
        set {
            if (value == "wizard") WizEnter();
            if (Set(ref page, value) || value == "wizard") { if (PageChanged != null) PageChanged(value); }
        }
    }

    // ------------------------------------------------------------ 상태 (상태줄, 대시보드 상태 카드, 경고 배너)
    bool running, stopping;
    public bool Running {
        get { return running; }
        private set {
            if (!Set(ref running, value)) return;
            Raise("NotRunning"); Raise("StopVisible"); Raise("RetryVisible"); RefreshStatus();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool NotRunning { get { return !running; } }
    public string StatusText { get; private set; }
    public string StatusState { get; private set; }     // ok | warn | busy
    public bool BannerVisible { get; private set; }
    public string BannerText { get; private set; }
    public string CoresText { get { return L.T("lbl_cores", Environment.ProcessorCount); } }
    public string VersionText { get { return "v" + AppInfo.Version; } }

    public void RefreshStatus() {
        List<string> missing = Conversion.MissingTools(S);
        bool ok = missing.Count == 0;
        StatusText = running ? L.T("status_converting") : (ok ? L.T("status_ready") : L.T("status_blender_missing"));
        StatusState = running ? "busy" : (ok ? "ok" : "warn");
        BannerVisible = !ok;
        BannerText = ok ? "" : L.T("m_path_banner", string.Join(", ", missing.ToArray()));
        Raise("StatusText"); Raise("StatusState"); Raise("BannerVisible"); Raise("BannerText");
        WizAvailability();
    }

    public void SaveAll() { S.Save(); }
}
}
