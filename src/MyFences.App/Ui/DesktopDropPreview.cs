using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MyFences.App.Interop;

namespace MyFences.App.Ui;

internal sealed class DesktopDropPreview : Window
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly DispatcherTimer _timer;
    private string? _lastPlan;

    public DesktopDropPreview(Action update)
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        Focusable = false; IsHitTestVisible = false; Content = _canvas;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            Native.SetWindowLong(handle, -20, Native.GetWindowLong(handle, -20) | 0x08000000 | 0x00000080 | 0x00000020);
            Native.EnableWindow(handle, false);
            HwndSource.FromHwnd(handle)?.AddHook((nint hwnd, int message, nint wp, nint lp, ref bool handled) =>
            {
                if (message == 0x84) { handled = true; return (nint)(-1); } // HTTRANSPARENT
                if (message == 0x21) { handled = true; return (nint)3; } // MA_NOACTIVATE
                return 0;
            });
        };
        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => update(); _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    public void Update(DesktopDropPlan plan)
    {
        var key = $"{plan.DropPoint.X},{plan.DropPoint.Y}:{plan.DesktopCoordinates.Count}:{plan.ReferenceCount}";
        if (_lastPlan == key && IsVisible) return;
        _lastPlan = key;
        if (!IsVisible) Show();
        var handle = new WindowInteropHelper(this).Handle; var scale = Native.DpiScale(handle);
        Width = plan.WorkArea.Width / scale; Height = plan.WorkArea.Height / scale;
        Native.SetWindowPos(handle, (nint)(-1), plan.WorkArea.Left, plan.WorkArea.Top, plan.WorkArea.Width, plan.WorkArea.Height, 0x10);
        _canvas.Children.Clear();
        foreach (var coordinate in plan.DesktopCoordinates)
        {
            var rectangle = new Rectangle
            {
                Width = plan.Spacing.X / scale, Height = plan.Spacing.Y / scale,
                RadiusX = 6, RadiusY = 6, Stroke = Theme.Accent, StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection([3, 2]), Fill = new SolidColorBrush(Color.FromArgb(70, 30, 75, 105))
            };
            Canvas.SetLeft(rectangle, (coordinate.X - plan.WorkArea.Left) / scale);
            Canvas.SetTop(rectangle, (coordinate.Y - plan.WorkArea.Top) / scale); _canvas.Children.Add(rectangle);
        }
        var label = plan.DesktopCoordinates.Count == 0 ? Text.Get("dropRemove") : plan.ReferenceCount == 0 ? Text.Get("dropDesktop") :
            string.Format(Text.Get("dropMixed"), plan.DesktopCoordinates.Count, plan.ReferenceCount);
        var caption = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 26, 32, 41)), BorderBrush = Theme.Accent, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6),
            Child = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 12 }
        };
        caption.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var x = Math.Clamp((plan.DropPoint.X - plan.WorkArea.Left) / scale + 16, 0, Math.Max(0, Width - caption.DesiredSize.Width));
        var y = Math.Clamp((plan.DropPoint.Y - plan.WorkArea.Top) / scale + 18, 0, Math.Max(0, Height - caption.DesiredSize.Height));
        Canvas.SetLeft(caption, x); Canvas.SetTop(caption, y); _canvas.Children.Add(caption);
    }
}
