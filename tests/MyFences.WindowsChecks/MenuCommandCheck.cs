using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MyFences.App;

namespace MyFences.WindowsChecks;

internal static class MenuCommandCheck
{
    public static int Run(Application app)
    {
        var results = new Dictionary<string, object>();
        try
        {
            InstanceCommand? received = null;
            var onUiThread = false;
            using var listener = new SingleInstanceCommands(app.Dispatcher, command =>
            {
                received = command;
                onUiThread = app.Dispatcher.CheckAccess();
            });
            var expected = new InstanceCommand("new-group", 1200, 800);
            var sender = Task.Run(() => SingleInstanceCommands.Send(expected));
            var frame = new DispatcherFrame();
            var stopwatch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            timer.Tick += (_, _) =>
            {
                if ((received is not null && sender.IsCompleted) || stopwatch.Elapsed > TimeSpan.FromSeconds(8)) frame.Continue = false;
            };
            timer.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
            results["senderStatus"] = sender.Status.ToString();
            results["acknowledged"] = sender.IsCompletedSuccessfully && sender.Result;
            results["received"] = received is null ? "none" : JsonSerializer.Serialize(received);
            results["onUiThread"] = onUiThread;
            if (sender.Exception is not null) results["senderError"] = sender.Exception.ToString();
            if (!sender.IsCompletedSuccessfully || !sender.Result || received != expected || !onUiThread)
                throw new InvalidOperationException("The menu command was not acknowledged and dispatched with its screen coordinates.");
            results["commandAcknowledged"] = true;
            results["coordinatesPreserved"] = true;
            results["receivedOnUiThread"] = true;
            results["success"] = true;
            return 0;
        }
        catch (Exception error) { results["success"] = false; results["error"] = error.ToString(); return 1; }
        finally
        {
            Directory.CreateDirectory(MyFences.App.Program.DataDirectory);
            File.WriteAllText(Path.Combine(MyFences.App.Program.DataDirectory, "menu-command-result.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(results));
            app.Shutdown();
        }
    }
}
