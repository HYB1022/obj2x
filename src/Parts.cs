using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Obj2x {

public class SidePanel : Panel, IRefresh {
    // 선택된 메뉴 뒤의 알약 모양이 메뉴 사이를 미끄러져 움직인다
    Fader py; Rectangle pill = Rectangle.Empty; bool placed;
    public SidePanel() { DoubleBuffered = true; BackColor = Theme.Sidebar; py = new Fader(this, 0, true); py.K = 15; }
    public void RefreshUi() { BackColor = Theme.Sidebar; }
    public void MovePill(Control target) {
        pill = target.Bounds;
        if (!placed || !Visible) { placed = true; py.Set(target.Top); Invalidate(true); }
        else py.To(target.Top);
    }
    public void Rescaled(Control target) { pill = target.Bounds; py.Set(target.Top); Invalidate(true); }
    public bool PillOver(Control c) { return pill.Width > 0 && Math.Abs(py.V - c.Top) < c.Height * 0.6f; }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        using (Pen p = new Pen(Theme.Divider, 1)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
        if (pill.Width > 0) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(pill.X + 2, py.V + 2, pill.Width - 4, pill.Height - 4);
            using (GraphicsPath gp = Ui.Round(r.X, r.Y, r.Width, r.Height, Ui.S(12f)))
            using (SolidBrush b = new SolidBrush(Theme.NavPill))
            using (Pen p = new Pen(Theme.NavPillBdr, 1)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
        }
        base.OnPaint(e);
    }
}

public class StatusPanel : Panel, IRefresh {
    public StatusPanel() { DoubleBuffered = true; BackColor = Theme.Sidebar; }
    public void RefreshUi() { BackColor = Theme.Sidebar; }
    protected override void OnPaint(PaintEventArgs e) {
        using (Pen p = new Pen(Theme.Divider, 1)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
        base.OnPaint(e);
    }
}

// 사이드바 위쪽 로고 (OBJ2X 로고 타일 + 글자)
public class LogoPanel : Control, IRefresh {
    public LogoPanel() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Size = new Size(210, 64);
    }
    public void RefreshUi() { Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Height / 64f;
        LogoArt.Draw(g, (int)(14 * k), (int)(12 * k), (int)(40 * k));
        TextRenderer.DrawText(g, "obj2x", Ui.F(FR.Big), new Point((int)(64 * k), (int)(15 * k)), Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}

public static class LogoArt {
    // 뒤쪽 그라데이션 타일 + 앞쪽 반투명 타일 + OBJ/2X 글자
    public static void Draw(Graphics g, int x, int y, int size) {
        float s = size / 172f;
        using (GraphicsPath back = Ui.Round(x + 36 * s - 14 * s, y + 36 * s - 14 * s, 150 * s, 150 * s, 34 * s))
        using (LinearGradientBrush gb = new LinearGradientBrush(new RectangleF(x + 22 * s, y + 22 * s, 150 * s, 150 * s), Theme.Accent, Theme.AccentHov, 45f)) {
            g.FillPath(gb, back);
        }
        using (GraphicsPath front = Ui.Round(x, y, 150 * s, 150 * s, 34 * s)) {
            using (SolidBrush fb = new SolidBrush(Color.FromArgb(150, Theme.Accent))) g.FillPath(fb, front);
            using (Pen fp = new Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1f, 1.5f * s))) g.DrawPath(fp, front);
        }
        using (StringFormat sf = new StringFormat()) {
            sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; sf.FormatFlags = StringFormatFlags.NoWrap;
            using (Font f1 = new Font(Ui.Family, Math.Max(6f, 24f * s), FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font f2 = new Font(Ui.Family, Math.Max(8f, 44f * s), FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush w = new SolidBrush(Color.White)) {
                g.DrawString("OBJ", f1, w, new RectangleF(x + 8 * s, y + 18 * s, 134 * s, 44 * s), sf);
                g.DrawString("2X", f2, w, new RectangleF(x + 8 * s, y + 60 * s, 134 * s, 70 * s), sf);
            }
        }
    }
}

// 노란 경고 배너
public class BannerPanel : Panel, IRefresh {
    public BannerPanel() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.BnBg;
    }
    public void RefreshUi() { BackColor = Theme.BnBg; }
    protected override void OnPaintBackground(PaintEventArgs e) {
        Graphics g = e.Graphics;
        g.Clear(Parent != null && Parent.BackColor.A == 255 ? Parent.BackColor : Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float k = Height / 72f;
        using (GraphicsPath gp = Ui.Round(1, 1, Width - 3, Height - 3, 14 * k))
        using (SolidBrush b = new SolidBrush(Theme.BnBg))
        using (Pen p = new Pen(Theme.BnBorder, 2)) { g.FillPath(b, gp); g.DrawPath(p, gp); }
        using (Pen ip = new Pen(Theme.BnText, 1.8f * k)) {
            ip.LineJoin = LineJoin.Round;
            g.DrawPolygon(ip, new PointF[] { new PointF(34 * k, 24 * k), new PointF(49 * k, 50 * k), new PointF(19 * k, 50 * k) });
            g.DrawLine(ip, 34 * k, 32 * k, 34 * k, 41 * k);
            g.DrawLine(ip, 34 * k, 45 * k, 34 * k, 45.5f * k);
        }
    }
}

// 결과 표 (테마 색을 따르는 ListView)
public class ResList : ListView, IRefresh {
    public ResList() {
        View = View.Details; FullRowSelect = true; HideSelection = false; MultiSelect = false;
        OwnerDraw = true; BorderStyle = BorderStyle.FixedSingle;
        DoubleBuffered = true;
    }
    public void RefreshUi() { BackColor = Theme.Input; ForeColor = Theme.Text; Font = Ui.F(FR.Main); Invalidate(); }
    public void Init() {
        Columns.Clear();
        string[] keys = new string[] { "col_file", "col_vert", "col_tri", "col_mat", "col_time", "col_stat" };
        int[] w = new int[] { 260, 75, 75, 60, 70, 220 };
        HorizontalAlignment[] al = new HorizontalAlignment[] { HorizontalAlignment.Left, HorizontalAlignment.Right, HorizontalAlignment.Right, HorizontalAlignment.Right, HorizontalAlignment.Right, HorizontalAlignment.Left };
        for (int i = 0; i < keys.Length; i++) {
            ColumnHeader c = new ColumnHeader();
            c.Text = L.T(keys[i]); c.Width = w[i]; c.TextAlign = al[i];
            Columns.Add(c);
        }
    }
    protected override void ScaleControl(SizeF f, BoundsSpecified bs) {
        foreach (ColumnHeader c in Columns) c.Width = (int)Math.Round(c.Width * f.Width);
        base.ScaleControl(f, bs);
    }
    public void RetextColumns() {
        string[] keys = new string[] { "col_file", "col_vert", "col_tri", "col_mat", "col_time", "col_stat" };
        for (int i = 0; i < keys.Length && i < Columns.Count; i++) Columns[i].Text = L.T(keys[i]);
    }
    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) {
        using (SolidBrush b = new SolidBrush(Theme.Card)) e.Graphics.FillRectangle(b, e.Bounds);
        using (Pen p = new Pen(Theme.Divider, 1)) e.Graphics.DrawLine(p, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        if (e.Header.TextAlign == HorizontalAlignment.Right) fl |= TextFormatFlags.Right;
        TextRenderer.DrawText(e.Graphics, e.Header.Text, Ui.F(FR.Bold), new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height), Theme.Muted, fl);
    }
    protected override void OnDrawItem(DrawListViewItemEventArgs e) { e.DrawDefault = true; }
    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) { e.DrawDefault = true; }
}

// 링크처럼 보이는 라벨
public class LinkLbl : Label, IRefresh {
    public bool Bad;
    public LinkLbl() { AutoSize = true; BackColor = Color.Transparent; Cursor = Cursors.Hand; UseMnemonic = false; }
    public void RefreshUi() { Font = Ui.F(FR.Main); ForeColor = Theme.Accent; }
}
}
