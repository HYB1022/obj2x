using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Obj2x;

namespace BusDepotMac;

// 화면 조각을 코드로 만드는 도우미 (XAML 없이). 카드, 줄, 숫자 칸, 파일/폴더 고르기, 알림
public static class Ui {
    public static Window Main;
    public static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(80, 70, 229));
    public static bool Dark;
    public static IBrush CardBg => new SolidColorBrush(Dark ? Color.FromRgb(36, 38, 46) : Colors.White);
    public static IBrush PageBg => new SolidColorBrush(Dark ? Color.FromRgb(22, 23, 28) : Color.FromRgb(244, 245, 249));

    public static TextBlock Title(string t) => new TextBlock { Text = t, FontSize = 28, FontWeight = FontWeight.Bold, Margin = new Thickness(4, 0, 0, 4) };
    public static TextBlock Sub(string t) => new TextBlock { Text = t, FontSize = 12.5, Opacity = 0.65, TextWrapping = TextWrapping.Wrap };
    public static TextBlock Caption(string t) => new TextBlock { Text = t, FontSize = 12.5, Opacity = 0.6, Margin = new Thickness(16, 18, 0, 6) };
    public static TextBlock Text(string t, double size = 14, bool bold = false) => new TextBlock { Text = t, FontSize = size, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };

    // 둥근 카드 (안에 세로로 쌓기)
    public static Border Card(params Control[] items) {
        StackPanel p = new StackPanel { Spacing = 2 };
        foreach (Control c in items) if (c != null) p.Children.Add(c);
        return new Border { Child = p, CornerRadius = new CornerRadius(16), Padding = new Thickness(18, 12), Background = CardBg };
    }

    // 제목 + 설명 왼쪽, 컨트롤 오른쪽인 줄
    public static Control Row(string title, string sub, Control right) {
        Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6) };
        StackPanel left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Text(title));
        if (!string.IsNullOrEmpty(sub)) left.Children.Add(Sub(sub));
        g.Children.Add(left);
        if (right != null) { right.VerticalAlignment = VerticalAlignment.Center; right.Margin = new Thickness(16, 0, 0, 0); Grid.SetColumn(right, 1); g.Children.Add(right); }
        return g;
    }

    public static Button Btn(string t, Action click, bool primary = false) {
        Button b = new Button { Content = t, Padding = new Thickness(14, 7), CornerRadius = new CornerRadius(10) };
        if (primary) b.Classes.Add("accent");
        b.Click += (s, e) => { try { click(); } catch (Exception ex) { Msg(ex.Message); } };
        return b;
    }

    public static CheckBox Check(bool value, Action<bool> changed) {
        CheckBox c = new CheckBox { IsChecked = value };
        c.IsCheckedChanged += (s, e) => changed(c.IsChecked == true);
        return c;
    }

    // 숫자 칸: 고칠 때마다 숫자로 읽히면 set (입력 중인 글자는 그대로 둔다)
    public static TextBox Num(double value, Action<double> set, double width = 90, bool enabled = true) {
        TextBox t = new TextBox { Text = CfgDoc.Fmt(value), Width = width, HorizontalContentAlignment = HorizontalAlignment.Right, IsEnabled = enabled };
        t.TextChanged += (s, e) => { double v; if (CfgDoc.Num(t.Text, out v)) set(v); };
        return t;
    }
    public static TextBox Edit(string value, Action<string> set, double width = 220) {
        TextBox t = new TextBox { Text = value ?? "", Width = width };
        t.TextChanged += (s, e) => set(t.Text ?? "");
        return t;
    }

    public static ComboBox Combo(IList<string[]> items, string selected, Action<string> changed, double width = 220) {
        ComboBox c = new ComboBox { Width = width };
        int sel = 0;
        for (int i = 0; i < items.Count; i++) { c.Items.Add(items[i][1]); if (items[i][0] == selected) sel = i; }
        c.SelectedIndex = items.Count > 0 ? sel : -1;
        c.SelectionChanged += (s, e) => { if (c.SelectedIndex >= 0) changed(items[c.SelectedIndex][0]); };
        return c;
    }

    // 세그먼트 대신 라디오 버튼 묶음
    public static StackPanel Choice(IList<string[]> items, string selected, Action<string> changed) {
        StackPanel p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        string group = Guid.NewGuid().ToString();
        foreach (string[] it in items) {
            RadioButton r = new RadioButton { Content = it[1], GroupName = group, IsChecked = it[0] == selected };
            string code = it[0];
            r.IsCheckedChanged += (s, e) => { if (r.IsChecked == true) changed(code); };
            p.Children.Add(r);
        }
        return p;
    }

    public static ScrollViewer Page(params Control[] items) {
        StackPanel p = new StackPanel { Spacing = 4, Margin = new Thickness(24, 20, 24, 28), MaxWidth = 860 };
        foreach (Control c in items) if (c != null) p.Children.Add(c);
        return new ScrollViewer { Content = p, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public static async Task<string> PickFolder(string title, string start) {
        IStorageProvider sp = Main.StorageProvider;
        FolderPickerOpenOptions o = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
        try { if (!string.IsNullOrEmpty(start) && System.IO.Directory.Exists(start)) o.SuggestedStartLocation = await sp.TryGetFolderFromPathAsync(start); } catch (Exception) { }
        IReadOnlyList<IStorageFolder> r = await sp.OpenFolderPickerAsync(o);
        return r.Count > 0 ? r[0].TryGetLocalPath() : null;
    }
    public static async Task<string> PickFile(string title, string kind, string[] patterns, string start) {
        IStorageProvider sp = Main.StorageProvider;
        FilePickerOpenOptions o = new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType(kind) { Patterns = patterns } } };
        try {
            string d = start;
            if (!string.IsNullOrEmpty(d) && System.IO.File.Exists(d)) d = System.IO.Path.GetDirectoryName(d);
            if (!string.IsNullOrEmpty(d) && System.IO.Directory.Exists(d)) o.SuggestedStartLocation = await sp.TryGetFolderFromPathAsync(d);
        } catch (Exception) { }
        IReadOnlyList<IStorageFile> r = await sp.OpenFilePickerAsync(o);
        return r.Count > 0 ? r[0].TryGetLocalPath() : null;
    }

    // 창 안에 뜨는 간단한 알림 창
    public static void Msg(string text, string title = "Bus Depot") {
        Dispatcher.UIThread.Post(() => {
            Window w = new Window { Title = title, Width = 460, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
            StackPanel p = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
            p.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeight.Bold });
            p.Children.Add(new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            Button ok = new Button { Content = L.T("btn_close"), HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(22, 7) };
            ok.Classes.Add("accent");
            ok.Click += (s, e) => w.Close();
            p.Children.Add(ok);
            w.Content = p;
            if (Main != null && Main.IsVisible) w.ShowDialog(Main); else w.Show();
        });
    }

    public static void Post(Action a) => Dispatcher.UIThread.Post(a);
    public static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
