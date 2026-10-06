using System.Runtime.InteropServices;
using System.Text;

namespace MyFences.App.Interop;

[StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
[StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }

internal static class Native
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string className, string? title);
    internal delegate bool EnumWindow(nint hwnd, nint parameter);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] internal static extern bool EnumChildWindows(nint parent, EnumWindow callback, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint child);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLong(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] internal static extern nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rectangle);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder name, int maximum);
    [DllImport("user32.dll")] internal static extern int MapWindowPoints(nint from, nint to, ref Point point, uint count);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool EnableWindow(nint hwnd, bool enabled);
    [DllImport("user32.dll")] internal static extern nint ChildWindowFromPointEx(nint parent, Point point, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern int SHParseDisplayName(string name, nint bind, out nint pidl, uint flags, out uint attributes);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern bool SHGetPathFromIDListEx(nint pidl, StringBuilder path, uint maximum, uint flags);
    [DllImport("shell32.dll")] internal static extern nint ILFindLastID(nint pidl);
    [DllImport("shell32.dll")] internal static extern nint ILCombine(nint first, nint second);
    [DllImport("shell32.dll")] internal static extern void ILFree(nint pidl);
    [DllImport("shell32.dll")] internal static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object obj, out nint pidl);
    [DllImport("shell32.dll")] internal static extern int SHBindToParent(nint pidl, ref Guid iid, out nint folder, out nint child);
    [DllImport("user32.dll")] internal static extern nint CreatePopupMenu();
    [DllImport("user32.dll")] internal static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] internal static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint parameters);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern nint SHGetFileInfo(string path, uint attributes, out ShellFileInfo info, uint size, uint flags);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct ShellFileInfo
    { public nint Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }

    internal static double DpiScale(nint window) { var dpi = window == 0 ? 96u : GetDpiForWindow(window); return (dpi == 0 ? 96 : dpi) / 96d; }
    internal static System.Windows.Point ScreenToDip(Point point, nint window) { var scale = DpiScale(window); return new(point.X / scale, point.Y / scale); }
    internal static Point DipToScreen(double x, double y, nint window) { var scale = DpiScale(window); return new((int)Math.Round(x * scale), (int)Math.Round(y * scale)); }
}

[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellWindows
{
    [PreserveSig] int GetTypeInfoCount(); [PreserveSig] int GetTypeInfo(); [PreserveSig] int GetIDsOfNames(); [PreserveSig] int Invoke();
    [PreserveSig] int Count(out int count);
    [PreserveSig] int Item([MarshalAs(UnmanagedType.Struct)] object index, [MarshalAs(UnmanagedType.IDispatch)] out object folder);
    [PreserveSig] int NewEnum(out nint value);
    [PreserveSig] int Register(nint pid, int hwnd, int kind, out int cookie);
    [PreserveSig] int RegisterPending(int thread, ref object location, ref object root, int kind, out int cookie);
    [PreserveSig] int Revoke(int cookie); [PreserveSig] int OnNavigate(int cookie, ref object location);
    [PreserveSig] int OnActivated(int cookie, [MarshalAs(UnmanagedType.VariantBool)] bool active);
    [PreserveSig] int FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object location, [MarshalAs(UnmanagedType.Struct)] ref object root, int kind, out int hwnd, int options, [MarshalAs(UnmanagedType.IDispatch)] out object dispatch);
}
[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProvider { [PreserveSig] int QueryService(ref Guid service, ref Guid iid, out nint value); }
[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    [PreserveSig] int GetWindow(out nint hwnd); [PreserveSig] int Help(int mode);
    [PreserveSig] int InsertMenus(nint menu, nint widths); [PreserveSig] int SetMenu(nint menu, nint hole, nint hwnd);
    [PreserveSig] int RemoveMenus(nint menu); [PreserveSig] int SetStatus(nint text);
    [PreserveSig] int EnableModeless(int enable); [PreserveSig] int Translate(nint message, ushort id);
    [PreserveSig] int Browse(nint pidl, uint flags); [PreserveSig] int GetStream(uint mode, out nint stream);
    [PreserveSig] int GetControl(uint id, out nint hwnd); [PreserveSig] int SendControl(uint id, uint message, nint wp, nint lp, out nint result);
    [PreserveSig] int QueryActiveShellView([MarshalAs(UnmanagedType.IUnknown)] out object view);
}
[ComImport, Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView2
{
    [PreserveSig] int GetViewMode(out uint mode); [PreserveSig] int SetViewMode(uint mode);
    [PreserveSig] int GetFolder(ref Guid iid, out nint folder); [PreserveSig] int Item(int index, out nint pidl);
    [PreserveSig] int ItemCount(uint flags, out int count); [PreserveSig] int Items(uint flags, ref Guid iid, out nint items);
    [PreserveSig] int GetSelectionMarkedItem(out int index); [PreserveSig] int GetFocusedItem(out int index);
    [PreserveSig] int GetItemPosition(nint pidl, out Point point); [PreserveSig] int GetSpacing(out Point point);
    [PreserveSig] int GetDefaultSpacing(out Point point); [PreserveSig] int GetAutoArrange();
    [PreserveSig] int SelectItem(int index, uint flags);
    [PreserveSig] int SelectAndPositionItems(uint count, [MarshalAs(UnmanagedType.LPArray)] nint[] items, [MarshalAs(UnmanagedType.LPArray)] Point[] points, uint flags);
    [PreserveSig] int SetGroupBy(); [PreserveSig] int GetGroupBy(); [PreserveSig] int SetViewProperty();
    [PreserveSig] int GetViewProperty(); [PreserveSig] int SetTileViewProperties(); [PreserveSig] int SetExtendedTileViewProperties(); [PreserveSig] int SetText();
    [PreserveSig] int SetCurrentFolderFlags(uint mask, uint flags); [PreserveSig] int GetCurrentFolderFlags(out uint flags);
}
[ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellFolder
{
    [PreserveSig] int ParseDisplayName(nint hwnd, nint bind, [MarshalAs(UnmanagedType.LPWStr)] string name, out uint eaten, out nint pidl, ref uint attributes);
    [PreserveSig] int EnumObjects(nint hwnd, uint flags, out nint list);
    [PreserveSig] int BindToObject(nint pidl, nint bind, ref Guid iid, out nint obj);
    [PreserveSig] int BindToStorage(nint pidl, nint bind, ref Guid iid, out nint obj);
    [PreserveSig] int CompareIds(nint flags, nint first, nint second);
    [PreserveSig] int CreateViewObject(nint hwnd, ref Guid iid, out nint obj);
    [PreserveSig] int GetAttributes(uint count, [MarshalAs(UnmanagedType.LPArray)] nint[] items, ref uint attributes);
    [PreserveSig] int GetUIObjectOf(nint hwnd, uint count, [MarshalAs(UnmanagedType.LPArray)] nint[] items, ref Guid iid, nint reserved, out nint obj);
}
[StructLayout(LayoutKind.Sequential)] internal struct InvokeCommand
{
    public int Size; public uint Mask; public nint Hwnd, Verb, Parameters, Directory; public int Show; public uint Hotkey; public nint Icon;
}
[ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IContextMenu
{
    [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
    [PreserveSig] int InvokeCommand(ref InvokeCommand command);
    [PreserveSig] int GetCommandString(nuint id, uint type, nint reserved, nint name, uint maximum);
}
[ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IContextMenu3
{
    [PreserveSig] int QueryContextMenu(nint menu, uint index, uint first, uint last, uint flags);
    [PreserveSig] int InvokeCommand(ref InvokeCommand command);
    [PreserveSig] int GetCommandString(nuint id, uint type, nint reserved, nint name, uint maximum);
    [PreserveSig] int HandleMenuMsg(uint message, nint wp, nint lp);
    [PreserveSig] int HandleMenuMsg2(uint message, nint wp, nint lp, out nint result);
}
