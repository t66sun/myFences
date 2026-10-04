using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyFences.App.Interop;

namespace MyFences.App.Ui;

internal static class ShellImages
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static ImageSource Get(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;
        var exists = File.Exists(path) || Directory.Exists(path);
        Native.SHGetFileInfo(path, 0x80, out var info, (uint)Marshal.SizeOf<Native.ShellFileInfo>(), 0x100u | (exists ? 0 : 0x10u));
        if (info.Icon == 0)
        {
            var fallback = Imaging.CreateBitmapSourceFromHIcon(System.Drawing.SystemIcons.Application.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            fallback.Freeze(); return fallback;
        }
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); Cache[path] = image; return image;
        }
        finally { Native.DestroyIcon(info.Icon); }
    }
}
