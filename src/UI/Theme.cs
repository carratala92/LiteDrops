using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LiteDrops.UI;

/// <summary>Colors and helpers for the dark look from the Canva mockup.</summary>
static class Theme
{
    public static readonly Color Background = Color.FromArgb(0x0E, 0x0E, 0x10);
    public static readonly Color Card = Color.FromArgb(0x18, 0x18, 0x1B);
    public static readonly Color CardBorder = Color.FromArgb(0x2C, 0x2C, 0x32);
    public static readonly Color Input = Color.FromArgb(0x1F, 0x1F, 0x23);
    public static readonly Color Accent = Color.FromArgb(0x91, 0x47, 0xFF);
    public static readonly Color AccentDim = Color.FromArgb(0x77, 0x2C, 0xE8);
    public static readonly Color Track = Color.FromArgb(0x2A, 0x2A, 0x30);
    public static readonly Color Text = Color.FromArgb(0xEF, 0xEF, 0xF1);
    public static readonly Color SubText = Color.FromArgb(0xAD, 0xAD, 0xB8);
    public static readonly Color Green = Color.FromArgb(0x2E, 0xCC, 0x71);
    public static readonly Color Amber = Color.FromArgb(0xF5, 0xB7, 0x31);
    public static readonly Color Red = Color.FromArgb(0xEB, 0x4D, 0x4B);

    const string Family = "Segoe UI";
    public static readonly Font Title = new(Family + " Semibold", 17f);
    public static readonly Font Heading = new(Family + " Semibold", 10.5f);
    public static readonly Font Big = new(Family + " Semibold", 14f);
    public static readonly Font Body = new(Family, 10f);
    public static readonly Font BodyBold = new(Family + " Semibold", 10f);
    public static readonly Font Small = new(Family, 9f);
    public static readonly Font Code = new("Consolas", 22f, FontStyle.Bold);

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void DrawRounded(Graphics g, Color color, RectangleF r, float radius, float width = 1f)
    {
        using var path = RoundedRect(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Dark title bar and caption color matching the window background.</summary>
    public static void ApplyDarkTitleBar(IntPtr hwnd)
    {
        var on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        var caption = ColorTranslator.ToWin32(Background);
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
    }
}
