using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MyFences.App;
using MyFences.App.Interop;
using MyFences.App.Ui;
using MyFences.Core;
using Point = MyFences.App.Interop.Point;

namespace MyFences.WindowsChecks;

internal static class DesktopDropCheck
{
    public static int Run(Application app)
    {
        var previousDirectory = Environment.GetEnvironmentVariable("MYFENCES_DATA_DIR");
        var directory = Path.GetFullPath(Path.Combine(".local", "desktop-drop", Guid.NewGuid().ToString("N")));
        Environment.SetEnvironmentVariable("MYFENCES_DATA_DIR", directory);
        using var shell = new DesktopShell();
        var flags = shell.Flags;
        var icons = shell.Enumerate().Where(i => i.X >= 0 && i.Y >= 0).Take(2).ToList();
        var results = new Dictionary<string, object>();
        AppController? controller = null;
        try
        {
            if (icons.Count != 2) throw new InvalidOperationException("This check needs two existing desktop icons, restored in finally.");
            Directory.CreateDirectory(directory);
            var external = Path.Combine(directory, "external-reference.txt"); File.WriteAllText(external, "Reference only.");
            var beforeDesktopFiles = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Exercise free positioning, then restore the machine's original flags and icon locations.
            shell.SetManagedFlags(0);
            var state = AppState.Create("en"); var group = new FenceGroup { Name = "Desktop drop check", X = 150, Y = 100 };
            state.Groups.Add(group); var ordered = new[] { icons[0].Path, external, icons[1].Path };
            Grouping.Assign(state, ordered, group.Id); state.Settings.DesktopPermissionGranted = true;
            new StateStore(directory).Save(state); Theme.Initialize(app); controller = new(app, false, true);
            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            AssertHidden();

            var drop = FindDropPoint(controller, ordered, shell);
            var plan = controller.PlanDesktopDrop(ordered.Reverse(), drop)!;
            Require(plan.DesktopCoordinates.Select(i => i.Path).SequenceEqual([icons[0].Path, icons[1].Path]), "Preview did not preserve group order.");
            Require(plan.ReferenceCount == 1, "Mixed drop did not separate the external reference.");
            var foreground = Native.GetForegroundWindow();
            using (var preview = new PreviewLifetime(new DesktopDropPreview(() => { })))
            {
                preview.Window.Update(plan); app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Require(controller.IsDesktopDrop(drop), "Drop preview blocked desktop hit testing.");
                Require(Native.GetForegroundWindow() == foreground, "Drop preview stole foreground activation.");
                results["previewClickThrough"] = true; results["previewNoActivation"] = true;
            }

            Require(controller.DropReferencesOnDesktop(ordered.Reverse(), drop), "Desktop release was rejected.");
            Require(controller.State.Items.All(i => i.GroupId is null), "The whole mixed selection was not ungrouped.");
            AssertPlaced(plan); AssertReferenceOnly(external, beforeDesktopFiles);
            var snapshot = ReadRecovery();
            Require(!snapshot.Icons.Any(i => ordered.Contains(i.Path, StringComparer.OrdinalIgnoreCase)), "Released icons retained obsolete recovery ownership.");
            results["mixedRelease"] = true; results["groupOrder"] = true; results["releasePixels"] = true; results["referenceOnly"] = true;

            controller.Undo(); AssertHidden();
            Require(controller.State.Items.Select(i => i.Path).SequenceEqual(ordered), "One Undo did not restore group order.");
            Require(controller.State.Items.All(i => i.GroupId == group.Id), "One Undo did not restore batch membership.");
            snapshot = ReadRecovery();
            foreach (var original in icons)
            {
                var recovered = snapshot.Icons.Single(i => string.Equals(i.Path, original.Path, StringComparison.OrdinalIgnoreCase));
                Require(recovered.X == original.X && recovered.Y == original.Y, "Undo did not restore original recovery coordinates.");
            }
            results["batchUndo"] = true; results["undoOriginalJournal"] = true;

            var sourceWindow = app.Windows.OfType<FenceWindow>().Single(); var sourceHandle = new WindowInteropHelper(sourceWindow).Handle;
            Native.GetWindowRect(sourceHandle, out var sourceBounds);
            Require(!controller.DropReferencesOnDesktop(ordered, new(sourceBounds.Left + 40, sourceBounds.Top + 20)), "Dropping over a group incorrectly released the selection.");
            AssertHidden(); Require(controller.State.Items.All(i => i.GroupId == group.Id), "Invalid drop changed membership.");
            results["invalidTargetKeepsGroup"] = true;

            var secondDrop = FindDropPoint(controller, ordered, shell); var secondPlan = controller.PlanDesktopDrop(ordered, secondDrop)!;
            Require(controller.DropReferencesOnDesktop(ordered, secondDrop), "Second release was rejected."); AssertPlaced(secondPlan);
            controller.Quit(); AssertPlaced(secondPlan);
            Require(!File.Exists(Path.Combine(directory, "desktop-recovery.json")), "Normal quit left recovery ownership.");
            Require((shell.Flags & DesktopShell.ManagedFlags) == 0, "Normal quit did not restore its original free-positioning flags.");
            AssertReferenceOnly(external, beforeDesktopFiles); results["quitRetainsNewPlacement"] = true;

            // Recovery cannot rewind a successfully released icon on the next process start.
            var recovery = new DesktopSession(directory); recovery.Restore(shell); AssertPlaced(secondPlan);
            results["recoveryRetainsReleasedPlacement"] = true; results["success"] = true; return 0;
        }
        catch (Exception exception) { results["success"] = false; results["error"] = exception.ToString(); return 1; }
        finally
        {
            controller?.Quit();
            var recovery = new DesktopSession(directory); if (recovery.RecoveryPending) recovery.Restore(shell);
            shell.SetManagedFlags(0); foreach (var icon in icons) shell.Position(icon.Path, icon.X, icon.Y); shell.SetManagedFlags(flags);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "desktop-drop-result.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(results));
            Environment.SetEnvironmentVariable("MYFENCES_DATA_DIR", previousDirectory); app.Shutdown();
        }

        void AssertHidden()
        {
            var current = shell.Enumerate();
            foreach (var icon in icons)
            {
                var found = current.Single(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
                Require(found.X < -1500 && found.Y < -1500, "Grouped icon is visible on desktop.");
            }
        }
        void AssertPlaced(DesktopDropPlan plan)
        {
            var current = shell.Enumerate();
            foreach (var position in plan.DesktopCoordinates)
            {
                var found = current.Single(i => string.Equals(i.Path, position.Path, StringComparison.OrdinalIgnoreCase));
                var screen = shell.ViewToScreen(new(found.X, found.Y));
                Require(screen.X == position.X && screen.Y == position.Y, "Shell coordinates differ from the previewed screen pixels.");
                results["dpi"] = Native.GetDpiForWindow(shell.ViewWindow);
                results["previewedPixels"] = plan.DesktopCoordinates.Select(i => new { i.X, i.Y }).ToArray();
                results["actualPixels"] = plan.DesktopCoordinates.Select(i => current.Single(c => c.Path == i.Path)).Select(i => shell.ViewToScreen(new(i.X, i.Y))).Select(i => new { i.X, i.Y }).ToArray();
            }
        }
        RecoverySnapshot ReadRecovery() => JsonSerializer.Deserialize<RecoverySnapshot>(File.ReadAllText(Path.Combine(directory, "desktop-recovery.json")))!;
        void AssertReferenceOnly(string external, HashSet<string> beforeDesktopFiles)
        {
            Require(File.ReadAllText(external) == "Reference only.", "External reference content moved or changed.");
            var after = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Require(beforeDesktopFiles.SetEquals(after), "Desktop release created a file or shortcut.");
        }
    }

    private static Point FindDropPoint(AppController controller, IEnumerable<string> paths, DesktopShell shell)
    {
        var area = DesktopShell.WorkArea(shell.ViewToScreen(new(0, 0)));
        for (var y = area.Top + 16; y < area.Bottom - 100; y += 43)
            for (var x = area.Left + 16; x < area.Right - 160; x += 47)
            {
                var point = new Point(x, y); var plan = controller.PlanDesktopDrop(paths, point);
                if (plan is not null && plan.DesktopCoordinates.Count != 0 && plan.DesktopCoordinates[0].X == x && plan.DesktopCoordinates[0].Y == y) return point;
            }
        throw new InvalidOperationException("No exposed empty desktop area is available for the check.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class PreviewLifetime(DesktopDropPreview window) : IDisposable
    {
        public DesktopDropPreview Window { get; } = window;
        public void Dispose() => Window.Close();
    }
}
