using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MyFences.App;
using MyFences.App.Ui;
using MyFences.Core;

namespace MyFences.WindowsChecks;

internal static class RenderCheck
{
    public static int Run(Application app)
    {
        var output = Path.Combine(Environment.CurrentDirectory, ".local", "render-check");
        Directory.CreateDirectory(output);
        Theme.Initialize(app);
        AppController? controller = null;
        var results = new List<string>();
        try
        {
            controller = new(app, true);
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            foreach (var language in new[] { "zh-CN", "en" })
            {
                Text.Language = language;
                var settings = new SettingsWindow(controller);
                settings.Show(); app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); settings.UpdateLayout();
                foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
                {
                    Save(settings, Path.Combine(output, $"settings-{language}-{scale}.png"), scale);
                    results.Add($"Settings {language} render scale {scale}");
                }
                settings.Close();
                foreach (var group in controller.State.Groups)
                {
                    var window = new FenceWindow(controller, group, true) { AllowClose = true };
                    window.Show(); window.Update(group, controller.State.Items.Where(i => i.GroupId == group.Id).ToList()); app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); window.UpdateLayout();
                    Save(window, Path.Combine(output, $"group-{language}-{group.Id}.png"), 1);
                    group.Width = 220; window.Update(group, [new ItemReference { Path = Path.Combine(Environment.CurrentDirectory, "A deliberately long missing filename for layout verification.txt"), GroupId = group.Id }]);
                    window.UpdateLayout(); Save(window, Path.Combine(output, $"narrow-{language}-{group.Id}.png"), 1);
                    window.Close(); results.Add($"Group {language} populated / empty / narrow");
                }
            }
            File.WriteAllText(Path.Combine(output, "render-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        finally { controller?.Quit(); app.Shutdown(); }
    }
    private static void Save(Window window, string path, double scale)
    {
        var visual = window;
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth * scale), (int)Math.Ceiling(visual.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
