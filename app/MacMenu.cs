using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Obj2x.UI;

// 맥 메뉴 막대의 앱 메뉴 ('Bus Depot' 아래): Avalonia 기본 항목(About Avalonia 등) 대신 앱의 페이지로 바로 가기.
// ⌘1~⌘9로 위에서부터, ⌘,로 설정. 숨기기/종료 같은 표준 항목은 macOS(Avalonia)가 뒤에 붙인다.
// 앱 메뉴는 앱이 시작될 때(App.Initialize) 만들어 둬야 macOS에 반영된다. 나중에는 글자/보이기만 바꾼다.
// 메뉴 단축키는 창이 키를 먼저 받으면 메뉴까지 가지 않을 수 있어서 창에서도 ⌘숫자를 직접 처리한다.
public static class MacMenu {
    static readonly Dictionary<string, NativeMenuItem> items = new();
    static readonly Dictionary<Key, string> keys = new();
    static MainWindow win;

    public static void Init(Application app) {
        if (!Platform.Mac) return;
        NativeMenu menu = new NativeMenu();
        menu.Add(Item("about", null));
        menu.Add(new NativeMenuItemSeparator());
        foreach (string p in MainWindow.PageNames) {
            if (p == "about" || p == "settings") continue;
            menu.Add(Item(p, null));
        }
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(Item("settings", new KeyGesture(Key.OemComma, KeyModifiers.Meta)));
        keys[Key.OemComma] = "settings";
        NativeMenu.SetMenu(app, menu);
        Refresh();
    }

    public static void Attach(MainWindow mw) {
        if (!Platform.Mac) return;
        win = mw;
        Refresh();
        Loc.Changed += Refresh;
        mw.VM.PropertyChanged += (s, e) => { if (e.PropertyName == "DevMode") Refresh(); };
        mw.AddHandler(InputElement.KeyDownEvent, (s, e) => {
            if (e.KeyModifiers != KeyModifiers.Meta || !keys.TryGetValue(e.Key, out string page)) return;
            Go(page); e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    // 언어에 맞는 글자, 개발자 옵션은 켰을 때만, 보이는 페이지에 위에서부터 ⌘1~⌘9
    static void Refresh() {
        int n = 0;
        foreach (string p in MainWindow.PageNames) {
            if (!items.TryGetValue(p, out NativeMenuItem it)) continue;
            it.Header = L.T(p == "about" ? "mac_menu_about" : "nav_" + p);
            if (p == "about" || p == "settings") continue;
            bool show = p != "dev" || (win != null && win.VM.DevMode);
            it.IsVisible = show;
            if (!show) { it.Gesture = null; continue; }
            n++;
            Key k = Key.D0 + n;
            if (n <= 9) { it.Gesture = new KeyGesture(k, KeyModifiers.Meta); keys[k] = p; }
        }
        for (int i = n + 1; i <= 9; i++) keys.Remove(Key.D0 + i);
    }

    static NativeMenuItem Item(string page, KeyGesture key) {
        NativeMenuItem it = new NativeMenuItem(page);
        if (key != null) it.Gesture = key;
        it.Click += (s, e) => Go(page);
        items[page] = it;
        return it;
    }

    static void Go(string page) {
        if (win == null) return;
        win.VM.Page = page; win.Activate();
    }
}
