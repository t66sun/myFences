using System.Runtime.InteropServices;
using System.Text;
using MyFences.Core;

namespace MyFences.App.Interop;

internal sealed record DesktopIcon(string Path, ItemKind Kind, int X, int Y);
internal sealed class DesktopShell : IDisposable
{
    private readonly List<object> _objects = [];
    private IFolderView2 _view = null!;
    private nint _desktopPidl;
    private nint _viewWindow;
    public nint ViewWindow => _viewWindow;
    public const uint ManagedFlags = 0x5;

    public DesktopShell()
    {
        try
        {
            var windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), true)!)!;
            _objects.Add(windows);
            object location = 0, root = Type.Missing;
            Check(((IShellWindows)windows).FindWindowSW(ref location, ref root, 8, out _, 1, out var dispatch));
            _objects.Add(dispatch);
            var service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
            var browserIid = typeof(IShellBrowser).GUID;
            Check(((IServiceProvider)dispatch).QueryService(ref service, ref browserIid, out var pointer));
            object browser;
            try { browser = Marshal.GetObjectForIUnknown(pointer); } finally { Marshal.Release(pointer); }
            _objects.Add(browser);
            Check(((IShellBrowser)browser).QueryActiveShellView(out var view));
            _objects.Add(view); _view = (IFolderView2)view;
            _viewWindow = Native.FindWindowEx(DesktopHost.FindHost(), 0, "SysListView32", null);
            if (_viewWindow == 0) throw new InvalidOperationException("The visible desktop icon window could not be found.");
            var folderIid = typeof(IShellFolder).GUID;
            Check(_view.GetFolder(ref folderIid, out pointer));
            object folder;
            try { folder = Marshal.GetObjectForIUnknown(pointer); } finally { Marshal.Release(pointer); }
            try { Check(Native.SHGetIDListFromObject(folder, out _desktopPidl)); }
            finally { Marshal.ReleaseComObject(folder); }
        }
        catch { Dispose(); throw; }
    }
    internal static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    public uint Flags { get { Check(_view.GetCurrentFolderFlags(out var flags)); return flags; } }
    public Point Spacing { get { Check(_view.GetSpacing(out var spacing)); return spacing; } }
    public Point ScreenToView(Point point) { Native.MapWindowPoints(0, _viewWindow, ref point, 1); return point; }
    public Point ViewToScreen(Point point) { Native.MapWindowPoints(_viewWindow, 0, ref point, 1); return point; }
    public static ScreenBounds WorkArea(Point screenPoint)
    {
        var bounds = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(screenPoint.X, screenPoint.Y)).WorkingArea;
        return new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }
    public List<ScreenBounds> OccupiedScreenAreas()
    {
        var spacing = Spacing; var bounds = WorkArea(ViewToScreen(new(0, 0))); var result = new List<ScreenBounds>();
        foreach (var position in IconPositions())
        {
            var point = ViewToScreen(position);
            var area = new ScreenBounds(point.X - 12, point.Y - 8, point.X + spacing.X, point.Y + spacing.Y);
            if (area.Intersects(bounds)) result.Add(area);
        }
        return result;
    }
    private List<Point> IconPositions()
    {
        Check(_view.ItemCount(2, out var count)); var result = new List<Point>();
        for (var index = 0; index < count; index++)
        {
            Check(_view.Item(index, out var child));
            try { Check(_view.GetItemPosition(child, out var point)); result.Add(point); }
            finally { if (child != 0) Native.ILFree(child); }
        }
        return result;
    }
    public bool HitTestIcon(Point point)
    {
        var spacing = Spacing;
        return IconPositions().Any(i => point.X >= i.X - 12 && point.X < i.X + spacing.X && point.Y >= i.Y - 8 && point.Y < i.Y + spacing.Y);
    }
    public void SetManagedFlags(uint flags) => Check(_view.SetCurrentFolderFlags(ManagedFlags, flags & ManagedFlags));
    public List<DesktopIcon> Enumerate()
    {
        Check(_view.ItemCount(2, out var count)); var result = new List<DesktopIcon>();
        for (var index = 0; index < count; index++)
        {
            Check(_view.Item(index, out var child));
            try
            {
                var absolute = Native.ILCombine(_desktopPidl, child);
                try
                {
                    var buffer = new StringBuilder(32768);
                    if (!Native.SHGetPathFromIDListEx(absolute, buffer, 32768, 0)) continue;
                    var path = buffer.ToString(); if (string.IsNullOrEmpty(path)) continue;
                    Check(_view.GetItemPosition(child, out var point));
                    var extension = Path.GetExtension(path).ToLowerInvariant();
                    var kind = Directory.Exists(path) ? ItemKind.Folder : extension is ".lnk" or ".url" or ".exe" or ".appref-ms" ? ItemKind.Shortcut : ItemKind.File;
                    result.Add(new(path, kind, point.X, point.Y));
                }
                finally { if (absolute != 0) Native.ILFree(absolute); }
            }
            finally { if (child != 0) Native.ILFree(child); }
        }
        return result;
    }
    public bool Position(string path, int x, int y)
    {
        Check(_view.ItemCount(2, out var count));
        for (var index = 0; index < count; index++)
        {
            Check(_view.Item(index, out var child));
            try
            {
                var absolute = Native.ILCombine(_desktopPidl, child);
                try
                {
                    var buffer = new StringBuilder(32768);
                    if (Native.SHGetPathFromIDListEx(absolute, buffer, 32768, 0) && string.Equals(buffer.ToString(), path, StringComparison.OrdinalIgnoreCase))
                    { Check(_view.SelectAndPositionItems(1, [child], [new(x, y)], 0x80008)); return true; }
                }
                finally { if (absolute != 0) Native.ILFree(absolute); }
            }
            finally { if (child != 0) Native.ILFree(child); }
        }
        return false;
    }
    public (object Folder, List<nint> Children)? GetMenuItems(IReadOnlyList<string> paths)
    {
        var wanted = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var children = new List<nint>();
        Check(_view.ItemCount(2, out var count));
        for (var index = 0; index < count; index++)
        {
            Check(_view.Item(index, out var child));
            var keep = false;
            try
            {
                var absolute = Native.ILCombine(_desktopPidl, child);
                try
                {
                    var buffer = new StringBuilder(32768);
                    if (Native.SHGetPathFromIDListEx(absolute, buffer, 32768, 0) && wanted.Contains(buffer.ToString())) { children.Add(child); keep = true; }
                }
                finally { if (absolute != 0) Native.ILFree(absolute); }
            }
            finally { if (!keep) Native.ILFree(child); }
        }
        if (children.Count != wanted.Count) { foreach (var child in children) Native.ILFree(child); return null; }
        var iid = typeof(IShellFolder).GUID;
        Check(_view.GetFolder(ref iid, out var pointer));
        try { return (Marshal.GetObjectForIUnknown(pointer), children); } finally { Marshal.Release(pointer); }
    }
    public void Dispose()
    {
        if (_desktopPidl != 0) { Native.ILFree(_desktopPidl); _desktopPidl = 0; }
        foreach (var obj in _objects.AsEnumerable().Reverse()) if (Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
        _objects.Clear();
    }

    public static bool IsBareDesktop(Point point)
    {
        var hwnd = Native.WindowFromPoint(point); var name = new StringBuilder(128);
        Native.GetClassName(hwnd, name, name.Capacity);
        return name.ToString() is "SysListView32" or "SHELLDLL_DefView" or "WorkerW" or "Progman";
    }
}
