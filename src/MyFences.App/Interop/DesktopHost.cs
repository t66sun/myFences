using System.Windows;
using System.Windows.Interop;
using System.Text;

namespace MyFences.App.Interop;

internal static class DesktopHost
{
    public static bool IsExposed(Window window)
        => IsExposed(new WindowInteropHelper(window).Handle);

    private static bool IsExposed(nint handle)
    {
        if (!Native.IsWindowVisible(handle)) return false;
        Native.GetWindowRect(handle, out var bounds);
        var screen = new Point((bounds.Left + bounds.Right) / 2, (bounds.Top + bounds.Bottom) / 2);
        for (var current = handle; Native.GetParent(current) is var parent && parent != 0; current = parent)
        {
            var local = screen; Native.MapWindowPoints(0, parent, ref local, 1);
            if (Native.ChildWindowFromPointEx(parent, local, 7) != current) return false;
        }
        return true;
    }
    public static List<string> Describe()
    {
        var results = new List<string>();
        string DescribeWindow(nint handle)
        {
            var name = new StringBuilder(128); Native.GetClassName(handle, name, 128);
            Native.GetWindowRect(handle, out var rect);
            return $"{name} 0x{handle:X} parent=0x{Native.GetParent(handle):X} visible={Native.IsWindowVisible(handle)} exposed={IsExposed(handle)} dpi={Native.GetDpiForWindow(handle)} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom} style=0x{Native.GetWindowLong(handle, -16):X}";
        }
        Native.EnumWindows((hwnd, _) =>
        {
            var name = new StringBuilder(128); Native.GetClassName(hwnd, name, 128);
            if (name.ToString() is not ("Progman" or "WorkerW")) return true;
            results.Add(DescribeWindow(hwnd));
            Native.EnumChildWindows(hwnd, (child, _) =>
            { results.Add("  " + DescribeWindow(child)); return true; }, 0);
            return true;
        }, 0);
        return results;
    }
    public static nint FindHost()
    {
        var progman = Native.FindWindow("Progman", null);
        if (progman == 0) throw new InvalidOperationException("Windows Explorer desktop is unavailable.");
        nint host = 0;
        Native.EnumWindows((hwnd, _) =>
        {
            var view = Native.FindWindowEx(hwnd, 0, "SHELLDLL_DefView", null);
            if (view != 0 && Native.IsWindowVisible(view)) { host = view; return false; }
            return true;
        }, 0);
        // The wallpaper WorkerW can be covered by SHELLDLL_DefView. Keep
        // interactive groups inside the icon view, above its SysListView32.
        if (host == 0) throw new InvalidOperationException("The desktop window host could not be found.");
        return host;
    }

    public static void Attach(Window window, nint parent)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var style = (long)Native.GetWindowLong(handle, -16);
        Native.SetWindowLong(handle, -16, (nint)((style & ~0x80000000L) | 0x40000000L));
        Native.SetParent(handle, parent);
        if (Native.GetParent(handle) != parent) throw new InvalidOperationException("Desktop window attachment failed.");
        Native.SetWindowPos(handle, 0, 0, 0, 0, 0, 0x0033);
    }
}
