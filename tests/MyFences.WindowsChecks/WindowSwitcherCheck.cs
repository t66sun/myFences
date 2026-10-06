using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MyFences.App;
using MyFences.App.Interop;
using MyFences.App.Ui;
using MyFences.Core;

namespace MyFences.WindowsChecks;

internal static class WindowSwitcherCheck
{
    public static int Run(Application app)
    {
        Theme.Initialize(app);
        var state = AppState.Create("en");
        state.Groups.Add(new() { Name = "Switcher check 1" });
        state.Groups.Add(new() { Name = "Switcher check 2" });
        new StateStore(MyFences.App.Program.DataDirectory).Save(state);
        using var controller = new AppController(app, false);
        try
        {
            var settings = new SettingsWindow(controller);
            settings.Show();
            var preview = new DesktopDropPreview(() => { });
            preview.Show();
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            foreach (Window window in app.Windows.Cast<Window>().ToArray())
            {
                Check(window);
                window.Hide(); window.Show(); Check(window);
            }
            Exception? failure = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var dialog = app.Windows.Cast<Window>().Single(w => w.Title == "Switcher naming check");
                try { Check(dialog); } catch (Exception error) { failure = error; }
                dialog.DialogResult = false;
            };
            timer.Start();
            NameDialog.Ask("Switcher naming check", "Name", "check");
            if (failure is not null) throw failure;
            Native.EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out var pid);
                var title = new StringBuilder(128); GetWindowText(handle, title, title.Capacity);
                if (pid == Environment.ProcessId && title.ToString() == "Hidden Window")
                    failure = new InvalidOperationException("WPF created a hidden owner window.");
                return true;
            }, 0);
            if (failure is not null) throw failure;
            preview.Close(); settings.Close();
            Console.WriteLine("PASS: desktop groups, Settings, naming dialog and drop preview excluded; no hidden owners; hide/show preserved styles.");
            return 0;
        }
        finally { controller.Quit(); app.Shutdown(); }
    }

    private static void Check(Window window)
    {
        var style = (long)Native.GetWindowLong(new WindowInteropHelper(window).Handle, -20);
        if ((style & 0x80) == 0 || (style & 0x40000) != 0)
            throw new InvalidOperationException(window.Title + " is not excluded from Alt+Tab.");
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int count);
}
