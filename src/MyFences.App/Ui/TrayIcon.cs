using System.Drawing;
using System.Drawing.Drawing2D;
using MyFences.App.Interop;

namespace MyFences.App.Ui;

internal static class TrayIcon
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(40, 95, 240));
            using var white = new SolidBrush(Color.White);
            graphics.FillRectangle(background, 2, 2, 28, 28);
            graphics.FillRectangle(white, 7, 8, 18, 3);
            graphics.FillRectangle(white, 7, 14, 8, 10);
            graphics.FillRectangle(white, 18, 14, 7, 10);
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { Native.DestroyIcon(handle); }
    }
}
