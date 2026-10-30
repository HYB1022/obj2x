using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Obj2x.UI {

// 메인 창: Shell.xaml과 페이지 XAML을 읽어 붙이고, ViewModel이 부탁하는 화면 일(IUi)을 맡는다.
public class MainWindow : Window, IUi {
    public readonly MainViewModel VM;
    readonly Settings S;
    readonly bool upgrade;
    readonly Grid host, pageHost, overlay;
    readonly FrameworkElement shell;
    readonly Canvas pillLayer;
    readonly Border pill;
    readonly TextBox logBox;
    readonly Dictionary<string, FrameworkElement> pages = new Dictionary<string, FrameworkElement>();
    readonly ScaleTransform zoom = new ScaleTransform(1, 1);
    string shown = "dashboard";
    public DialogHost LastDialog;
    public static readonly string[] PageNames = new string[] { "dashboard", "wizard", "advanced", "settings", "about" };

    public MainWindow(Settings s, string here, bool upgrade) {
        S = s; this.upgrade = upgrade;
        Title = L.T("app_title");
        Icon = Art.AppIcon();
        Width = 1056; Height = 725; MinWidth = 920; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Maximized;
        UseLayoutRounding = true;

        host = new Grid(); host.SetResourceReference(Panel.BackgroundProperty, "Bg");
        shell = (FrameworkElement)Xaml.Load("Shell.xaml");
        shell.LayoutTransform = zoom;
        host.Children.Add(shell);
        Content = host;
        pageHost = (Grid)shell.FindName("PageHost");
        overlay = (Grid)shell.FindName("Overlay");
        pillLayer = (Canvas)shell.FindName("PillLayer");
        pill = (Border)shell.FindName("NavPill");

        VM = new MainViewModel(s, here, this);
        DataContext = VM;
        // 대시보드만 바로 만들고, 다른 페이지는 처음 열 때 만든다 (시작이 빨라진다)
        logBox = (TextBox)Page("dashboard").FindName("LogBox");
        pages["dashboard"].Visibility = Visibility.Visible;

        VM.PageChanged += ShowPage;
        VM.LogAppended += delegate(string chunk) { logBox.AppendText(chunk); logBox.ScrollToEnd(); };
        VM.LogCleared += delegate { logBox.Clear(); };
        Loc.Changed += delegate { Title = L.T("app_title"); };
        SizeChanged += delegate { UpdateZoom(); };
        SourceInitialized += delegate { Theme.TitleBar(this); };
        Loaded += delegate {
            UpdateZoom();
            MovePill(shown, false);
            PreloadPages();
            if (S.UpdCheck) VM.StartUpdateCheck();
            if (this.upgrade && S.NeedDisclaimer()) Disclaimer(false, null);
        };
        Closing += delegate { VM.SaveAll(); VM.E.Stop(); };
    }

    // ------------------------------------------------------------ 화면 배율
    // 기준 크기(1040x686)보다 창이 크면 글자와 여백을 함께 키운다. 0.1 단위로 최대 1.3배.
    void UpdateZoom() {
        double w = host.ActualWidth, h = host.ActualHeight;
        if (w < 200 || h < 200) return;
        double z = Math.Floor(Math.Min(w / 1040, h / 686) * 10 + 0.001) / 10;
        z = Math.Max(1, Math.Min(1.3, z));
        if (Math.Abs(z - zoom.ScaleX) < 0.01) return;
        zoom.ScaleX = zoom.ScaleY = z;
        Dispatcher.BeginInvoke(new Action(delegate { MovePill(shown, false); }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // 셀프 테스트/화면 캡처: 창 안쪽 크기를 정해 둔다 (창이 그 크기에 맞춰진다)
    public void FixClientSize(double w, double h) {
        WindowState = WindowState.Normal;
        host.Width = w; host.Height = h;
        SizeToContent = SizeToContent.WidthAndHeight;
        UpdateLayout(); UpdateZoom();
    }
    public FrameworkElement Host { get { return host; } }
    public string LogText { get { return logBox.Text; } }     // 셀프 테스트용

    FrameworkElement Page(string name) {
        FrameworkElement p;
        if (pages.TryGetValue(name, out p)) return p;
        p = (FrameworkElement)Xaml.Load(char.ToUpper(name[0]) + name.Substring(1) + ".xaml");
        // Collapsed가 아니라 Hidden: 숨겨 둔 동안에도 크기 계산이 유지되어 다시 보일 때 레이아웃을 새로 하지 않는다
        p.Visibility = Visibility.Hidden;
        p.RenderTransform = new TranslateTransform();
        pages[name] = p; pageHost.Children.Add(p);
        return p;
    }

    // 시작한 뒤 한가할 때 나머지 페이지를 하나씩 미리 만들어 둔다 (처음 열 때 XAML을 읽느라 끊기지 않게)
    void PreloadPages() {
        foreach (string n in PageNames) {
            if (pages.ContainsKey(n)) continue;
            string name = n;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { Page(name); }));
        }
    }
    public int OverlayCount { get { return overlay.Children.Count; } }

    // ------------------------------------------------------------ 페이지 전환 (새 화면이 살짝 떠오르며 나타남)
    void ShowPage(string name) {
        if (Array.IndexOf(PageNames, name) < 0) return;
        FrameworkElement np = Page(name);
        if (name != shown) {
            pages[shown].Visibility = Visibility.Hidden;
            np.Visibility = Visibility.Visible;
            if (!Motion.Off) {
                // 전환하는 동안만 페이지를 비트맵 한 장으로 굳혀서 그래픽카드가 옮기게 한다 (글자/상자를 매 프레임 다시 그리지 않음)
                BitmapCache cache = new BitmapCache(zoom.ScaleX); cache.SnapsToDevicePixels = true;
                np.CacheMode = cache;
                FrameworkElement page = np;
                DoubleAnimation fade = new DoubleAnimation(0, 1, Motion.D(0.24));
                DoubleAnimation rise = new DoubleAnimation(14, 0, Motion.D(0.24));
                rise.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                rise.Completed += delegate { page.CacheMode = null; };
                np.BeginAnimation(OpacityProperty, fade);
                ((TranslateTransform)np.RenderTransform).BeginAnimation(TranslateTransform.YProperty, rise);
            }
            shown = name;
        }
        MovePill(name, true);
        if (name == "about") ((AboutHero)np.FindName("Hero")).StartIntro();
    }

    void MovePill(string page, bool anim) {
        NavItem ni = shell.FindName("Nav_" + page) as NavItem;
        if (ni == null || !ni.IsLoaded) return;
        double top = ni.TranslatePoint(new Point(0, 0), pillLayer).Y + 2;
        TranslateTransform tt = pill.RenderTransform as TranslateTransform;
        if (tt == null) { tt = new TranslateTransform(); pill.RenderTransform = tt; }
        if (!anim || Motion.Off) { tt.BeginAnimation(TranslateTransform.YProperty, null); tt.Y = top; return; }
        DoubleAnimation a = new DoubleAnimation(top, Motion.D(0.25));
        a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
        tt.BeginAnimation(TranslateTransform.YProperty, a);
    }

    // ------------------------------------------------------------ IUi
    public void Msg(string text) { LastDialog = Dialogs.Msg(overlay, "obj2x", text); }

    public void Disclaimer(bool review, Action<bool> done) {
        LastDialog = Dialogs.Disclaimer(overlay, review, delegate(bool ok) {
            if (!review) {
                if (ok) { S.Agreed = true; S.AgreedVer = AppInfo.Version; S.Save(); }
                else Close();
            }
            if (done != null) done(ok);
        });
    }

    public string BrowseFolder(string start) { return FolderPicker.Pick(this, start); }

    public string PickFile(string title, string filter, string start) {
        Microsoft.Win32.OpenFileDialog d = new Microsoft.Win32.OpenFileDialog();
        d.Title = title; d.Filter = filter; d.CheckFileExists = true;
        try { string dir = Path.GetDirectoryName(Conversion.Clean(start)); if (Directory.Exists(dir)) d.InitialDirectory = dir; } catch (Exception) { }
        return d.ShowDialog(this) == true ? d.FileName : null;
    }

    public string SaveFile(string name, string filter) {
        Microsoft.Win32.SaveFileDialog d = new Microsoft.Win32.SaveFileDialog();
        d.FileName = name; d.Filter = filter;
        return d.ShowDialog(this) == true ? d.FileName : null;
    }

    // 변환 스레드가 보내는 일을 모아서 한 번에 처리한다 (줄마다 화면 스레드를 깨우지 않게).
    // Background 우선순위라 화면 그리기와 입력이 먼저 처리되어 변환 중에도 창이 버벅이지 않는다.
    readonly Queue<Action> posted = new Queue<Action>();
    bool drainQueued;
    public void Post(Action a) {
        lock (posted) {
            posted.Enqueue(a);
            if (drainQueued) return;
            drainQueued = true;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Drain));
    }
    void Drain() {
        Action[] batch;
        lock (posted) { batch = posted.ToArray(); posted.Clear(); drainQueued = false; }
        foreach (Action a in batch) a();
    }

    public void ApplyLook() { Theme.Crossfade(host, delegate { Theme.Apply(S.Theme, S.Accent, S.SysFont); Theme.TitleBar(this); }); }

    public void Exit() { Close(); }
}
}
