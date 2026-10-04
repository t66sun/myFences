using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MyFences.App.Interop;

internal static class ShellMenu
{
    public static void Show(nint hwnd, IReadOnlyList<string> paths, string removeLabel, string resumeLabel, Action remove, Action resume)
    {
        if (paths.Count == 0) return;
        var absolute = new List<nint>();
        var children = new List<nint>();
        var ownedChildren = new List<nint>();
        object? folderObject = null, menuObject = null;
        nint menu = 0;
        HwndSource? source = null;
        HwndSourceHook? hook = null;
        try
        {
            foreach (var path in paths)
            { DesktopShell.Check(Native.SHParseDisplayName(path, 0, out var pidl, 0, out _)); absolute.Add(pidl); }
            // Desktop selection may span the user and public desktop directories.
            // These are immediate items of the merged desktop Shell folder.
            using var desktop = new DesktopShell();
            var desktopItems = desktop.GetMenuItems(paths);
            if (desktopItems is not null)
            {
                folderObject = desktopItems.Value.Folder;
                children.AddRange(desktopItems.Value.Children);
                ownedChildren.AddRange(desktopItems.Value.Children);
            }
            else
            {
                var parentIid = typeof(IShellFolder).GUID;
                DesktopShell.Check(Native.SHBindToParent(absolute[0], ref parentIid, out var folderPointer, out var firstChild));
                try { folderObject = Marshal.GetObjectForIUnknown(folderPointer); } finally { Marshal.Release(folderPointer); }
                var parent = Path.GetDirectoryName(paths[0]);
                if (paths.Any(p => !string.Equals(Path.GetDirectoryName(p), parent, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Select items in the same source folder for a combined Windows menu. / 组合系统菜单请选择同一来源文件夹的项目。");
                children.AddRange(absolute.Select(Native.ILFindLastID));
            }
            var iid = typeof(IContextMenu).GUID;
            DesktopShell.Check(((IShellFolder)folderObject).GetUIObjectOf(hwnd, (uint)children.Count, children.ToArray(), ref iid, 0, out var contextPointer));
            try { menuObject = Marshal.GetObjectForIUnknown(contextPointer); } finally { Marshal.Release(contextPointer); }
            var context = (IContextMenu)menuObject;
            var context3 = menuObject as IContextMenu3;
            menu = Native.CreatePopupMenu();
            DesktopShell.Check(context.QueryContextMenu(menu, 0, 1, 0x6fff, 0));
            Native.AppendMenu(menu, 0x800, 0, null);
            Native.AppendMenu(menu, 0, 0x7001, removeLabel);
            Native.AppendMenu(menu, 0, 0x7002, resumeLabel);
            source = HwndSource.FromHwnd(hwnd);
            hook = (nint _, int message, nint wp, nint lp, ref bool handled) =>
            {
                if (context3 is not null && message is 0x117 or 0x2b or 0x2c or 0x120)
                { var hr = context3.HandleMenuMsg2((uint)message, wp, lp, out var result); if (hr >= 0) { handled = true; return result; } }
                return 0;
            };
            source?.AddHook(hook);
            Native.GetCursorPos(out var point); Native.SetForegroundWindow(hwnd);
            var selected = Native.TrackPopupMenuEx(menu, 0x100 | 0x2, point.X, point.Y, hwnd, 0);
            if (selected == 0x7001) remove();
            else if (selected == 0x7002) resume();
            else if (selected is > 0 and < 0x7000)
            {
                var command = new InvokeCommand { Size = Marshal.SizeOf<InvokeCommand>(), Hwnd = hwnd, Verb = (nint)(selected - 1), Show = 1 };
                DesktopShell.Check(context.InvokeCommand(ref command));
            }
        }
        finally
        {
            if (hook is not null) source?.RemoveHook(hook);
            if (menu != 0) Native.DestroyMenu(menu);
            if (menuObject is not null) Marshal.ReleaseComObject(menuObject);
            if (folderObject is not null) Marshal.ReleaseComObject(folderObject);
            foreach (var pidl in absolute) Native.ILFree(pidl);
            foreach (var pidl in ownedChildren) Native.ILFree(pidl);
        }
    }
}
