using System.Runtime.InteropServices;

namespace LiteDrops.UI;

/// <summary>
/// Borderless dark dialog with a title, a × button and a drop shadow. It can be moved by
/// dragging the title area and closed with Esc. Shown modeless next to the main window.
/// </summary>
abstract class DarkDialog : Form
{
    protected const int TitleHeight = 56;

    protected DarkDialog(string title, Size clientSize)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        KeyPreview = true;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        ClientSize = clientSize;
        Text = title;
        DoubleBuffered = true;

        var heading = new Label
        {
            Text = title, Font = Theme.Big, ForeColor = Theme.Text, AutoSize = true,
            Location = new Point(22, 16), BackColor = Theme.Card,
        };
        var close = new Label
        {
            Text = "×", Font = new Font("Segoe UI", 16f), ForeColor = Theme.SubText, BackColor = Theme.Card,
            TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
            Bounds = new Rectangle(clientSize.Width - 48, 10, 36, 36),
        };
        close.MouseEnter += (_, _) => close.ForeColor = Theme.Text;
        close.MouseLeave += (_, _) => close.ForeColor = Theme.SubText;
        close.Click += (_, _) => Close();
        heading.MouseDown += (_, e) => DragWindow(e);
        Controls.AddRange([heading, close, Divider(0, TitleHeight, clientSize.Width)]);
        ResumeLayout(false);
    }

    protected static Panel Divider(int x, int y, int width) => new()
    {
        BackColor = Theme.CardBorder, Bounds = new Rectangle(x, y, width, 1),
    };

    /// <summary>Shows the dialog modeless, roughly centred over <paramref name="owner"/>.</summary>
    public void ShowOver(Form owner)
    {
        var b = owner.Bounds;
        Location = new Point(b.X + (b.Width - Width) / 2, b.Y + Math.Min(120, Math.Max(0, (b.Height - Height) / 2)));
        Show(owner);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) Close();
    }

    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Lets the borderless dialog be moved by dragging its title area.</summary>
    protected void DragWindow(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Y < TitleHeight * DeviceDpi / 96) DragWindow(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ActiveControl = null; // no control highlighted until the keyboard is used
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.CardBorder);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }
}
