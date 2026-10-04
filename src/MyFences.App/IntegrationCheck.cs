using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using MyFences.App.Interop;
using MyFences.Core;

namespace MyFences.App;

internal static class IntegrationCheck
{
    public static int Run(Application app)
    {
        Directory.CreateDirectory(Program.DataDirectory);
        var results = new Dictionary<string, object>();
        DesktopShell? shell = null;
        var session = new DesktopSession(Program.DataDirectory);
        Window? window = null;
        uint originalFlags = 0;
        var flagsRead = false;
        try
        {
            shell = new(); originalFlags = shell.Flags; flagsRead = true;
            if (session.RecoveryPending) session.Restore(shell);
            results["shellConnected"] = true;
            results["originalFlags"] = shell.Flags;
            var icons = shell.Enumerate(); results["filesystemIconCount"] = icons.Count;
            var host = DesktopHost.FindHost();
            window = new Window { Title = "MyFences integration check", Width = 360, Height = 180, Left = 150, Top = 100, ShowInTaskbar = false, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Content = new Border { CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(184, 37, 51, 68)), Child = new TextBlock { Text = "MyFences · desktop integration", Foreground = Brushes.White, Margin = new Thickness(24), FontSize = 18 } } };
            window.SourceInitialized += (_, _) => DesktopHost.Attach(window, host);
            window.Show();
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            if (!DesktopHost.IsExposed(window)) throw new InvalidOperationException("The desktop integration window is covered by the desktop icon layer.");
            results["desktopExposed"] = true;
            results["desktopAttached"] = Native.GetParent(new WindowInteropHelper(window).Handle) == host;
            results["windowDpi"] = Native.GetDpiForWindow(new WindowInteropHelper(window).Handle);
            var icon = icons.FirstOrDefault(i => i.X >= 0 && i.Y >= 0);
            session.Begin(shell);
            if ((shell.Flags & DesktopShell.ManagedFlags) != 0) throw new InvalidOperationException("Desktop flags were not disabled.");
            results["flagsAdjusted"] = true;
            if (icon is not null)
            {
                var state = AppState.Create("en"); var group = new FenceGroup(); state.Groups.Add(group);
                Grouping.Assign(state, [icon.Path], group.Id); session.Synchronize(state, shell);
                var hidden = shell.Enumerate().Single(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
                if (hidden.X >= 0 || hidden.Y >= 0) throw new InvalidOperationException("Desktop icon remained visible.");
                results["iconHidden"] = true;
                Grouping.Assign(state, [icon.Path], null); session.Synchronize(state, shell);
                var restored = shell.Enumerate().Single(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
                if (restored.X != icon.X || restored.Y != icon.Y) throw new InvalidOperationException("Desktop icon position was not restored.");
                results["iconRestored"] = true;
                Grouping.Assign(state, [icon.Path], group.Id); session.Synchronize(state, shell);
                new DesktopSession(Program.DataDirectory).Restore(shell);
                var recovered = shell.Enumerate().Single(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
                if (recovered.X != icon.X || recovered.Y != icon.Y || session.RecoveryPending) throw new InvalidOperationException("Recovery from the persisted journal failed.");
                results["journalRecovery"] = true;
            }
            else results["iconTestSkipped"] = "No visible filesystem desktop icon";
            session.Restore(shell);
            if ((shell.Flags & DesktopShell.ManagedFlags) != (originalFlags & DesktopShell.ManagedFlags)) throw new InvalidOperationException("Desktop flags were not restored.");
            results["flagsRestored"] = true;
            results["success"] = true;
            return 0;
        }
        catch (Exception exception) { results["success"] = false; results["error"] = exception.ToString(); results["desktopWindows"] = DesktopHost.Describe(); return 1; }
        finally
        {
            try { if (shell is not null && session.Active) session.Restore(shell); }
            catch (Exception exception) { results["recoveryError"] = exception.ToString(); }
            if (shell is not null && flagsRead) results["finalFlags"] = shell.Flags;
            window?.Close(); shell?.Dispose();
            File.WriteAllText(Path.Combine(Program.DataDirectory, "integration-result.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            app.Shutdown();
        }
    }
}
