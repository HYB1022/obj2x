using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace Obj2x.UI;

// 맥(openOMSI) 전용 설정: openOMSI content 폴더, OMSI 파일 인코딩. Windows에서는 화면에 나오지 않는다
public partial class MainViewModel {
    public bool IsMac => Platform.Mac;
    public bool IsWindows => !Platform.Mac;

    public ICommand AutoModsCmd { get; private set; }
    public ICommand BrowseModsCmd { get; private set; }
    public ObservableCollection<TrItem> AnsiChoices { get; private set; }

    void InitMac() {
        AutoModsCmd = new Cmd(delegate { SetMods(Platform.FindContent()); });
        BrowseModsCmd = new Cmd(delegate { ui.BrowseFolder(S.ModsDir, delegate(string p) { if (p != null) SetMods(p); }); });
        AnsiChoices = new ObservableCollection<TrItem> { new TrItem("0", delegate { return L.T("mac_enc_auto"); }) };
        foreach (int cp in Platform.CodePages) {
            string name = cp switch { 949 => "949 (한국어)", 1252 => "1252 (Deutsch, English…)", 1250 => "1250 (Polski, Čeština…)", 1251 => "1251 (Русский)", 932 => "932 (日本語)", 936 => "936 (中文)", _ => cp.ToString() };
            AnsiChoices.Add(new TrItem(cp.ToString(), delegate { return name; }));
        }
        Loc.Changed += delegate { foreach (TrItem t in AnsiChoices) t.Refresh(); Platform.ApplyEncoding(S); };
    }

    void SetMods(string dir) {
        S.ModsDir = dir ?? ""; S.Save();
        Platform.ApplyMods(S);
        Raise("ModsDirText", "ObjOutText", "SplOutText");
    }
    public string ModsDirText => string.IsNullOrEmpty(S.ModsDir) ? L.T("mac_mods_none") : S.ModsDir;

    public string AnsiCp {
        get { return S.AnsiCp.ToString(); }
        set { if (int.TryParse(value, out int n) && n != S.AnsiCp) { S.AnsiCp = n; S.Save(); Platform.ApplyEncoding(S); Raise("AnsiCp"); } }
    }
}
