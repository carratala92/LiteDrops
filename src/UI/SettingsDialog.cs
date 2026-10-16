using System.ComponentModel;
using System.Diagnostics;

namespace LiteDrops.UI;

/// <summary>Settings dialog from the Canva mockup: autostart, tray, badges and language.</summary>
sealed class SettingsDialog : DarkDialog
{
    const string RepoUrl = "https://github.com/naoki6310/LiteDrops";

    readonly AppSettings _settings;
    readonly ToggleSwitch _autostart = new();
    readonly ToggleSwitch _closeToTray = new();
    readonly ToggleSwitch _mineBadges = new();
    readonly SegmentedControl _language = new(["auto", "en", "ja"], [L.LanguageAutoShort, "English", "日本語"]);

    /// <summary>The language picked in the dialog, applied by the caller (it needs a restart).</summary>
    public string Language => _language.Value;

    /// <summary>Raised when a setting the miner uses has changed.</summary>
    public event Action? MiningSettingsChanged;

    public SettingsDialog(AppSettings settings) : base(L.Settings, new Size(420, 526))
    {
        _settings = settings;
        SuspendLayout();
        _autostart.Checked = AutoStart.IsEnabled;
        _autostart.CheckedChanged += (_, _) => AutoStart.Set(_autostart.Checked);
        _closeToTray.Checked = settings.CloseToTray;
        _closeToTray.CheckedChanged += (_, _) =>
        {
            _settings.CloseToTray = _closeToTray.Checked;
            _settings.Save();
        };
        _mineBadges.Checked = settings.MineBadges;
        _mineBadges.CheckedChanged += (_, _) =>
        {
            _settings.MineBadges = _mineBadges.Checked;
            _settings.Save();
            MiningSettingsChanged?.Invoke();
        };
        _language.Value = settings.Language;
        AddRow(57, L.StartWithWindows, L.StartWithWindowsDesc, _autostart);
        Controls.Add(Divider(22, 142, 376));
        AddRow(143, L.CloseToTray, L.CloseToTrayDesc, _closeToTray);
        Controls.Add(Divider(22, 228, 376));
        AddRow(229, L.MineBadges, L.MineBadgesDesc, _mineBadges);
        Controls.Add(Divider(22, 314, 376));
        AddRow(315, L.Language, L.LanguageDesc, _language);
        Controls.Add(Divider(0, 400, 420));

        var version = new Label
        {
            Text = $"LiteDrops v{UpdateChecker.CurrentVersion}", Font = Theme.Small, ForeColor = Theme.SubText,
            AutoSize = true, Location = new Point(22, 416), BackColor = Theme.Card,
        };
        var github = new TextLink { Text = "GitHub", Font = Theme.Small, BackColor = Theme.Card };
        github.Click += (_, _) => Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        var done = new AccentButton
        {
            Text = L.Done, Bounds = new Rectangle(298, 464, 100, 40), BackColor = Theme.Card,
        };
        done.Click += (_, _) => Close();

        Controls.AddRange([version, github, done]);
        ResumeLayout(false);
        github.Location = new Point(ClientSize.Width - github.Width - 22, version.Top);
    }

    void AddRow(int top, string label, string description, Control control)
    {
        const int rowH = 85;
        var textW = 398 - control.Width - 22 - 16;
        var name = new Label
        {
            Text = label, Font = Theme.BodyBold, ForeColor = Theme.Text, BackColor = Theme.Card,
            Bounds = new Rectangle(22, top + 18, textW, 24), AutoEllipsis = true,
        };
        var desc = new Label
        {
            Text = description, Font = Theme.Small, ForeColor = Theme.SubText, BackColor = Theme.Card,
            Bounds = new Rectangle(22, top + 44, textW, 34), AutoEllipsis = true,
        };
        control.Location = new Point(398 - control.Width, top + (rowH - control.Height) / 2);
        Controls.AddRange([name, desc, control]);
    }
}

/// <summary>Pill-shaped on/off switch.</summary>
sealed class ToggleSwitch : Control
{
    bool _checked;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Size = new Size(48, 26);
        BackColor = Theme.Card;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) Checked = !Checked;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        Theme.FillRounded(g, _checked ? Theme.Accent : Theme.Track, r, r.Height / 2);
        if (Focused && ShowFocusCues) Theme.DrawRounded(g, Theme.AccentDim, r, r.Height / 2, 2f);
        var knob = r.Height - 6;
        var x = _checked ? r.Right - knob - 3 : r.X + 3;
        using var brush = new SolidBrush(_checked ? Color.White : Theme.SubText);
        g.FillEllipse(brush, x, r.Y + 3, knob, knob);
    }
}

/// <summary>Row of mutually exclusive options ("Auto | English | 日本語").</summary>
sealed class SegmentedControl : Control
{
    readonly string[] _values;
    readonly string[] _labels;
    int _selected;
    int _hover = -1;

    public SegmentedControl(string[] values, string[] labels)
    {
        _values = values;
        _labels = labels;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Theme.Card;
        Font = Theme.Small;
        Cursor = Cursors.Hand;
        Size = new Size(190, 32);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => _values[_selected];
        set
        {
            var index = Array.IndexOf(_values, value);
            _selected = index < 0 ? 0 : index;
            Invalidate();
        }
    }

    RectangleF Segment(int i)
    {
        var w = (Width - 2f) / _values.Length;
        return new RectangleF(1 + i * w, 1, w, Height - 2);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hover = Math.Clamp((int)(e.X / ((Width - 2f) / _values.Length)), 0, _values.Length - 1);
        if (hover != _hover) { _hover = hover; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _selected = Math.Clamp((int)(e.X / ((Width - 2f) / _values.Length)), 0, _values.Length - 1);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Theme.Smooth(g);
        var s = DeviceDpi / 96f;
        var outer = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        Theme.FillRounded(g, Theme.Input, outer, 7 * s);
        Theme.DrawRounded(g, Theme.CardBorder, outer, 7 * s);
        for (var i = 0; i < _values.Length; i++)
        {
            var seg = RectangleF.Inflate(Segment(i), -2 * s, -2 * s);
            if (i == _selected) Theme.FillRounded(g, Theme.Accent, seg, 5 * s);
            else if (i == _hover) Theme.FillRounded(g, Theme.Track, seg, 5 * s);
            TextRenderer.DrawText(g, _labels[i], Font, Rectangle.Round(seg),
                i == _selected ? Color.White : Theme.SubText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
