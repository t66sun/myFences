using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MyFences.App;
using MyFences.App.Interop;
using MyFences.App.Ui;
using MyFences.Core;

namespace MyFences.WindowsChecks;

internal static class ControllerCheck
{
    public static int Run(Application app)
    {
        var previousDirectory = Environment.GetEnvironmentVariable("MYFENCES_DATA_DIR");
        var directory = Path.Combine(MyFences.App.Program.DataDirectory, "controller-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("MYFENCES_DATA_DIR", directory);
        using var shell = new DesktopShell();
        var flags = shell.Flags;
        var icon = shell.Enumerate().First(i => i.X >= 0 && i.Y >= 0);
        var results = new Dictionary<string, object>();
        AppController? controller = null;
        try
        {
            var state = AppState.Create("zh-CN");
            var group = new FenceGroup { Name = "MyFences integration", X = 150, Y = 100 };
            state.Groups.Add(group); Grouping.Assign(state, [icon.Path], group.Id);
            // Isolated test data: consent covers this one temporary capture and is not saved to the user's layout.
            state.Settings.DesktopPermissionGranted = true; new StateStore(directory).Save(state);
            Theme.Initialize(app); controller = new(app, false);
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var window = app.Windows.OfType<FenceWindow>().Single();
            var handle = new WindowInteropHelper(window).Handle;
            if (Native.GetParent(handle) == 0) throw new InvalidOperationException("Group did not attach to the desktop.");
            Native.GetWindowRect(handle, out var bounds);
            var scale = Native.GetDpiForWindow(handle) / 96d;
            if (bounds.Right - bounds.Left != Math.Round(group.Width * scale) || bounds.Bottom - bounds.Top != Math.Round(group.Height * scale))
                throw new InvalidOperationException("Native group dimensions do not match DPI-scaled layout.");
            results["actualGroupAttached"] = true; results["dpi"] = Native.GetDpiForWindow(handle);
            if (!DesktopHost.IsExposed(window)) throw new InvalidOperationException("The group is covered by a sibling in the desktop window tree.");
            results["groupExposed"] = true;
            CheckHidden();
            var newGroup = new FenceGroup { Name = "New group check", X = 600, Y = 100 };
            controller.State.Groups.Add(newGroup);
            controller.ApplySettings(controller.State.Clone());
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var addedWindow = app.Windows.OfType<FenceWindow>().Single(w => w.Title == newGroup.Name);
            if (!DesktopHost.IsExposed(addedWindow)) throw new InvalidOperationException("A newly created group is covered.");
            results["newGroupExposed"] = true;
            controller.Collapse(group.Id);
            if (window.Height != 36) throw new InvalidOperationException("Collapse failed.");
            controller.Undo();
            if (window.Height != group.Height) throw new InvalidOperationException("Collapse undo failed.");
            results["collapseUndo"] = true;
            controller.Assign([icon.Path], null);
            CheckRestored();
            controller.Undo(); CheckHidden(); results["ungroupUndo"] = true;
            controller.Quit(); CheckRestored();
            if ((shell.Flags & DesktopShell.ManagedFlags) != (flags & DesktopShell.ManagedFlags)) throw new InvalidOperationException("Exit did not restore arrangement flags.");
            results["quitRestored"] = true; results["success"] = true;
            return 0;
        }
        catch (Exception exception) { results["success"] = false; results["error"] = exception.ToString(); return 1; }
        finally
        {
            controller?.Quit();
            var recovery = new DesktopSession(directory);
            if (recovery.RecoveryPending) recovery.Restore(shell);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "controller-result.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(results));
            Environment.SetEnvironmentVariable("MYFENCES_DATA_DIR", previousDirectory); app.Shutdown();
        }
        void CheckHidden()
        {
            var current = shell.Enumerate().Single(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
            if (current.X >= 0 || current.Y >= 0) throw new InvalidOperationException("Native icon was not captured.");
        }
        void CheckRestored()
        {
            var current = shell.Enumerate().Single(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
            if (current.X != icon.X || current.Y != icon.Y) throw new InvalidOperationException("Original icon coordinates were not restored.");
        }
    }
}
