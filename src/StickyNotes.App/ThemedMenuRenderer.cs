using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StickyNotes.App;

/// <summary>Tray ContextMenuStrip'i uygulama temasına (açık krem yüzey, sarı vurgu, Segoe UI) uydurur.</summary>
internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color Surface = Color.FromArgb(0xFB, 0xFA, 0xF6);
    private static readonly Color Line = Color.FromArgb(0xDE, 0xDA, 0xCD);
    private static readonly Color Hover = Color.FromArgb(0xFF, 0xF3, 0xC0);
    private static readonly Color Accent = Color.FromArgb(0xE3, 0xB2, 0x1F);
    private static readonly Color Ink = Color.FromArgb(0x2B, 0x2A, 0x28);

    public ThemedMenuRenderer() : base(new Table()) { RoundedEdges = false; }

    public static void Apply(ContextMenuStrip menu)
    {
        menu.Renderer = new ThemedMenuRenderer();
        menu.BackColor = Surface;
        menu.ForeColor = Ink;
        menu.Font = new Font("Segoe UI", 10f);
        menu.ShowImageMargin = false;
        menu.Padding = new Padding(4);
        foreach (ToolStripItem i in menu.Items)
        {
            if (i is ToolStripMenuItem) i.Padding = new Padding(2, 5, 2, 5);
        }
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(Surface);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var p = new Pen(Line);
        var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        e.Graphics.DrawRectangle(p, r);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        using var path = Round(r, 5);
        using var fill = new SolidBrush(Hover);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(Accent);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Ink : Color.Gray;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var p = new Pen(Line);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
    }

    private static GraphicsPath Round(Rectangle r, int radius)
    {
        var d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private sealed class Table : ProfessionalColorTable
    {
        public Table() { UseSystemColors = false; }
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemBorder => Accent;
        public override Color MenuBorder => Line;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Surface;
    }
}
