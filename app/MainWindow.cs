using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Obj2x.UI;

// 메인 창: Shell.axaml과 페이지 화면을 읽어 붙이고, ViewModel이 부탁하는 화면 일(IUi)을 맡는다.
public class MainWindow : Window, IUi {
    public readonly MainViewModel VM;
    readonly Settings S;
    readonly bool upgrade;
    readonly Panel host, pageHost, overlay;
    readonly Control shell;
    readonly LayoutTransformControl zoomBox;
    readonly ScaleTransform zoom = new ScaleTransform(1, 1);
    TextBox logBox;
    readonly Dictionary<string, Control> pages = new();
    string shown = "dashboard";
    public DialogHost LastDialog;
    public static readonly string[] PageNames = { "dashboard", "wizard", "object", "spline", "bus", "map", "advanced", "settings", "dev", "about" };
    // 개발자 옵션: 알림(토스트), 새로 고침 빈도 표시, 레이아웃 경계 표시
    readonly Panel toastLayer = new Panel();
    readonly TextBlock fpsText = new TextBlock();
    readonly BoundsLayer bounds = new BoundsLayer();
    public static MainWindow Instance;

    public MainWindow(Settings s, string here, bool upgrade) {
        Instance = this;
        S = s; this.upgrade = upgrade;
        Title = L.T("app_title");
        try { Icon = Art.AppIcon(); } catch (Exception) { }
        Width = 1056; Height = 725; MinWidth = 920; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        Res.Dyn(this, FontFamilyProperty, "UiFont");

        host = Res.Dyn(new Panel(), Panel.BackgroundProperty, "Bg");
        shell = (Control)Xaml.Load("Shell.axaml");
        zoomBox = new LayoutTransformControl { Child = shell, LayoutTransform = zoom };
        host.Children.Add(zoomBox);
        bounds.Target = shell; bounds.IsVisible = false;
        host.Children.Add(bounds);
        toastLayer.IsHitTestVisible = false;
        host.Children.Add(toastLayer);
        fpsText.FontFamily = new FontFamily("Consolas, Menlo, monospace"); fpsText.FontSize = 13; fpsText.Foreground = Look.Brush(Color.FromRgb(80, 230, 120));
        fpsText.Background = Look.Brush(Color.FromArgb(170, 0, 0, 0)); fpsText.Padding = new Thickness(8, 3, 8, 3);
        fpsText.HorizontalAlignment = HorizontalAlignment.Right; fpsText.VerticalAlignment = VerticalAlignment.Top; fpsText.Margin = new Thickness(0, 6, 8, 0);
        fpsText.IsHitTestVisible = false; fpsText.IsVisible = false;
        host.Children.Add(fpsText);
        dropLayer = DropHint();
        host.Children.Add(dropLayer);
        Content = host;
        pageHost = shell.FindControl<Panel>("PageHost");
        overlay = shell.FindControl<Panel>("Overlay");

        VM = new MainViewModel(s, here, this);
        MacMenu.Attach(this);
        DataContext = VM;
        // 버스 탭 되돌리기 / 다시 실행 (글자 칸에 있을 때는 칸 자체의 되돌리기가 먼저 먹는다). 맥은 ⌘
        KeyModifiers mod = Omsi.IsWindows ? KeyModifiers.Control : KeyModifiers.Meta;
        KeyBindings.Add(new KeyBinding { Command = VM.UndoCmd, Gesture = new KeyGesture(Key.Z, mod) });
        KeyBindings.Add(new KeyBinding { Command = VM.RedoCmd, Gesture = new KeyGesture(Key.Y, mod) });
        KeyBindings.Add(new KeyBinding { Command = VM.RedoCmd, Gesture = new KeyGesture(Key.Z, mod | KeyModifiers.Shift) });
        // 대시보드만 바로 만들고, 다른 페이지는 처음 열 때 만든다 (시작이 빨라진다)
        logBox = Page("dashboard").FindControl<TextBox>("LogBox");
        pages["dashboard"].IsVisible = true; pages["dashboard"].Opacity = 1;

        VM.PageChanged += ShowPage;
        VM.PropertyChanged += (o, e) => {
            if (e.PropertyName == "DevFps" || e.PropertyName == "DevMode" || e.PropertyName == null) SetFps(VM.DevFps);
            if (e.PropertyName == "DevBounds" || e.PropertyName == "DevMode" || e.PropertyName == null) bounds.IsVisible = VM.DevBounds;
        };
        // 폴더/파일을 창 위에 끌어다 놓기
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, (o, e) => OnDrag(e));
        AddHandler(DragDrop.DragOverEvent, (o, e) => OnDrag(e));
        AddHandler(DragDrop.DragLeaveEvent, (o, e) => ShowDrop(false));
        AddHandler(DragDrop.DropEvent, (o, e) => {
            ShowDrop(false);
            string p = Behave.DropPath(e);
            if (p != null) { Activate(); VM.DropPath(p); }
        });
        VM.LogAppended += chunk => { if (logBox == null) return; logBox.Text += chunk; logBox.CaretIndex = logBox.Text.Length; };
        VM.LogCleared += () => { if (logBox != null) logBox.Text = ""; };
        Loc.Changed += () => Title = L.T("app_title");
        host.SizeChanged += (o, e) => UpdateZoom();
        Opened += (o, e) => {
            Look.FitToScreen(this);
            UpdateZoom();
            PreloadPages();
            SetFps(VM.DevFps);
            bounds.IsVisible = VM.DevBounds;
            if (S.UpdCheck) VM.StartUpdateCheck();
            if (this.upgrade && S.NeedDisclaimer()) Disclaimer(false, null);
        };
        Closing += (o, e) => { VM.SaveAll(); VM.E.Stop(); };
    }

    // ------------------------------------------------------------ 화면 배율
    // 기준 크기(1040x686)보다 창이 크면 글자와 여백을 함께 키운다. 0.1 단위로 최대 1.3배.
    void UpdateZoom() {
        double w = host.Bounds.Width, h = host.Bounds.Height;
        if (w < 200 || h < 200) return;
        double z = Math.Floor(Math.Min(w / 1040, h / 686) * 10 + 0.001) / 10;
        z = Math.Max(1, Math.Min(1.3, z));
        if (Math.Abs(z - zoom.ScaleX) < 0.01) return;
        zoom.ScaleX = zoom.ScaleY = z;
    }

    // 셀프 테스트/화면 캡처: 창 안쪽 크기를 정해 둔다
    public void FixClientSize(double w, double h) {
        WindowState = WindowState.Normal;
        ClientSize = new Size(w, h);
        host.Width = w; host.Height = h;
        Pump.Run(50); UpdateZoom();
    }
    public Control Host => host;
    public string LogText => logBox?.Text ?? "";     // 셀프 테스트용
    public Panel OverlayLayer => overlay;

    public Control Page(string name) {
        if (pages.TryGetValue(name, out Control p)) return p;
        p = (Control)Xaml.Load(char.ToUpper(name[0]) + name.Substring(1) + ".axaml");
        p.IsVisible = false;
        p.RenderTransform = new TranslateTransform();
        pages[name] = p; pageHost.Children.Add(p);
        return p;
    }

    // 시작한 뒤 한가할 때 나머지 페이지를 하나씩 미리 만들어 둔다 (처음 열 때 화면을 읽느라 끊기지 않게)
    void PreloadPages() {
        foreach (string n in PageNames) {
            if (pages.ContainsKey(n)) continue;
            string name = n;
            Dispatcher.UIThread.Post(() => Page(name), DispatcherPriority.ApplicationIdle);
        }
    }
    public int OverlayCount => overlay.Children.Count;

    // ------------------------------------------------------------ 페이지 전환 (새 화면이 살짝 떠오르며 나타남)
    void ShowPage(string name) {
        if (Array.IndexOf(PageNames, name) < 0) return;
        Control np = Page(name);
        if (name != shown) {
            pages[shown].IsVisible = false;
            np.IsVisible = true;
            TranslateTransform tt = (TranslateTransform)np.RenderTransform;
            if (!Motion.Disabled) {
                Tween.Run(np, "pgop", 0, 1, Motion.Sec(0.24), v => np.Opacity = v, t => t);
                Tween.Run(tt, "pgy", 14, 0, Motion.Sec(0.24), v => tt.Y = v);
            } else { np.Opacity = 1; tt.Y = 0; }
            shown = name;
        }
        if (name == "about") {
            np.FindControl<AboutHero>("Hero")?.StartIntro();
            foreach (HeroBackdrop b in np.GetVisualDescendants().OfType<HeroBackdrop>()) b.Wake();
        }
    }

    // ------------------------------------------------------------ IUi
    public void Msg(string text) { LastDialog = Dialogs.Msg(overlay, AppInfo.Name, text); }

    public void Disclaimer(bool review, Action<bool> done) {
        LastDialog = Dialogs.Disclaimer(overlay, review, ok => {
            if (!review) {
                if (ok) { S.Agreed = true; S.AgreedVer = AppInfo.Version; S.Save(); }
                else Close();
            }
            done?.Invoke(ok);
        });
    }

    static async Task<IStorageFolder> StartFolder(IStorageProvider sp, string start) {
        try {
            string d = start;
            if (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) d = Path.GetDirectoryName(Conversion.Clean(d));
            if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return await sp.TryGetFolderFromPathAsync(d);
        } catch (Exception) { }
        return null;
    }
    // "이름|*.a;*.b|이름2|*.c" → 고르기 창의 형식 목록
    static List<FilePickerFileType> Filters(string filter) {
        List<FilePickerFileType> r = new();
        if (string.IsNullOrEmpty(filter)) return r;
        string[] p = filter.Split('|');
        for (int i = 0; i + 1 < p.Length; i += 2)
            r.Add(new FilePickerFileType(p[i]) { Patterns = p[i + 1].Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToList() });
        return r;
    }

    public async void BrowseFolder(string start, Action<string> done) {
        string r = null;
        try {
            var res = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { SuggestedStartLocation = await StartFolder(StorageProvider, start), AllowMultiple = false });
            if (res.Count > 0) r = res[0].TryGetLocalPath();
            // 맥은 폴더 경로 끝에 '/'를 붙여 준다 (/Users/a/maps/X/). 이름·상위 폴더 계산이 틀어지지 않게 뗀다
            if (r != null && r.Length > 1 && (r.EndsWith('/') || r.EndsWith('\\')) && Path.GetPathRoot(r) != r) r = r.TrimEnd('/', '\\');
        } catch (Exception ex) { Msg(ex.Message); }
        done(r);
    }

    public async void PickFile(string title, string filter, string start, Action<string> done) {
        string r = null;
        try {
            List<FilePickerFileType> f = Filters(filter);
            // 맥은 실행 파일(blender)처럼 확장자가 없는 파일을 고를 수 있게 형식을 걸지 않는다
            if (!Omsi.IsWindows && f.Exists(x => x.Patterns.Any(pp => pp == "*" || pp.EndsWith(".exe")))) f = null;
            var res = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, FileTypeFilter = f, AllowMultiple = false, SuggestedStartLocation = await StartFolder(StorageProvider, start) });
            if (res.Count > 0) r = res[0].TryGetLocalPath();
        } catch (Exception ex) { Msg(ex.Message); }
        done(r);
    }

    public async void SaveFile(string name, string filter, Action<string> done) {
        string r = null;
        try {
            List<FilePickerFileType> f = Filters(filter);
            string ext = Path.GetExtension(name).TrimStart('.');
            var res = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = name, FileTypeChoices = f, DefaultExtension = ext, ShowOverwritePrompt = true });
            r = res?.TryGetLocalPath();
        } catch (Exception ex) { Msg(ex.Message); }
        done(r);
    }

    public async void Copy(string text) {
        try { if (Clipboard != null) await Clipboard.SetTextAsync(text); } catch (Exception ex) { Msg(ex.Message); }
    }

    public string RenderText {
        get {
            ModelView mv = FindModelView();
            if (mv != null && mv.RendererInfo.Length > 0 && !mv.RendererInfo.StartsWith("GL:")) return mv.RendererInfo;
            return "Skia";
        }
    }
    ModelView FindModelView() { if (pages.TryGetValue("bus", out Control b)) return b.GetVisualDescendants().OfType<ModelView>().FirstOrDefault(); return null; }

    // 변환 스레드가 보내는 일을 모아서 한 번에 처리한다 (줄마다 화면 스레드를 깨우지 않게).
    readonly Queue<Action> posted = new();
    bool drainQueued;
    public void Post(Action a) {
        lock (posted) {
            posted.Enqueue(a);
            if (drainQueued) return;
            drainQueued = true;
        }
        Dispatcher.UIThread.Post(Drain, DispatcherPriority.Background);
    }
    void Drain() {
        Action[] batch;
        lock (posted) { batch = posted.ToArray(); posted.Clear(); drainQueued = false; }
        foreach (Action a in batch) a();
    }

    // 테마를 바꿀 때 지금 화면을 찍어 위에 덮고, 새 테마를 적용한 뒤 찍은 화면을 서서히 지운다
    public void ApplyLook() {
        void apply() { Look.Apply(S.Theme, S.Accent, S.Font); }
        int w = (int)host.Bounds.Width, h = (int)host.Bounds.Height;
        if (Motion.Disabled || w < 1 || h < 1 || !host.IsVisible) { apply(); return; }
        double sc = RenderScaling;
        RenderTargetBitmap shot = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w * sc), (int)Math.Ceiling(h * sc)), new Vector(96 * sc, 96 * sc));
        shot.Render(host);
        Image img = new Image { Source = shot, Stretch = Stretch.Fill, IsHitTestVisible = false, Width = w, Height = h, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ZIndex = 10000 };
        host.Children.Add(img);
        apply();
        Tween.Run(img, "xf", 1, 0, Motion.Sec(0.35), v => img.Opacity = v, Motion.EaseInOut, () => { host.Children.Remove(img); shot.Dispose(); });
    }

    public void Exit() { Close(); }

    // 페이지를 맨 아래로 부드럽게 스크롤 (페이지 전환 뒤 레이아웃이 끝난 다음에)
    public void ScrollToEnd(string page) {
        Dispatcher.UIThread.Post(() => {
            if (Page(page) is not ScrollViewer sv) return;
            double from = sv.Offset.Y, to = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            Tween.Run(sv, "scroll", from, to, Motion.Sec(0.45), v => sv.Offset = new Vector(sv.Offset.X, v), Motion.EaseInOut);
        }, DispatcherPriority.Loaded);
    }

    public void Confirm(string title, string body, string okText, Action<bool> done) { LastDialog = Dialogs.Confirm(overlay, title, body, okText, done); }

    public void OpenFolder(string path) {
        try { Omsi.Open(path); } catch (Exception ex) { Msg(ex.Message); }
    }

    // 변환이 끝났을 때 다른 창을 보고 있으면 작업 표시줄 단추를 깜빡여 알린다 (Windows)
    public void Finished(bool failed) {
        if (IsActive || !Omsi.IsWindows) return;
        try {
            IntPtr hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero) return;
            FLASHWINFO f = new FLASHWINFO { cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(), hwnd = hwnd, dwFlags = 3 | 12 };
            FlashWindowEx(ref f);
        } catch (Exception) { }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
    [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    // ------------------------------------------------------------ 끌어다 놓기
    readonly Border dropLayer;
    Border DropHint() {
        Border b = new Border { IsHitTestVisible = false, IsVisible = false, Margin = new Thickness(14), CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(2.5),
            Background = Look.Brush(Color.FromArgb(150, 0, 0, 0)) };
        Res.Dyn(b, Border.BorderBrushProperty, "Accent");
        b.Child = new TextBlock { Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return b;
    }
    void OnDrag(DragEventArgs e) {
        bool ok = Behave.DropPath(e) != null && OverlayCount == 0;
        e.DragEffects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        ShowDrop(ok);
    }
    public void DropPreview(bool on) { ShowDrop(on); }   // 개발용(소개 영상)
    void ShowDrop(bool on) {
        if (on == dropLayer.IsVisible) return;
        ((TextBlock)dropLayer.Child).Text = L.T(VM.Page == "object" ? "drop_hint_obj" : VM.Page == "spline" ? "drop_hint_tex" : VM.Page == "bus" ? "drop_hint_bus" : VM.Page == "map" ? "drop_hint_map" : "drop_hint");
        dropLayer.IsVisible = on;
        if (on) Tween.Run(dropLayer, "op", 0, 1, Motion.Sec(0.15), v => dropLayer.Opacity = v, t => t);
    }

    // ------------------------------------------------------------ 알림 (화면 아래 가운데에 잠깐 떴다 사라진다)
    Border toast; DispatcherTimer toastTimer;
    public void Toast(string text) {
        if (toast != null) toastLayer.Children.Remove(toast);
        TextBlock t = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
        toast = new Border { Child = t, CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 10, 18, 10), Background = Look.Brush(Color.FromArgb(225, 34, 36, 44)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 40) };
        TranslateTransform tt = new TranslateTransform(0, 12); toast.RenderTransform = tt;
        toastLayer.Children.Add(toast);
        Border me = toast;
        Tween.Run(me, "op", 0, 1, Motion.Sec(0.2), v => me.Opacity = v, x => x);
        Tween.Run(tt, "y", 12, 0, Motion.Sec(0.25), v => tt.Y = v);
        toastTimer?.Stop();
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        toastTimer.Tick += (o, e) => {
            toastTimer.Stop();
            Tween.Run(me, "op", me.Opacity, 0, Motion.Sec(0.25), v => me.Opacity = v, x => x, () => { toastLayer.Children.Remove(me); if (toast == me) toast = null; });
        };
        toastTimer.Start();
    }
    public bool FpsVisible => fpsText.IsVisible;   // 셀프 테스트용
    public string LastToast => toast != null ? ((TextBlock)toast.Child).Text : null;   // 셀프 테스트용

    public string ScaleText => Math.Round(RenderScaling * 100) + "% · " + zoom.ScaleX.ToString("0.0") + "x";

    // ------------------------------------------------------------ 새로 고침 빈도 표시 (오른쪽 위, 0.5초마다 갱신)
    bool fpsOn; int fpsFrames; DateTime fpsStart;
    void SetFps(bool on) {
        if (on == fpsOn) return;
        fpsOn = on;
        fpsText.IsVisible = on;
        if (on) { fpsFrames = 0; fpsStart = DateTime.Now; fpsText.Text = "-- fps"; RequestAnimationFrame(FpsFrame); }
    }
    void FpsFrame(TimeSpan t) {
        if (!fpsOn) return;
        fpsFrames++;
        double sec = (DateTime.Now - fpsStart).TotalSeconds;
        if (sec >= 0.5) { fpsText.Text = Math.Round(fpsFrames / sec) + " fps"; fpsFrames = 0; fpsStart = DateTime.Now; }
        RequestAnimationFrame(FpsFrame);
    }
}
