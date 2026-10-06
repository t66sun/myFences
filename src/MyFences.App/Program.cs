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
        InstanceCommand? command = null;
        if (args.Contains("--new-group"))
        {
            var index = Array.IndexOf(args, "--new-group");
            if (args.Contains("--cursor") && Native.GetCursorPos(out var point)) command = new("new-group", point.X, point.Y);
            else if (index + 2 < args.Length && int.TryParse(args[index + 1], out var x) && int.TryParse(args[index + 2], out var y)) command = new("new-group", x, y);
            else command = new("new-group");
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (args.Contains("--integration-check")) return IntegrationCheck.Run(app);
        MyFences.App.Ui.Theme.Initialize(app);
        using var mutex = new Mutex(true, "Local\\MyFences", out var ownsInstance);
        if (!ownsInstance)
        {
            if (command is not null && SingleInstanceCommands.Send(command)) return 0;
            MessageBox.Show(command is null ? "MyFences is already running. Use its tray menu.\nMyFences 已在运行，请使用托盘菜单。" : "The running MyFences did not accept the new-group command. Restart MyFences from this version.\n当前实例未接收新建请求，请退出后从本版本重新启动。", "MyFences");
            return 1;
        }
        try
        {
            Directory.CreateDirectory(DataDirectory);
            if (args.Contains("--restore-desktop"))
            { using var shell = new DesktopShell(); new DesktopSession(DataDirectory).Restore(shell); return 0; }
            var controller = new AppController(app, args.Contains("--preview"), suppressInitialSettings: command is not null);
            void Receive(InstanceCommand request) => controller.Safe(() =>
            {
                if (request.ScreenX is int x && request.ScreenY is int y) controller.NewGroupAt(x, y);
                else controller.NewGroup();
            });
            using var commands = new SingleInstanceCommands(app.Dispatcher, Receive);
            if (command is not null) app.Dispatcher.BeginInvoke(() => Receive(command));
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
