using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace RemapAKey;

/// <summary>Draws the tray icon at runtime (a keycap with swap arrows), so no binary asset is needed.</summary>
internal static class AppIcon
{
    public static Icon Create(bool active)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var body = active ? Color.FromArgb(37, 99, 235) : Color.FromArgb(120, 120, 128);
            using var path = RoundedRect(new RectangleF(1, 1, size - 2, size - 2), 7);
            using var fill = new SolidBrush(body);
            g.FillPath(fill, path);

            using var font = new Font("Segoe UI Symbol", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("⇄", font, Brushes.White, new RectangleF(0, 0, size, size + 1), format);
        }

        // The icon handle is intentionally never freed: there are only two, and they live as long as the app.
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
