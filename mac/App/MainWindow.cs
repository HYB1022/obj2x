using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Obj2x;

namespace BusDepotMac;

// 왼쪽 메뉴 + 오른쪽 페이지. 언어/테마를 바꾸면 페이지를 새로 만든다
public class MainWindow : Window {
    readonly ListBox nav = new ListBox();
    readonly ContentControl host = new ContentControl();
    readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
    string current = "convert";
    public static MainWindow Instance;

    static readonly string[][] Nav = {
        new[] { "convert", "mac_nav_convert" }, new[] { "bus", "nav_bus" }, new[] { "map", "nav_map" },
        new[] { "object", "nav_object" }, new[] { "spline", "nav_spline" }, new[] { "settings", "nav_settings" }, new[] { "about", "nav_info" } };

    public MainWindow() {
        Instance = this; Ui.Main = this;
        Title = "Bus Depot";
        Width = 1180; Height = 780; MinWidth = 860; MinHeight = 560;
        Rebuild();
    }

    public void Rebuild() {
        Ui.Dark = Application.Current.ActualThemeVariant == ThemeVariant.Dark || (MacEnv.S.Theme == "dark");
        pages.Clear();
        Background = Ui.PageBg;
        Grid g = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*") };

        StackPanel side = new StackPanel { Margin = new Thickness(16, 20, 8, 16), Spacing = 6 };
        side.Children.Add(new TextBlock { Text = "Bus Depot", FontSize = 26, FontWeight = FontWeight.Bold, Margin = new Thickness(8, 0, 0, 0) });
        side.Children.Add(new TextBlock { Text = "v" + AppInfo.Version + " · macOS · openOMSI", FontSize = 12, Opacity = 0.6, Margin = new Thickness(8, 0, 0, 12) });
        nav.Items.Clear();
        foreach (string[] n in Nav) nav.Items.Add(new ListBoxItem { Content = L.T(n[1]), Tag = n[0], Padding = new Thickness(12, 10), FontSize = 15 });
        nav.Background = Brushes.Transparent;
        nav.SelectionChanged -= NavChanged;
        nav.SelectionChanged += NavChanged;
        side.Children.Add(nav);
        g.Children.Add(side);
        Grid.SetColumn(host, 1);
        if (host.Parent is Panel old) old.Children.Remove(host);
        g.Children.Add(host);
        Content = g;
        Show(current);
    }

    void NavChanged(object s, SelectionChangedEventArgs e) {
        if (nav.SelectedItem is ListBoxItem it && it.Tag is string code && code != currentShown) Show(code);
    }
    string currentShown;

    public void Show(string code) {
        current = code; currentShown = code;
        for (int i = 0; i < Nav.Length; i++) if (Nav[i][0] == code && nav.SelectedIndex != i) nav.SelectedIndex = i;
        if (!pages.TryGetValue(code, out Control p)) {
            p = code switch {
                "convert" => new ConvertPage(),
                "bus" => new BusPage(),
                "map" => new MapPage(),
                "object" => new ObjectPage(),
                "spline" => new SplinePage(),
                "settings" => new SettingsPage(),
                _ => new AboutPage(),
            };
            pages[code] = p;
        }
        host.Content = p;
    }
    public T Page<T>(string code) where T : Control { Show(code); return (T)pages[code]; }
}
