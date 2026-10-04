using System.Globalization;
using System.Text.Json;
using System.Windows;
using MyFences.App.Interop;

namespace MyFences.App;

internal static class Program
{
    internal static string DataDirectory => Environment.GetEnvironmentVariable("MYFENCES_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFences");
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (args.Contains("--integration-check")) return IntegrationCheck.Run(app);
        MyFences.App.Ui.Theme.Initialize(app);
        using var mutex = new Mutex(true, "Local\\MyFences", out var ownsInstance);
        if (!ownsInstance) { MessageBox.Show("MyFences is already running. Use its tray menu.\nMyFences 已在运行，请使用托盘菜单。", "MyFences"); return 1; }
        try
        {
            Directory.CreateDirectory(DataDirectory);
            if (args.Contains("--restore-desktop"))
            { using var shell = new DesktopShell(); new DesktopSession(DataDirectory).Restore(shell); return 0; }
            var controller = new AppController(app, args.Contains("--preview"));
            app.Run();
            controller.Dispose();
            return 0;
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(DataDirectory);
            try
            {
                var session = new DesktopSession(DataDirectory);
                if (session.RecoveryPending) { using var shell = new DesktopShell(); session.Restore(shell); }
            }
            catch (Exception recoveryError) { exception = new AggregateException(exception, recoveryError); }
            File.WriteAllText(Path.Combine(DataDirectory, "last-error.txt"), exception.ToString());
            MessageBox.Show("MyFences could not start. Your saved layout has been retained.\nMyFences 无法启动，已保留原布局。\n\n" + exception.Message, "MyFences", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
