using System.ComponentModel;
using System.Runtime.InteropServices;
using LiteDrops.Mining;

namespace LiteDrops.UI;

static class NativeTheme
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    /// <summary>Dark scrollbars for list boxes and text boxes.</summary>
    public static void Dark(Control control) =>
        control.HandleCreated += (_, _) => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
}

/// <summary>Rounded dark card with an accent-colored heading.</summary>
sealed class CardPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Heading { get; set; } = "";

    string _hint = "";

    /// <summary>Small grey text on the right of the heading.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Hint { get => _hint; set { _hint = value; Invalidate(); } }

    public CardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Background;
        Padding = new Padding(16, 40, 16, 12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var s = DeviceDpi / 96f;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRounded(g, Theme.Card, r, 10 * s);
        Theme.DrawRounded(g, Theme.CardBorder, r, 10 * s);
        if (Heading.Length > 0)
            TextRenderer.DrawText(g, Heading, Theme.Heading, new Point((int)(16 * s), (int)(12 * s)), Theme.Accent);
        if (_hint.Length > 0)
            TextRenderer.DrawText(g, _hint, Theme.Small,
                new Rectangle(0, (int)(13 * s), Width - (int)(16 * s), (int)(20 * s)), Theme.SubText,
                TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>"● Mining" style status badge.</summary>
sealed class StatusPill : Control
{
    Color _color = Theme.SubText;

    public StatusPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Theme.Background;
        Font = Theme.BodyBold;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DotColor { get => _color; set { _color = value; Invalidate(); } }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var s = DeviceDpi / 96f;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRounded(g, Color.FromArgb(28, _color), r, 8 * s);
        Theme.DrawRounded(g, Color.FromArgb(110, _color), r, 8 * s);
        var dot = 9 * s;
        using (var brush = new SolidBrush(_color))
            g.FillEllipse(brush, 12 * s, (Height - dot) / 2, dot, dot);
        var textRect = new Rectangle((int)(28 * s), 0, Width - (int)(34 * s), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, _color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

sealed class SlimProgress : Control
{
    double _value;

    public SlimProgress()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Theme.Card;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Value { get => _value; set { _value = Math.Clamp(value, 0, 1); Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e) =>
        PaintBar(e.Graphics, new RectangleF(0, 0, Width, Height), _value, DeviceDpi / 96f);

    public static void PaintBar(Graphics g, RectangleF r, double value, float s)
    {
        Theme.Smooth(g);
        Theme.FillRounded(g, Theme.Track, r, r.Height / 2);
        if (value > 0)
            Theme.FillRounded(g, Theme.Accent, r with { Width = Math.Max(r.Height, (float)(r.Width * value)) }, r.Height / 2);
    }
}

sealed class AccentButton : Control
{
    bool _hover;

    public AccentButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Theme.Card;
        Font = Theme.BodyBold;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var color = !Enabled ? Theme.Track : _hover ? Theme.AccentDim : Theme.Accent;
        Theme.FillRounded(g, color, new RectangleF(0, 0, Width - 1, Height - 1), 7 * DeviceDpi / 96f);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? Color.White : Theme.SubText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

sealed class CampaignListBox : ListBox
{
    public CampaignListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        ItemHeight = 32;
        IntegralHeight = false;
        SelectionMode = SelectionMode.None;
        NativeTheme.Dark(this);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var g = e.Graphics;
        var row = (CampaignRow)Items[e.Index];
        using (var bg = new SolidBrush(Theme.Card))
            g.FillRectangle(bg, e.Bounds);
        var s = DeviceDpi / 96f;
        var b = e.Bounds;
        var gameW = (int)(b.Width * 0.27f);
        var nameW = (int)(b.Width * 0.38f);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
        var textColor = row.Earnable ? Theme.Text : Theme.SubText;
        TextRenderer.DrawText(g, row.GameName, Theme.BodyBold, new Rectangle(b.X, b.Y, gameW - (int)(6 * s), b.Height), textColor, flags);
        TextRenderer.DrawText(g, row.CampaignName, Theme.Small, new Rectangle(b.X + gameW, b.Y, nameW - (int)(8 * s), b.Height), Theme.SubText, flags);
        var pctW = (int)(44 * s);
        var barX = b.X + gameW + nameW;
        var barW = b.Right - pctW - barX - (int)(6 * s);
        var barH = 8 * s;
        if (barW > 10)
            SlimProgress.PaintBar(g, new RectangleF(barX, b.Y + (b.Height - barH) / 2, barW, barH), row.Progress, s);
        TextRenderer.DrawText(g, $"{row.Progress:0%}", Theme.BodyBold, new Rectangle(b.Right - pctW, b.Y, pctW, b.Height),
            Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }
}
