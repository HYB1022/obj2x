using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace Obj2x.UI {

public partial class MainViewModel {
    int wizStep = 1;
    string wizMode = "std";
    bool stdEnabled = true, xEnabled = true;
    public ObservableCollection<RecentItem> Recent { get; private set; }
    public ICommand WizNextCmd { get; private set; }
    public ICommand WizBackCmd { get; private set; }
    public ICommand WizCancelCmd { get; private set; }
    public ICommand PickModeCmd { get; private set; }
    public ICommand BrowseInCmd { get; private set; }
    public ICommand BrowseOutCmd { get; private set; }
    public ICommand PickRecentCmd { get; private set; }
    public ICommand ParUpCmd { get; private set; }
    public ICommand ParDownCmd { get; private set; }

    void InitWizard() {
        Recent = new ObservableCollection<RecentItem>();
        WizNextCmd = new Cmd(delegate { WizNext(); }, delegate(object p) { return !running; });
        WizBackCmd = new Cmd(delegate { if (wizStep > 1) WizStep = wizStep - 1; });
        WizCancelCmd = new Cmd(delegate { Page = "dashboard"; });
        PickModeCmd = new Cmd(delegate(object p) { SetMode((string)p, true); });
        BrowseInCmd = new Cmd(delegate { string p = ui.BrowseFolder(InPath); if (p != null) InPath = p; });
        BrowseOutCmd = new Cmd(delegate { string p = ui.BrowseFolder(OutPath); if (p != null) OutPath = p; });
        PickRecentCmd = new Cmd(delegate(object p) { string s = p as string; if (s != null && Directory.Exists(s)) InPath = s; });
        ParUpCmd = new Cmd(delegate { Par = Par + 1; });
        ParDownCmd = new Cmd(delegate { Par = Par - 1; });
    }

    public int WizStep {
        get { return wizStep; }
        set {
            if (Set(ref wizStep, Math.Max(1, Math.Min(4, value)))) { Raise("BackVisible"); Raise("NextText"); }
            if (wizStep == 2) RecentRefresh();
            if (wizStep == 4) Raise("Summary");
        }
    }
    public bool BackVisible { get { return wizStep > 1; } }
    public string NextText { get { return L.T(wizStep == 4 ? "btn_run" : "btn_next"); } }

    // ---- 1단계: 종류
    public string WizMode { get { return wizMode; } }
    public bool IsStd { get { return wizMode == "std"; } }
    public bool IsX { get { return wizMode == "x"; } }
    public bool IsFast { get { return wizMode == "fast"; } }
    public bool StdEnabled { get { return stdEnabled; } }
    public bool XEnabled { get { return xEnabled; } }
    public string AvailText { get; private set; }

    // persist=false: 도구가 없어 임시로 바꾸는 경우라 저장된 선택은 건드리지 않는다
    void SetMode(string m, bool persist) {
        wizMode = m;
        if (persist) { S.Fast = (m == "fast"); S.DoConv = (m == "std"); }
        Raise("WizMode"); Raise("IsStd"); Raise("IsX"); Raise("IsFast");
        Raise("OutLocked"); Raise("OutEnabled"); Raise("OutPath"); Raise("ParEnabled"); Raise("Summary");
    }

    // 도구가 없으면 해당 방식을 잠근다
    void WizAvailability() {
        bool hb = Conversion.BlenderOk(S), hc = Conversion.ConvOk(S);
        stdEnabled = hb && hc; xEnabled = hb;
        Raise("StdEnabled"); Raise("XEnabled");
        if (!stdEnabled && wizMode == "std") SetMode("fast", false);
        if (!xEnabled && wizMode == "x") SetMode("fast", false);
        List<string> missing = Conversion.MissingTools(S);
        if (missing.Count > 0) {
            string r = L.T("wiz_avail_head", string.Join(", ", missing.ToArray()));
            if (!hb) r += L.T("wiz_avail_noblender");
            if (!hc) r += L.T("wiz_avail_noconv");
            AvailText = r + L.T("wiz_avail_tail");
        } else AvailText = "";
        Raise("AvailText");
    }

    // 마법사에 들어올 때 저장된 설정으로 다시 채운다
    void WizEnter() {
        wizStep = 1; Raise("WizStep"); Raise("BackVisible"); Raise("NextText");
        SetMode(S.Fast ? "fast" : (S.DoConv ? "std" : "x"), false);
        WizAvailability();
        Raise("InPath"); Raise("OutPath"); Raise("SameDir"); Raise("Inc"); Raise("Sub"); Raise("Par");
        RecentRefresh();
    }

    // ---- 2단계: 경로
    public string InPath { get { return S.Inp; } set { S.Inp = value ?? ""; Raise("InPath"); if (OutLocked) Raise("OutPath"); Raise("Summary"); } }
    public string OutPath { get { return OutLocked ? S.Inp : S.Out; } set { if (!OutLocked) { S.Out = value ?? ""; Raise("OutPath"); Raise("Summary"); } } }
    public bool SameDir { get { return S.SameDir; } set { S.SameDir = value; Raise("SameDir"); Raise("OutLocked"); Raise("OutEnabled"); Raise("OutPath"); } }
    public bool OutLocked { get { return S.SameDir || wizMode == "fast"; } }
    public bool OutEnabled { get { return !OutLocked; } }
    public bool RecentEmpty { get { return Recent.Count == 0; } }

    public void RecentRefresh() {
        Recent.Clear();
        foreach (string p in S.Recent) { RecentItem it = new RecentItem(); it.Path = p; it.Exists = Directory.Exists(p); Recent.Add(it); }
        Raise("RecentEmpty");
    }

    // ---- 3단계: 옵션
    public bool Inc { get { return S.Inc; } set { S.Inc = value; Raise("Inc"); } }
    public bool Sub { get { return S.Sub; } set { S.Sub = value; Raise("Sub"); } }
    public int Par { get { return S.Par; } set { S.Par = Math.Max(1, Math.Min(20, value)); Raise("Par"); } }
    public bool ParEnabled { get { return wizMode == "std"; } }

    // ---- 4단계: 확인
    public string[] Summary {
        get {
            List<string> o = new List<string>();
            if (S.Inc) o.Add(L.T("sum_opt_inc"));
            if (S.Sub) o.Add(L.T("sum_opt_sub"));
            if (wizMode == "std") o.Add(L.T("sum_opt_par", S.Par));
            return new string[] {
                L.T(wizMode == "fast" ? "sum_mode_fast" : (wizMode == "x" ? "sum_mode_x" : "sum_mode_std")),
                S.Inp, OutPath, o.Count > 0 ? string.Join(", ", o.ToArray()) : L.T("sum_opt_none") };
        }
    }

    void WizNext() {
        if (wizStep == 2) {
            if (!Directory.Exists(S.Inp.Trim())) { ui.Msg(L.T("msg_err_in")); return; }
            if (!OutLocked && S.Out.Trim().Length == 0) { ui.Msg(L.T("msg_err_out")); return; }
        }
        if (wizStep < 4) WizStep = wizStep + 1;
        else StartConversion();
    }
}
}
