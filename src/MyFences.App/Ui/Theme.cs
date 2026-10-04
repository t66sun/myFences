using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyFences.App.Ui;

internal static class Theme
{
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(36, 48, 68));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(101, 113, 134));
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(40, 95, 240));
    public static void Initialize(Application app)
    {
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/MyFences;component/Ui/Theme.xaml", UriKind.Relative) });
        var windows = new Style(typeof(Window));
        windows.Setters.Add(new Setter(Window.IconProperty, new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/MyFences;component/Assets/MyFences.ico"))));
        app.Resources[typeof(Window)] = windows;
    }
    public static Button Button(string caption, Action action, bool primary = false)
    {
        var button = new Button { Content = caption, Margin = new Thickness(0, 0, 8, 0) };
        if (primary) button.Style = (Style)Application.Current.FindResource("PrimaryButton");
        AutomationProperties.SetName(button, caption);
        button.Click += (_, _) => action(); return button;
    }
    public static TextBlock Label(string caption, double size = 13, bool muted = false) => new() { Text = caption, FontSize = size, Foreground = muted ? Muted : Ink, TextWrapping = TextWrapping.Wrap };
}
