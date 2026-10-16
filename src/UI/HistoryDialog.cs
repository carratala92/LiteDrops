using System.ComponentModel;

namespace LiteDrops.UI;

/// <summary>"Claim history" dialog from the Canva mockup.</summary>
sealed class HistoryDialog : DarkDialog
{
    readonly HistoryList _list = new();
    readonly Label _count = new()
    {
        Font = Theme.Small, ForeColor = Theme.SubText, BackColor = Theme.Card, AutoSize = true,
    };
    readonly Label _empty = new()
    {
        Font = Theme.Small, ForeColor = Theme.SubText, BackColor = Theme.Card, Text = L.NoHistory,
        TextAlign = ContentAlignment.MiddleCenter,
    };

    public HistoryDialog(ClaimHistory history) : base(L.ClaimHistory, new Size(420, 500))
    {
        SuspendLayout();
        _list.Bounds = new Rectangle(14, 64, 400, 352);
        _empty.Bounds = _list.Bounds;
        Controls.Add(Divider(0, 424, 420));
        _count.Location = new Point(22, 452);
        var close = new AccentButton { Text = L.Close, Bounds = new Rectangle(298, 442, 100, 40), BackColor = Theme.Card };
        close.Click += (_, _) => Close();
        Controls.AddRange([_list, _empty, _count, close]);
        ResumeLayout(false);
        Refresh(history);
    }

    public void Refresh(ClaimHistory history)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var record in history.Records) _list.Items.Add(record);
        _list.EndUpdate();
        _empty.Visible = history.Records.Count == 0;
        _list.Visible = !_empty.Visible;
        _count.Text = L.ClaimedCount(history.Records.Count);
    }

    sealed class HistoryList : ListBox
    {
        public HistoryList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Card;
            ItemHeight = 58;
            IntegralHeight = false;
            SelectionMode = SelectionMode.None;
            NativeTheme.Dark(this);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var g = e.Graphics;
            var record = (ClaimRecord)Items[e.Index];
            var b = e.Bounds;
            var s = DeviceDpi / 96f;
            using (var bg = new SolidBrush(Theme.Card))
                g.FillRectangle(bg, b);
            Theme.Smooth(g);
            // green check in a circle
            var d = 22 * s;
            var circle = new RectangleF(b.X + 8 * s, b.Y + (b.Height - d) / 2, d, d);
            using (var pen = new Pen(Theme.Green, 1.8f * s))
            {
                g.DrawEllipse(pen, circle);
                g.DrawLines(pen, [
                    new PointF(circle.X + d * 0.28f, circle.Y + d * 0.52f),
                    new PointF(circle.X + d * 0.44f, circle.Y + d * 0.67f),
                    new PointF(circle.X + d * 0.73f, circle.Y + d * 0.36f),
                ]);
            }
            var x = (int)(circle.Right + 14 * s);
            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, record.Drop, Theme.BodyBold,
                new Rectangle(x, b.Y + (int)(9 * s), b.Right - x - (int)(8 * s), (int)(22 * s)), Theme.Text, flags);
            var local = record.ClaimedAt.ToLocalTime();
            var when = L.Ja ? local.ToString("M/d HH:mm") : local.ToString("MMM d HH:mm");
            TextRenderer.DrawText(g, $"{record.Game} · {when}", Theme.Small,
                new Rectangle(x, b.Y + (int)(31 * s), b.Right - x - (int)(8 * s), (int)(20 * s)), Theme.SubText, flags);
            if (e.Index < Items.Count - 1)
            {
                using var line = new Pen(Theme.CardBorder);
                g.DrawLine(line, x, b.Bottom - 1, b.Right - 8 * s, b.Bottom - 1);
            }
        }
    }
}

/// <summary>Small accent-colored text that acts as a link.</summary>
sealed class TextLink : Control
{
    bool _hover;

    public TextLink()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
        Font = Theme.Small;
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitSize(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitSize(); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    void FitSize() => Size = TextRenderer.MeasureText(Text, Font) + new Size(2, 2);

    protected override void OnPaint(PaintEventArgs e)
    {
        using var font = _hover ? new Font(Font, FontStyle.Underline) : null;
        TextRenderer.DrawText(e.Graphics, Text, font ?? Font, ClientRectangle, _hover ? Theme.Text : Theme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>Outlined pill with text and a chevron ("Update available: v1.5.0 ›").</summary>
sealed class PillLink : Control
{
    Color _color = Theme.Accent;
    bool _hover;

    public PillLink()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
        Font = Theme.BodyBold;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Color { get => _color; set { _color = value; Invalidate(); } }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        var s = DeviceDpi / 96f;
        Size = new Size(TextRenderer.MeasureText(Text, Font).Width + (int)(26 * s), (int)(28 * s));
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e) =>
        PaintPill(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Text, Font, _color, _hover, DeviceDpi / 96f);

    public static void PaintPill(Graphics g, RectangleF r, string text, Font font, Color color, bool hover, float s)
    {
        Theme.Smooth(g);
        Theme.FillRounded(g, Color.FromArgb(hover ? 60 : 28, color), r, 8 * s);
        Theme.DrawRounded(g, Color.FromArgb(200, color), r, 8 * s, 1.2f * s);
        TextRenderer.DrawText(g, text, font, Rectangle.Round(r), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
