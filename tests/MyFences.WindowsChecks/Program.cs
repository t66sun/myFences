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
        return args.Contains("--render-check") ? RenderCheck.Run(app) :
            args.Contains("--controller-check") ? ControllerCheck.Run(app) : MyFences.App.IntegrationCheck.Run(app);
    }
}
