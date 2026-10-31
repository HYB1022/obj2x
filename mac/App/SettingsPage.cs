using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Obj2x;

namespace BusDepotMac;

// 설정: OMSI 2 폴더, openOMSI Mods 폴더, OMSI 파일 인코딩, 언어, 테마
public class SettingsPage : UserControl {
    readonly TextBlock omsiText = Ui.Sub(""), modsText = Ui.Sub("");

    public SettingsPage() {
        Settings S = MacEnv.S;
        List<string[]> langs = new List<string[]>();
        foreach (string[] l in L.Languages) langs.Add(new[] { l[0], l[1] });
        List<string[]> enc = new List<string[]> { new[] { "0", L.T("mac_enc_auto") } };
        foreach (int cp in MacEnv.CodePages) enc.Add(new[] { cp.ToString(), cp switch { 949 => "949 (한국어)", 1252 => "1252 (Deutsch, English…)", 1250 => "1250 (Polski, Čeština…)", 1251 => "1251 (Русский)", 932 => "932 (日本語)", 936 => "936 (中文)", _ => cp.ToString() } });
        string[][] themes = { new[] { "system", L.T("mac_theme_system") }, new[] { "light", L.T("mac_theme_light") }, new[] { "dark", L.T("mac_theme_dark") } };
        Content = Ui.Page(
            Ui.Title(L.T("nav_settings")),
            Ui.Caption("OMSI 2 / openOMSI"),
            Ui.Card(
                Ui.Row(L.T("mac_omsi_dir"), L.T("mac_omsi_dir_sub"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Ui.Btn(L.T("btn_auto"), () => { S.OmsiDir = MacEnv.FindOmsi(); S.Save(); Refresh(); }),
                    Ui.Btn(L.T("btn_browse"), async () => { string d = await Ui.PickFolder(L.T("mac_omsi_dir"), S.OmsiDir); if (d != null) { if (!Omsi.IsInstall(d)) Ui.Msg(L.T("omsi_err")); S.OmsiDir = d; S.Save(); Refresh(); } }) } }),
                omsiText,
                Ui.Row(L.T("mac_mods_dir"), L.T("mac_mods_dir_sub"), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Ui.Btn(L.T("btn_auto"), () => { S.ModsDir = MacEnv.FindMods(); S.Save(); MacEnv.ApplyMods(); Refresh(); }),
                    Ui.Btn(L.T("btn_browse"), async () => { string d = await Ui.PickFolder(L.T("mac_mods_dir"), S.ModsDir); if (d != null) { S.ModsDir = d; S.Save(); MacEnv.ApplyMods(); Refresh(); } }) } }),
                modsText,
                Ui.Row(L.T("mac_encoding"), L.T("mac_encoding_sub"), Ui.Combo(enc, S.AnsiCp.ToString(), v => { S.AnsiCp = int.Parse(v); S.Save(); MacEnv.ApplyEncoding(); }, 260))),
            Ui.Caption(L.T("mac_sec_view")),
            Ui.Card(
                Ui.Row(L.T("mac_lang"), null, Ui.Combo(langs, L.Lang, v => { S.Lang = v; L.Lang = v; S.Save(); MacEnv.ApplyEncoding(); MainWindow.Instance.Rebuild(); })),
                Ui.Row(L.T("mac_theme"), null, Ui.Combo(themes, S.Theme, v => { S.Theme = v; S.Save(); App.ApplyTheme(); MainWindow.Instance.Rebuild(); }))));
        Refresh();
    }

    void Refresh() {
        Settings S = MacEnv.S;
        omsiText.Text = MacEnv.OmsiOk ? S.OmsiDir : L.T("omsi_notfound");
        modsText.Text = string.IsNullOrEmpty(S.ModsDir) ? L.T("mac_mods_none") : S.ModsDir + "\n" + L.T("mac_make_root", MacEnv.MakeRoot);
    }
}

public class AboutPage : UserControl {
    public AboutPage() {
        Content = Ui.Page(
            Ui.Title("Bus Depot"),
            Ui.Sub("v" + AppInfo.Version + " · macOS"),
            new Border { Height = 10 },
            Ui.Card(
                Ui.Text(L.T("mac_about_desc"), 14),
                new Border { Height = 8 },
                Ui.Row(L.T("about_row_version"), null, Ui.Text(AppInfo.Version)),
                Ui.Row(L.T("about_row_repo"), null, Ui.Btn(AppInfo.Repo, () => Open("https://github.com/" + AppInfo.Repo))),
                Ui.Row("openOMSI", null, Ui.Btn("github.com/openOMSI-org", () => Open("https://github.com/openOMSI-org/openOMSI")))),
            new Border { Height = 10 },
            Ui.Sub("Copyright (C) 2026 Hyunwoo Bang, Junyoung · GPL-3.0-or-later\nHYB1022 · qbasys08 · Claude (Anthropic)"));
    }
    static void Open(string url) {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { try { Process.Start("open", url); } catch (Exception ex) { Ui.Msg(ex.Message); } }
    }
}
