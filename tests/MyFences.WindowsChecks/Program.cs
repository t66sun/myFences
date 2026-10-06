using System.Windows;

namespace MyFences.WindowsChecks;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if (args.Contains("--host-check")) { Console.WriteLine(string.Join(Environment.NewLine, MyFences.App.Interop.DesktopHost.Describe())); return 0; }
        if (args.Length == 2 && args[0] == "--write-icon")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
            using var icon = MyFences.App.Ui.TrayIcon.Create();
            using var output = File.Create(args[1]); icon.Save(output); return 0;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (args.Contains("--desktop-view")) return app.Run(new DesktopView());
        if (args.Contains("--menu-command-check")) return MenuCommandCheck.Run(app);
        if (args.Contains("--desktop-drop-check")) return DesktopDropCheck.Run(app);
        if (args.Length == 3 && args[0] == "--desktop-menu-check") return DesktopMenuCheck.Run(app, args[1], args[2]);
        return args.Contains("--render-check") ? RenderCheck.Run(app) :
            args.Contains("--controller-check") ? ControllerCheck.Run(app) : MyFences.App.IntegrationCheck.Run(app);
    }
}
