using System;
using System.Drawing;
using System.Windows.Forms;

namespace Obj2x {

// 앵커 대신 쓰는 단순 배치 규칙. 부모 크기가 바뀔 때 Pin.Apply 로 오른쪽 맞춤/폭 늘리기를 직접 계산한다.
// (숨겨진 패널의 앵커 계산이 어긋나는 문제를 피하기 위함)
public class PinInfo {
    public int Right = -1;          // >=0: 부모 오른쪽 가장자리에서 이만큼 띄워 맞춤
    public int StretchRight = -1;   // >=0: 폭을 부모 폭 - Left - 이 값으로
    public static PinInfo R(int m) { PinInfo p = new PinInfo(); p.Right = m; return p; }
    public static PinInfo S(int m) { PinInfo p = new PinInfo(); p.StretchRight = m; return p; }
}

public static class Pin {
    public static void Apply(Control root) {
        int pw = root.ClientSize.Width;
        foreach (Control c in root.Controls) {
            PinInfo pi = c.Tag as PinInfo;
            if (pi != null) {
                if (pi.StretchRight >= 0) { int w = Math.Max(20, pw - c.Left - Ui.S(pi.StretchRight)); if (c.Width != w) c.Width = w; }
                if (pi.Right >= 0) { int x = Math.Max(0, pw - Ui.S(pi.Right) - c.Width); if (c.Left != x) c.Left = x; }
            }
            if (c.Controls.Count > 0) Apply(c);
        }
    }
}
}
