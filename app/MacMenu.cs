using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Obj2x.UI;

// 맥 메뉴 막대의 앱 메뉴 ('Bus Depot' 아래): Avalonia 기본 항목(About Avalonia 등) 대신 앱의 페이지로 바로 가기.
// ⌘1~⌘9로 위에서부터, ⌘,로 설정. 숨기기/종료 같은 표준 항목은 macOS(Avalonia)가 뒤에 붙인다.
public static class MacMenu {
    public static void Attach(MainWindow mw) {
        if (!Platform.Mac || Application.Current == null) return;
        void build() {
            NativeMenu menu = new NativeMenu();
            menu.Add(Item(L.T("mac_menu_about"), "about", null, mw));
            menu.Add(new NativeMenuItemSeparator());
            int n = 0;
            foreach (string p in MainWindow.PageNames) {
                if (p == "about" || p == "settings" || (p == "dev" && !mw.VM.DevMode)) continue;
                n++;
                menu.Add(Item(L.T("nav_" + p), p, n <= 9 ? new KeyGesture(Key.D0 + n, KeyModifiers.Meta) : null, mw));
            }
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(Item(L.T("nav_settings"), "settings", new KeyGesture(Key.OemComma, KeyModifiers.Meta), mw));
            NativeMenu.SetMenu(Application.Current, menu);
        }
        build();
        Loc.Changed += build;
        mw.VM.PropertyChanged += (s, e) => { if (e.PropertyName == "DevMode") build(); };
    }

    static NativeMenuItem Item(string header, string page, KeyGesture key, MainWindow mw) {
        NativeMenuItem it = new NativeMenuItem(header);
        if (key != null) it.Gesture = key;
        it.Click += (s, e) => { mw.VM.Page = page; mw.Activate(); };
        return it;
    }
}
