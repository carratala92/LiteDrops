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

/// <summary>Text box with a rounded dark frame.</summary>
sealed class DarkTextBox : Panel
{
    public TextBox Box { get; } = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Body,
    };

    public DarkTextBox()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Card;
        Controls.Add(Box);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var s = DeviceDpi / 96f;
        var pad = (int)(10 * s);
        Box.SetBounds(pad, (Height - Box.Height) / 2, Width - pad * 2, Box.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var radius = 7 * DeviceDpi / 96f;
        Theme.FillRounded(g, Theme.Input, r, radius);
        Theme.DrawRounded(g, Box.Focused ? Theme.Accent : Theme.CardBorder, r, radius);
    }
}

/// <summary>Game list with checkboxes; the order is the mining priority.</summary>
sealed class GameListBox : ListBox
{
    public event Action? SelectionChanged;

    public GameListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        ItemHeight = 28;
        IntegralHeight = false;
        NativeTheme.Dark(this);
    }

    int BoxSize => (int)(18 * DeviceDpi / 96f);

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var g = e.Graphics;
        var game = (GameEntry)Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? Theme.Input : Theme.Card))
            g.FillRectangle(bg, e.Bounds);
        Theme.Smooth(g);
        var s = DeviceDpi / 96f;
        var box = new RectangleF(e.Bounds.X + 2 * s, e.Bounds.Y + (e.Bounds.Height - BoxSize) / 2f, BoxSize, BoxSize);
        if (game.Enabled)
        {
            Theme.FillRounded(g, Theme.Accent, box, 4 * s);
            using var pen = new Pen(Color.White, 2 * s);
            g.DrawLines(pen, [
                new PointF(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f),
                new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.70f),
                new PointF(box.X + box.Width * 0.77f, box.Y + box.Height * 0.32f),
            ]);
        }
        else
        {
            Theme.DrawRounded(g, Theme.SubText, box, 4 * s, 1.5f * s);
        }
        var textRect = new Rectangle((int)(box.Right + 10 * s), e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);
        TextRenderer.DrawText(g, game.Name, Font, textRect, game.Enabled ? Theme.Text : Theme.SubText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var index = IndexFromPoint(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0 && e.X < BoxSize + 30 * DeviceDpi / 96f)
            Toggle(index);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var index = IndexFromPoint(e.Location);
        if (index >= 0 && e.X >= BoxSize + 30 * DeviceDpi / 96f) Toggle(index);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space && SelectedIndex >= 0) Toggle(SelectedIndex);
    }

    void Toggle(int index)
    {
        var game = (GameEntry)Items[index];
        game.Enabled = !game.Enabled;
        Invalidate(GetItemRectangle(index));
        SelectionChanged?.Invoke();
    }
}

/// <summary>Game name suggestions shown while typing in the "Add game" box.</summary>
sealed class SuggestionListBox : ListBox
{
    public SuggestionListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Input;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        ItemHeight = 26;
        IntegralHeight = false;
        Cursor = Cursors.Hand;
        NativeTheme.Dark(this);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? Theme.AccentDim : Theme.Input))
            e.Graphics.FillRectangle(bg, e.Bounds);
        var pad = (int)(8 * DeviceDpi / 96f);
        var r = e.Bounds with { X = e.Bounds.X + pad, Width = e.Bounds.Width - pad };
        TextRenderer.DrawText(e.Graphics, (string)Items[e.Index], Font, r, Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
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
