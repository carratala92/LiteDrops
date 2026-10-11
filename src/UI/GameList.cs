using System.ComponentModel;

namespace LiteDrops.UI;

/// <summary>
/// Game list from the Canva mockup: each row has a drag handle, its priority number,
/// a checkbox and a delete button. Rows are reordered by dragging them.
/// </summary>
sealed class GameList : Control
{
    enum Part { None, Handle, Check, Name, Remove }

    IReadOnlyList<GameEntry> _items = [];
    readonly ToolTip _tip = new() { InitialDelay = 500 };
    readonly System.Windows.Forms.Timer _autoScroll = new() { Interval = 30 };
    int _scroll;           // pixels scrolled from the top
    int _hoverIndex = -1;
    Part _hoverPart;
    int _selected = -1;
    int _pressIndex = -1;
    Point _pressPoint;
    int _dragIndex = -1;   // row being dragged, -1 when not dragging
    int _dragY;            // mouse Y while dragging (client coordinates)
    int _dragGrabOffset;   // where in the row the mouse grabbed it
    bool _thumbDrag;
    int _thumbGrab;

    public event Action<GameEntry>? Toggled;
    public event Action<int, int>? Moved;
    public event Action<GameEntry>? RemoveRequested;

    public GameList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Card;
        Font = Theme.Body;
        TabStop = true;
        _autoScroll.Tick += (_, _) => AutoScrollTick();
    }

    float S => DeviceDpi / 96f;
    int RowHeight => (int)(38 * S);
    int Pitch => RowHeight + (int)(6 * S);
    int ContentHeight => _items.Count == 0 ? 0 : _items.Count * Pitch - (int)(6 * S);
    int MaxScroll => Math.Max(0, ContentHeight - Height);
    bool HasScrollbar => ContentHeight > Height;
    int ScrollbarWidth => HasScrollbar ? (int)(10 * S) : 0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<GameEntry> Items
    {
        get => _items;
        set
        {
            var selectedGame = _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;
            _items = value;
            _selected = selectedGame is null ? -1 : IndexOf(selectedGame);
            _scroll = Math.Clamp(_scroll, 0, MaxScroll);
            Invalidate();
        }
    }

    int IndexOf(GameEntry game)
    {
        for (var i = 0; i < _items.Count; i++)
            if (ReferenceEquals(_items[i], game)) return i;
        return -1;
    }

    /// <summary>Selects a game and scrolls it into view (used after adding one).</summary>
    public void Reveal(GameEntry game)
    {
        var index = IndexOf(game);
        if (index < 0) return;
        _selected = index;
        EnsureVisible(index);
        Invalidate();
    }

    void EnsureVisible(int index)
    {
        var top = index * Pitch;
        if (top < _scroll) _scroll = top;
        else if (top + RowHeight > _scroll + Height) _scroll = top + RowHeight - Height;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    Rectangle RowRect(int index, int? overrideTop = null) => new(
        0, overrideTop ?? index * Pitch - _scroll, Width - ScrollbarWidth - (HasScrollbar ? (int)(4 * S) : 0), RowHeight);

    // horizontal zones of a row, relative to its left edge
    (int HandleEnd, Rectangle Number, Rectangle Check, Rectangle Remove) Zones(Rectangle row)
    {
        var s = S;
        var numSize = (int)(26 * s);
        var number = new Rectangle(row.X + (int)(34 * s), row.Y + (row.Height - numSize) / 2, numSize, numSize);
        var checkSize = (int)(20 * s);
        var check = new Rectangle(number.Right + (int)(14 * s), row.Y + (row.Height - checkSize) / 2, checkSize, checkSize);
        var removeSize = (int)(24 * s);
        var remove = new Rectangle(row.Right - removeSize - (int)(10 * s), row.Y + (row.Height - removeSize) / 2, removeSize, removeSize);
        return (row.X + (int)(30 * s), number, check, remove);
    }

    (int Index, Part Part) HitTest(Point p)
    {
        if (p.X >= Width - ScrollbarWidth) return (-1, Part.None);
        var y = p.Y + _scroll;
        var index = y / Pitch;
        if (index < 0 || index >= _items.Count || y - index * Pitch > RowHeight) return (-1, Part.None);
        var row = RowRect(index);
        var z = Zones(row);
        if (z.Remove.Contains(p)) return (index, Part.Remove);
        if (Rectangle.Inflate(z.Check, (int)(6 * S), (int)(6 * S)).Contains(p)) return (index, Part.Check);
        if (p.X < z.HandleEnd) return (index, Part.Handle);
        return (index, Part.Name);
    }

    // ------------------------------------------------------------ painting

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, L.NoGames, Theme.Small, ClientRectangle,
                Theme.SubText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        for (var i = 0; i < _items.Count; i++)
        {
            if (i == _dragIndex) continue;
            var row = RowRect(i);
            if (row.Bottom < 0 || row.Top > Height) continue;
            DrawRow(g, i, row, lifted: false);
        }
        if (_dragIndex >= 0)
        {
            // insertion line where the row will land
            var insert = InsertIndex();
            var lineY = insert * Pitch - _scroll - (int)(3 * S);
            using (var pen = new Pen(Theme.Accent, 2.5f * S))
                g.DrawLine(pen, 12 * S, lineY, RowRect(0).Right - 12 * S, lineY);
            var top = Math.Clamp(_dragY - _dragGrabOffset, -RowHeight / 2, Height - RowHeight / 2);
            DrawRow(g, _dragIndex, RowRect(_dragIndex, top), lifted: true);
        }
        if (HasScrollbar)
        {
            var thumb = ThumbRect();
            Theme.FillRounded(g, Theme.Track, new RectangleF(Width - ScrollbarWidth + 3 * S, 0, 4 * S, Height), 2 * S);
            Theme.FillRounded(g, _thumbDrag ? Theme.Accent : Theme.SubText, thumb, 2 * S);
        }
    }

    RectangleF ThumbRect()
    {
        var trackH = (float)Height;
        var thumbH = Math.Max(24 * S, trackH * Height / ContentHeight);
        var y = MaxScroll == 0 ? 0 : (trackH - thumbH) * _scroll / MaxScroll;
        return new RectangleF(Width - ScrollbarWidth + 3 * S, y, 4 * S, thumbH);
    }

    void DrawRow(Graphics g, int index, Rectangle row, bool lifted)
    {
        var s = S;
        var game = _items[index];
        var hover = index == _hoverIndex && _dragIndex < 0;
        var rect = new RectangleF(row.X + 0.5f, row.Y + 0.5f, row.Width - 1, row.Height - 1);
        if (lifted)
        {
            Theme.FillRounded(g, Color.FromArgb(90, 0, 0, 0), rect with { X = rect.X + 3 * s, Y = rect.Y + 4 * s }, 8 * s);
        }
        var bg = lifted ? Theme.Track : hover || index == _selected && Focused ? Color.FromArgb(0x26, 0x26, 0x2C) : Theme.Input;
        Theme.FillRounded(g, bg, rect, 8 * s);
        Theme.DrawRounded(g, lifted ? Theme.Accent : Theme.CardBorder, rect, 8 * s, lifted ? 1.5f * s : 1f);

        var z = Zones(row);
        var dim = !game.Enabled;
        // drag handle: 2x3 dots
        using (var dot = new SolidBrush(hover && _hoverPart == Part.Handle || lifted ? Theme.Text : Theme.SubText))
        {
            var d = 3.6f * s;
            var cx = row.X + 13 * s;
            var cy = row.Y + row.Height / 2f;
            for (var r = -1; r <= 1; r++)
            for (var c = 0; c < 2; c++)
                g.FillEllipse(dot, cx + c * 6 * s - d / 2, cy + r * 6 * s - d / 2, d, d);
        }
        // priority number
        Theme.FillRounded(g, Theme.Card, z.Number, 6 * s);
        Theme.DrawRounded(g, Theme.CardBorder, z.Number, 6 * s);
        TextRenderer.DrawText(g, (index + 1).ToString(), Theme.Small, z.Number, dim ? Theme.SubText : Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        // checkbox
        if (game.Enabled)
        {
            Theme.FillRounded(g, Theme.Accent, z.Check, 4 * s);
            using var pen = new Pen(Color.White, 2 * s);
            var c = z.Check;
            g.DrawLines(pen, [
                new PointF(c.X + c.Width * 0.24f, c.Y + c.Height * 0.52f),
                new PointF(c.X + c.Width * 0.43f, c.Y + c.Height * 0.70f),
                new PointF(c.X + c.Width * 0.77f, c.Y + c.Height * 0.32f),
            ]);
        }
        else
        {
            Theme.DrawRounded(g, hover && _hoverPart == Part.Check ? Theme.Accent : Theme.SubText, z.Check, 4 * s, 1.5f * s);
        }
        // name
        var nameRect = new Rectangle(z.Check.Right + (int)(12 * s), row.Y, z.Remove.Left - z.Check.Right - (int)(18 * s), row.Height);
        TextRenderer.DrawText(g, game.Name, Font, nameRect, dim ? Theme.SubText : Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        // delete button
        var removeHot = hover && _hoverPart == Part.Remove;
        using (var circle = new SolidBrush(removeHot ? Theme.Red : Theme.Track))
            g.FillEllipse(circle, z.Remove);
        using (var pen = new Pen(removeHot ? Color.White : Theme.SubText, 1.8f * s))
        {
            var r = RectangleF.Inflate(z.Remove, -7.5f * s, -7.5f * s);
            g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
            g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
        }
    }

    // ------------------------------------------------------------ mouse

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        if (HasScrollbar && e.X >= Width - ScrollbarWidth)
        {
            var thumb = ThumbRect();
            if (!thumb.Contains(e.Location))
                SetScroll((int)((e.Y - thumb.Height / 2) / (Height - thumb.Height) * MaxScroll));
            _thumbDrag = true;
            _thumbGrab = (int)(e.Y - ThumbRect().Y);
            Capture = true;
            Invalidate();
            return;
        }
        var (index, part) = HitTest(e.Location);
        if (index < 0) return;
        _selected = index;
        switch (part)
        {
            case Part.Check:
                _items[index].Enabled = !_items[index].Enabled;
                Toggled?.Invoke(_items[index]);
                break;
            case Part.Remove:
                RemoveRequested?.Invoke(_items[index]);
                return;
            default:
                _pressIndex = index;
                _pressPoint = e.Location;
                break;
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_thumbDrag)
        {
            var thumbH = ThumbRect().Height;
            SetScroll((int)((e.Y - _thumbGrab) / Math.Max(1f, Height - thumbH) * MaxScroll));
            return;
        }
        if (_pressIndex >= 0 && _dragIndex < 0)
        {
            var drag = SystemInformation.DragSize;
            if (Math.Abs(e.Y - _pressPoint.Y) > drag.Height || Math.Abs(e.X - _pressPoint.X) > drag.Width)
            {
                _dragIndex = _pressIndex;
                _dragGrabOffset = _pressPoint.Y - RowRect(_dragIndex).Top;
                Capture = true;
                Cursor = Cursors.SizeAll;
                _tip.Hide(this);
                _autoScroll.Start();
            }
        }
        if (_dragIndex >= 0)
        {
            _dragY = e.Y;
            Invalidate();
            return;
        }
        var (index, part) = HitTest(e.Location);
        if (index != _hoverIndex || part != _hoverPart)
        {
            _hoverIndex = index;
            _hoverPart = part;
            Cursor = part switch
            {
                Part.Handle or Part.Name => Cursors.SizeAll,
                Part.Check or Part.Remove => Cursors.Hand,
                _ => Cursors.Default,
            };
            _tip.SetToolTip(this, part switch
            {
                Part.Handle or Part.Name => L.DragTip,
                Part.Check => L.CheckTip,
                Part.Remove => L.RemoveTip,
                _ => "",
            });
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_thumbDrag)
        {
            _thumbDrag = false;
            Capture = false;
            Invalidate();
            return;
        }
        if (_dragIndex >= 0)
        {
            var from = _dragIndex;
            var insert = InsertIndex();
            var to = insert > from ? insert - 1 : insert;
            EndDrag();
            if (to != from)
            {
                _selected = to;
                Moved?.Invoke(from, to);
            }
        }
        _pressIndex = -1;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragIndex >= 0) return;
        _hoverIndex = -1;
        _hoverPart = Part.None;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        SetScroll(_scroll - e.Delta / 120 * Pitch);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        if (_dragIndex >= 0) EndDrag();
        Invalidate();
    }

    void EndDrag()
    {
        _dragIndex = -1;
        _autoScroll.Stop();
        Capture = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    int InsertIndex()
    {
        var centre = _dragY - _dragGrabOffset + RowHeight / 2 + _scroll;
        return Math.Clamp((centre + Pitch / 2) / Pitch, 0, _items.Count);
    }

    void AutoScrollTick()
    {
        var edge = (int)(28 * S);
        var step = 0;
        if (_dragY < edge) step = -(int)(8 * S);
        else if (_dragY > Height - edge) step = (int)(8 * S);
        if (step != 0) SetScroll(_scroll + step);
    }

    void SetScroll(int value)
    {
        var clamped = Math.Clamp(value, 0, MaxScroll);
        if (clamped == _scroll) return;
        _scroll = clamped;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    // ------------------------------------------------------------ keyboard

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Space or Keys.Delete || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_items.Count == 0) return;
        if (_selected < 0) _selected = 0;
        switch (e.KeyCode)
        {
            case Keys.Up when e.Alt && _selected > 0:
                Moved?.Invoke(_selected, _selected - 1);
                _selected--;
                break;
            case Keys.Down when e.Alt && _selected < _items.Count - 1:
                Moved?.Invoke(_selected, _selected + 1);
                _selected++;
                break;
            case Keys.Up:
                _selected = Math.Max(0, _selected - 1);
                break;
            case Keys.Down:
                _selected = Math.Min(_items.Count - 1, _selected + 1);
                break;
            case Keys.Space:
                _items[_selected].Enabled = !_items[_selected].Enabled;
                Toggled?.Invoke(_items[_selected]);
                break;
            case Keys.Delete:
                RemoveRequested?.Invoke(_items[_selected]);
                return;
            default:
                return;
        }
        e.Handled = true;
        EnsureVisible(_selected);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tip.Dispose();
            _autoScroll.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Search field with a magnifier icon and a clear (×) button.</summary>
sealed class SearchBox : Panel
{
    readonly Label _clear = new()
    {
        Text = "×", Font = new Font("Segoe UI", 13f), ForeColor = Theme.SubText, BackColor = Theme.Input,
        TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Visible = false,
    };
    readonly ToolTip _tip = new();

    public TextBox Box { get; } = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Body,
    };

    public SearchBox()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Card;
        Controls.Add(Box);
        Controls.Add(_clear);
        _tip.SetToolTip(_clear, L.Clear);
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Box.TextChanged += (_, _) => _clear.Visible = Box.TextLength > 0;
        _clear.MouseEnter += (_, _) => _clear.ForeColor = Theme.Text;
        _clear.MouseLeave += (_, _) => _clear.ForeColor = Theme.SubText;
        _clear.Click += (_, _) =>
        {
            Box.Clear();
            Box.Focus();
        };
        Click += (_, _) => Box.Focus();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        var s = DeviceDpi / 96f;
        var left = (int)(34 * s);
        var clearW = (int)(28 * s);
        _clear.SetBounds(Width - clearW - (int)(4 * s), (int)(3 * s), clearW, Height - (int)(6 * s));
        Box.SetBounds(left, (Height - Box.Height) / 2, _clear.Left - left - (int)(2 * s), Box.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var s = DeviceDpi / 96f;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Theme.FillRounded(g, Theme.Input, r, 7 * s);
        Theme.DrawRounded(g, Box.Focused ? Theme.Accent : Theme.CardBorder, r, 7 * s);
        // magnifier
        using var pen = new Pen(Theme.SubText, 1.8f * s);
        var d = 11 * s;
        var x = 12 * s;
        var y = Height / 2f - d / 2 - 1.5f * s;
        g.DrawEllipse(pen, x, y, d, d);
        g.DrawLine(pen, x + d * 0.85f, y + d * 0.85f, x + d * 0.85f + 4.5f * s, y + d * 0.85f + 4.5f * s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Dropdown of Twitch game names under the search box.</summary>
sealed class SuggestionList : ListBox
{
    /// <summary>Names already in the user's list, shown as "Added".</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, bool> IsAdded { get; set; } = _ => false;

    int _hover = -1;

    public SuggestionList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Theme.Input;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        ItemHeight = 28;
        IntegralHeight = false;
        Cursor = Cursors.Hand;
        NativeTheme.Dark(this);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexFromPoint(e.Location);
        if (index != _hover)
        {
            _hover = index;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var name = (string)Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) != 0;
        var color = selected ? Theme.AccentDim : e.Index == _hover ? Color.FromArgb(0x2A, 0x2A, 0x30) : Theme.Input;
        using (var bg = new SolidBrush(color))
            e.Graphics.FillRectangle(bg, e.Bounds);
        var s = DeviceDpi / 96f;
        var pad = (int)(10 * s);
        var added = IsAdded(name);
        var tagW = added ? TextRenderer.MeasureText(L.Added, Theme.Small).Width + (int)(4 * s) : 0;
        var r = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, e.Bounds.Width - pad * 2 - tagW, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, name, Font, r, added && !selected ? Theme.SubText : Theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (added)
            TextRenderer.DrawText(e.Graphics, L.Added, Theme.Small,
                new Rectangle(e.Bounds.Right - tagW - pad, e.Bounds.Y, tagW, e.Bounds.Height),
                selected ? Color.White : Theme.Green, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }
}

/// <summary>"Removed “X” · Undo" notice; clicking it restores the game.</summary>
sealed class UndoBar : Control
{
    string _message = "";

    public UndoBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Theme.Card;
        Cursor = Cursors.Hand;
        Visible = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            Text = $"{value} · {L.Undo}"; // for screen readers
            var s = DeviceDpi / 96f;
            var w = TextRenderer.MeasureText(_message + "  ·  ", Theme.Small).Width
                    + TextRenderer.MeasureText(L.Undo, Theme.BodyBold).Width;
            Size = new Size(w, (int)(22 * s));
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding;
        var text = _message + "  ·  ";
        TextRenderer.DrawText(e.Graphics, text, Theme.Small, ClientRectangle, Theme.SubText, flags);
        var x = TextRenderer.MeasureText(e.Graphics, text, Theme.Small, Size, flags).Width;
        TextRenderer.DrawText(e.Graphics, L.Undo, Theme.BodyBold, ClientRectangle with { X = x, Width = Width - x },
            Theme.Accent, flags);
    }
}
