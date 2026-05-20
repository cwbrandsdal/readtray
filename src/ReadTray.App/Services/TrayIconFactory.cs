using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace ReadTray.App.Services;

public static class TrayIconFactory
{
    public static Icon Create()
    {
        var size = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var scale = size / 16f;
        using var background = new SolidBrush(Color.FromArgb(255, 18, 22, 29));
        using var documentPen = new Pen(Color.FromArgb(255, 245, 248, 252), Math.Max(1.3f, 1.45f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var textPen = new Pen(Color.FromArgb(255, 245, 248, 252), Math.Max(1.0f, 1.05f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var accent = new Pen(Color.FromArgb(255, 38, 224, 232), Math.Max(1.3f, 1.45f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var accent2 = new Pen(Color.FromArgb(255, 78, 143, 255), Math.Max(1.1f, 1.25f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        graphics.FillRoundedRectangle(background, new RectangleF(0.5f * scale, 0.5f * scale, 15f * scale, 15f * scale), 3.5f * scale);

        using var documentPath = new GraphicsPath();
        documentPath.AddLine(4.0f * scale, 4.0f * scale, 9.0f * scale, 4.0f * scale);
        documentPath.AddLine(9.0f * scale, 4.0f * scale, 10.5f * scale, 5.5f * scale);
        documentPath.AddLine(10.5f * scale, 5.5f * scale, 10.5f * scale, 11.4f * scale);
        documentPath.AddLine(10.5f * scale, 11.4f * scale, 8.0f * scale, 11.4f * scale);
        documentPath.AddLine(8.0f * scale, 11.4f * scale, 6.3f * scale, 13.0f * scale);
        documentPath.AddLine(6.3f * scale, 13.0f * scale, 6.3f * scale, 11.4f * scale);
        documentPath.AddLine(6.3f * scale, 11.4f * scale, 4.0f * scale, 11.4f * scale);
        documentPath.CloseFigure();
        graphics.DrawPath(documentPen, documentPath);

        graphics.DrawLine(textPen, 5.2f * scale, 6.5f * scale, 8.2f * scale, 6.5f * scale);
        graphics.DrawLine(accent, 5.2f * scale, 8.4f * scale, 8.8f * scale, 8.4f * scale);
        graphics.DrawArc(accent, 10.1f * scale, 6.4f * scale, 2.1f * scale, 3.4f * scale, -47, 94);
        graphics.DrawArc(accent2, 11.1f * scale, 5.1f * scale, 3.0f * scale, 6.0f * scale, -47, 94);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
