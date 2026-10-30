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
    void Toast(string text);          // 화면 아래에 잠깐 뜨는 알림
    string ScaleText { get; }         // 화면 배율 설명 (개발자 옵션 앱 정보)
    void Confirm(string title, string body, string okText, Action<bool> done);   // 확인/취소 대화 상자
    void OpenFolder(string path);     // 탐색기로 폴더 열기
    void Finished(bool failed);       // 변환이 끝남 (창이 비활성이면 작업 표시줄 단추를 깜빡인다)
    void ScrollToEnd(string page);    // 그 페이지를 맨 아래로 (부드럽게)
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

// 번역되는 드롭다운 한 줄 (글꼴, 애니메이션 배율). 언어를 바꿔도 목록은 그대로 두고 이름만 다시 읽게 한다.
// 목록을 새로 만들면 ComboBox가 선택을 잃고 빈 칸이 되기 때문이다.
public class TrItem : Observable {
    readonly Func<string> name;
    public string Code { get; private set; }
    public string Name { get { return name(); } }
    public TrItem(string code, Func<string> name) { Code = code; this.name = name; }
    public void Refresh() { Raise("Name"); }
    public override string ToString() { return Name; }
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
        PyPath = Path.Combine(here, "BusDepot.py");
        NavCmd = new Cmd(delegate(object p) { Page = (string)p; });
        InitDashboard();
        InitWizard();
        InitSettings();
        InitUpdate();
        InitDev();
        InitOmsi();
        InitViewer();
        WireEngine();
        // 저장해 둔 문구(상태, 배너, 마법사 안내)는 새 언어로 다시 만든 뒤 모든 속성을 다시 읽게 한다
        Loc.Changed += delegate {
            foreach (TrItem t in Fonts) t.Refresh();
            foreach (TrItem t in AnimChoices) t.Refresh();
            RefreshStatus(); RaiseAll(); RecentRefresh();
        };
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
            Raise("NotRunning", "StopVisible", "RetryVisible"); RefreshStatus();
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
        Raise("StatusText", "StatusState", "BannerVisible", "BannerText");
        WizAvailability();
    }

    public void SaveAll() { S.Save(); }
}
}
