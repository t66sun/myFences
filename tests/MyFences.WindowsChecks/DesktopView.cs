using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MyFences.App.Interop;
using Rect = MyFences.App.Interop.Rect;

namespace MyFences.WindowsChecks;

internal sealed class DesktopView : Window
{
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref Properties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect bounds);
    [StructLayout(LayoutKind.Sequential)] private struct Properties
    {
        public uint Flags; public Rect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    private nint _thumbnail;
    public DesktopView()
    {
        Title = "MyFences · Desktop rendering check"; Width = 680; Height = 430;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Brushes.Black;
        Content = new TextBlock { Text = "Live Windows desktop composition · diagnostic", Foreground = Brushes.White, Margin = new Thickness(12, 8, 12, 0) };
        Loaded += (_, _) =>
        {
            var result = DwmRegisterThumbnail(new WindowInteropHelper(this).Handle, Native.FindWindow("Progman", null), out _thumbnail);
            if (result < 0) throw new COMException("Desktop thumbnail registration failed.", result);
            Refresh();
        };
        SizeChanged += (_, _) => Refresh();
        Closed += (_, _) => { if (_thumbnail != 0) DwmUnregisterThumbnail(_thumbnail); Application.Current.Shutdown(); };
    }
    private void Refresh()
    {
        if (_thumbnail == 0) return;
        GetClientRect(new WindowInteropHelper(this).Handle, out var bounds);
        Native.GetWindowRect(Native.FindWindow("Progman", null), out var desktop);
        var aspect = (double)(desktop.Right - desktop.Left) / (desktop.Bottom - desktop.Top);
        var height = Math.Min(bounds.Bottom - 50, (bounds.Right - 20) / aspect);
        var width = height * aspect;
        var properties = new Properties { Flags = 1 | 4 | 8 | 16, Destination = new Rect { Left = 10, Top = 40, Right = 10 + (int)width, Bottom = 40 + (int)height }, Opacity = 255, Visible = true, ClientOnly = false };
        Marshal.ThrowExceptionForHR(DwmUpdateThumbnailProperties(_thumbnail, ref properties));
    }
}
