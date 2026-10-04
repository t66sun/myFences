using System.Windows;
using System.Windows.Controls;

namespace MyFences.App.Ui;

internal static class NameDialog
{
    public static string? Ask(string title, string label, string initial)
    {
        var window = new Window { Title = title, Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, FontFamily = new("Segoe UI"), Background = System.Windows.Media.Brushes.White };
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(Theme.Label(label));
        var input = new TextBox { Text = initial, MaxLength = 80, Margin = new Thickness(0, 10, 0, 20) }; panel.Children.Add(input);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var accept = Theme.Button(Text.Get("apply"), () => window.DialogResult = true, true); accept.IsDefault = true;
        var cancel = Theme.Button(Text.Get("close"), () => window.DialogResult = false); cancel.IsCancel = true;
        input.TextChanged += (_, _) => accept.IsEnabled = input.Text.Trim().Length > 0;
        actions.Children.Add(cancel); actions.Children.Add(accept); panel.Children.Add(actions); window.Content = panel;
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true ? input.Text.Trim() : null;
    }
}
