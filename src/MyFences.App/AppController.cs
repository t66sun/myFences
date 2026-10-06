using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MyFences.App.Interop;
using MyFences.App.Ui;
using MyFences.Core;
using Forms = System.Windows.Forms;

namespace MyFences.App;

internal sealed record DesktopDropPlan(string[] Paths, List<DesktopScreenCoordinate> DesktopCoordinates, int ReferenceCount, ScreenPoint DropPoint, ScreenBounds WorkArea, ScreenPoint Spacing);

internal sealed class AppController : IDisposable
{
    private readonly Application _app;
    private readonly bool _preview;
    private readonly StateStore _store;
    private readonly DesktopSession _session;
    private readonly UndoHistory _history = new();
    private readonly Dictionary<Guid, FenceWindow> _windows = [];
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _timer;
    private readonly Forms.NotifyIcon _tray;
    private readonly HwndSource _messages;
    private DesktopShell? _shell;
    private nint _host;
    private bool _visible = true, _quitting, _disposed;
    private SettingsWindow? _settingsWindow;
    private DesktopDropPreview? _dropPreview;
    private List<DesktopIcon>? _dragDesktop;
    private List<ScreenBounds>? _dragOccupied;
    public AppState State { get; private set; }
    public bool Preview => _preview;
    public bool CanUndo => _history.CanUndo;
    public ItemDrag? ActiveDrag { get; set; }

    public AppController(Application app, bool preview, bool suppressInitialSettings = false)
    {
        _app = app; _preview = preview;
        _store = new(Program.DataDirectory); _session = new(Program.DataDirectory);
        State = _store.Load(CultureInfo.CurrentUICulture.Name);
        if (preview && State.Groups.Count == 0) CreatePreview();
        Text.Language = State.Settings.Language;
        _messages = new(new HwndSourceParameters("MyFences messages") { ParentWindow = (nint)(-3), WindowStyle = 0 });
        _messages.AddHook(MessageHook);
        _tray = new Forms.NotifyIcon { Text = "MyFences", Icon = TrayIcon.Create(), Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Safe(() => ToggleVisibility()); };
        BuildTray(); RegisterShortcut();
        if (!preview)
        {
            _shell = new();
            if (_session.RecoveryPending) { _session.Restore(_shell); Notify(Text.Get("recovered")); }
            _host = DesktopHost.FindHost();
        }
        Render();
        if (!EnsureManagement(State)) SetVisible(false);
        else if (!_preview && _session.Active) _session.Synchronize(State, _shell!);
        UpdateWatchers();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Tick(); _timer.Start();
        _app.SessionEnding += (_, _) => { if (!_preview && _session.Active) _session.Restore(_shell!); };
        _app.DispatcherUnhandledException += (_, e) => { e.Handled = true; Report(e.Exception); };
        if (!suppressInitialSettings && (State.Groups.Count == 0 || preview)) ShowSettings();
    }

    private void CreatePreview()
    {
        State.Groups.Add(new() { Name = "项目 / Projects", X = 90, Y = 110 });
        State.Groups.Add(new() { Name = "资料 / Reference", X = 480, Y = 110, Color = "#334438" });
        var root = Environment.CurrentDirectory;
        foreach (var file in new[] { "docs\\DESIGN.md", "Directory.Build.props", "README.md" })
        { var path = Path.Combine(root, file); if (File.Exists(path)) Grouping.Assign(State, [path], State.Groups[0].Id); }
    }

    public void Safe(Action action) { try { action(); } catch (Exception exception) { Report(exception); } }
    public void Report(Exception exception)
    {
        Directory.CreateDirectory(Program.DataDirectory);
        File.WriteAllText(Path.Combine(Program.DataDirectory, "last-error.txt"), exception.ToString());
        MessageBox.Show(exception.Message, Text.Get("error"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void Notify(string message) { _tray.BalloonTipTitle = "MyFences"; _tray.BalloonTipText = message; _tray.ShowBalloonTip(3500); }

    private bool EnsureManagement(AppState proposed)
    {
        if (_preview || _session.Active) return true;
        var desktop = _shell!.Enumerate().Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!proposed.Items.Any(i => i.GroupId.HasValue && desktop.Contains(i.Path))) return true;
        if (!State.Settings.DesktopPermissionGranted)
        {
            if (MessageBox.Show(Text.Get("consentBody"), Text.Get("consentTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return false;
            proposed.Settings.DesktopPermissionGranted = true;
        }
        _session.Begin(_shell);
        return true;
    }

    private bool Commit(Action<AppState> change, bool undoable = true, IReadOnlyList<DesktopCoordinate>? releaseCoordinates = null, IReadOnlyList<DesktopCoordinate>? captureCoordinates = null)
    {
        var before = State.Clone(); var next = State.Clone(); change(next);
        var activeBefore = _session.Active;
        var affectedPaths = (releaseCoordinates ?? []).Concat(captureCoordinates ?? []).Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var originalCoordinates = _session.OriginalCoordinates(affectedPaths).ToList();
        if (!_preview && affectedPaths.Count != 0)
            foreach (var icon in _shell!.Enumerate().Where(i => affectedPaths.Contains(i.Path) && !originalCoordinates.Any(p => string.Equals(p.Path, i.Path, StringComparison.OrdinalIgnoreCase))))
                originalCoordinates.Add(new(icon.Path, icon.X, icon.Y));
        try
        {
            // Attach every destination window before changing any native icon.
            Render(next);
            if (!EnsureManagement(next)) { Render(before); return false; }
            if (!_preview) _store.Save(next);
            if (!_preview && _session.Active) _session.Synchronize(next, _shell!, releaseCoordinates, captureCoordinates);
            State = next;
            if (undoable) _history.Record(before, originalCoordinates);
            Text.Language = State.Settings.Language;
            Render(); BuildTray(); UpdateWatchers();
            return true;
        }
        catch
        {
            State = before;
            try
            {
                if (!_preview)
                {
                    if (!activeBefore && _session.Active) _session.Restore(_shell!);
                    else if (_session.Active) _session.Synchronize(before, _shell!, originalCoordinates, originalCoordinates);
                }
            }
            finally { if (!_preview) _store.Save(before); Render(); }
            throw;
        }
    }

    public void NewGroup()
        => CreateGroup(null);
    public void NewGroupAt(int screenX, int screenY)
        => CreateGroup(new Interop.Point(screenX, screenY));
    private void CreateGroup(Interop.Point? screenPoint)
    {
        var name = NameDialog.Ask(Text.Get("new"), Text.Get("groupName"), Text.Get("new") + " " + (State.Groups.Count + 1));
        if (name is null) return;
        var id = Guid.NewGuid();
        if (!Commit(state =>
        {
            var group = new FenceGroup { Id = id, Name = name, X = 80 + state.Groups.Count % 2 * 380, Y = 70 + state.Groups.Count % 3 * 80, Color = state.Settings.DefaultColor, Opacity = state.Settings.DefaultOpacity };
            if (screenPoint is { } point)
            {
                var window = _windows.Values.FirstOrDefault();
                var handle = window is null ? _host : new WindowInteropHelper(window).Handle;
                var location = Native.ScreenToDip(point, handle); group.X = location.X + 12; group.Y = location.Y + 12;
            }
            Clamp(group); state.Groups.Add(group);
        })) return;
        SetVisible(true);
        _windows[id].Highlight();
    }
    public void Rename(Guid id)
    {
        var group = State.Groups.Single(g => g.Id == id);
        var name = NameDialog.Ask(Text.Get("rename"), Text.Get("groupName"), group.Name);
        if (name is not null) Commit(state => state.Groups.Single(g => g.Id == id).Name = name);
    }
    public void Delete(Guid id) => Commit(state => Grouping.DeleteGroup(state, id));
    public void Collapse(Guid id) => Commit(state => { var group = state.Groups.Single(g => g.Id == id); group.Collapsed = !group.Collapsed; });
    public void Assign(IEnumerable<string> paths, Guid? group, int? index = null)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToArray();
        if (existing.Length != 0) Commit(state => Grouping.Assign(state, existing, group, index));
    }
    public void Resume(IEnumerable<string> paths) { var selected = paths.ToArray(); Commit(state => Grouping.ResumeRules(state, selected)); }
    public void AssignMissing(IEnumerable<string> paths) { var selected = paths.ToArray(); Commit(state => Grouping.Assign(state, selected, null)); }
    public void Organize()
    {
        var entries = _preview ? State.Items.Select(i => new DesktopEntry(i.Path, Directory.Exists(i.Path) ? ItemKind.Folder : ItemKind.File)).ToList() : _shell!.Enumerate().Select(i => new DesktopEntry(i.Path, i.Kind)).ToList();
        var count = 0;
        Commit(state => { count = Grouping.Organize(state, entries); foreach (var group in state.Groups) Clamp(group); });
        Notify(count == 0 ? Text.Get("unchanged") : $"{count} {Text.Get("organized")}");
    }
    public void Undo()
    {
        if (!_history.CanUndo) return;
        var transaction = _history.Peek(); var restored = transaction.State;
        if (Commit(state => { state.Groups = restored.Groups; state.Items = restored.Items; state.Rules = restored.Rules; state.Settings = restored.Settings; }, false, captureCoordinates: transaction.DesktopCoordinates))
        { _history.RemoveLast(); BuildTray(); }
    }
    public AppState BeginLayoutEdit() => State.Clone();
    public void FinishLayoutEdit(AppState before)
    {
        if (!_preview) _store.Save(State);
        _history.Record(before); BuildTray();
    }
    public static void Clamp(FenceGroup group)
    {
        var area = SystemParameters.WorkArea;
        group.Width = Math.Clamp(group.Width, 220, Math.Max(220, area.Width - 20));
        group.Height = Math.Clamp(group.Height, 130, Math.Max(130, area.Height - 20));
        group.X = Math.Clamp(group.X, area.Left, Math.Max(area.Left, area.Right - group.Width));
        group.Y = Math.Clamp(group.Y, area.Top, Math.Max(area.Top, area.Bottom - (group.Collapsed ? 36 : group.Height)));
    }
    public void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) { Notify(Text.Get("missing")); return; }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    public void ItemMenu(FenceWindow window, IReadOnlyList<string> paths)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (existing.Count == 0) return;
        ShellMenu.Show(new WindowInteropHelper(window).Handle, existing, Text.Get("remove"), Text.Get("resume"), () => Assign(paths, null), () => Resume(paths));
        Render();
    }

    public void ToggleVisibility() => SetVisible(!_visible);
    public void HideGroups() => SetVisible(false);
    public bool IsDesktopDrop(Interop.Point point)
    {
        if (!DesktopShell.IsBareDesktop(point) || _preview) return false;
        return !_shell!.HitTestIcon(_shell.ScreenToView(point));
    }
    internal DesktopDropPlan? PlanDesktopDrop(IEnumerable<string> paths, Interop.Point point, bool useDragSnapshot = false)
    {
        if (!IsDesktopDrop(point)) return null;
        var ordered = Grouping.OrderSelection(State, paths);
        var desktop = useDragSnapshot && _dragDesktop is not null ? _dragDesktop : _shell!.Enumerate();
        var desktopPaths = desktop.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nativePaths = ordered.Where(desktopPaths.Contains).ToArray();
        var spacing = _shell!.Spacing; var cell = new ScreenPoint(Math.Max(32, spacing.X), Math.Max(32, spacing.Y));
        var work = DesktopShell.WorkArea(point);
        var positions = DesktopPlacement.Plan(new(point.X, point.Y), work, cell,
            useDragSnapshot && _dragOccupied is not null ? _dragOccupied : _shell.OccupiedScreenAreas(), nativePaths.Length);
        if (positions.Count != nativePaths.Length) return null;
        var placed = nativePaths.Select((path, index) => new DesktopScreenCoordinate(path, positions[index].X, positions[index].Y)).ToList();
        return new(ordered, placed, ordered.Length - nativePaths.Length, new(point.X, point.Y), work, cell);
    }
    internal bool DropReferencesOnDesktop(IEnumerable<string> paths, Interop.Point point)
    {
        var plan = PlanDesktopDrop(paths, point);
        if (plan is null || plan.Paths.Length == 0) return false;
        var nativeCoordinates = plan.DesktopCoordinates.Select(i =>
        {
            var view = _shell!.ScreenToView(new(i.X, i.Y)); return new DesktopCoordinate(i.Path, view.X, view.Y);
        }).ToList();
        return Commit(state => Grouping.Assign(state, plan.Paths, null), releaseCoordinates: nativeCoordinates);
    }
    internal void BeginDesktopDrag(string[] paths)
    {
        if (_preview) return;
        _dragDesktop = _shell!.Enumerate(); _dragOccupied = _shell.OccupiedScreenAreas();
        _dropPreview = new(() =>
        {
            try
            {
                if (!Native.GetCursorPos(out var point)) { _dropPreview?.Hide(); return; }
                var plan = PlanDesktopDrop(paths, point, true);
                if (plan is null) _dropPreview?.Hide(); else _dropPreview?.Update(plan);
            }
            catch (COMException) { _dropPreview?.Hide(); }
        });
    }
    internal void EndDesktopDrag()
    { _dropPreview?.Close(); _dropPreview = null; _dragDesktop = null; _dragOccupied = null; }
    public void HandleExternalMove(IEnumerable<string> paths)
    {
        var gone = paths.Where(p => !File.Exists(p) && !Directory.Exists(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (gone.Count == 0) return;
        Commit(state => state.Items.RemoveAll(i => gone.Contains(i.Path)), false);
        _history.ForgetPaths(gone);
    }
    private void SetVisible(bool visible)
    {
        if (visible && !EnsureManagement(State)) return;
        _visible = visible;
        foreach (var window in _windows.Values) if (visible) window.Show(); else window.Hide();
    }
    private void Render(AppState? state = null, bool recreate = false)
    {
        var model = state ?? State;
        foreach (var id in _windows.Keys.Where(id => recreate || !model.Groups.Any(g => g.Id == id)).ToList())
        { _windows[id].AllowClose = true; _windows[id].Close(); _windows.Remove(id); }
        foreach (var group in model.Groups)
        {
            Clamp(group);
            if (!_windows.TryGetValue(group.Id, out var window))
            {
                window = new(this, group, _preview); _windows[group.Id] = window;
                if (!_preview) window.SourceInitialized += (_, _) => DesktopHost.Attach(window, _host);
                window.Show();
                if (!_preview) window.ApplyBounds();
            }
            window.Update(group, model.Items.Where(i => i.GroupId == group.Id).ToList());
            if (!_visible) window.Hide();
        }
    }
    public void ShowSettings()
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new(this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }
    public void ApplySettings(AppState edited)
    {
        Commit(state =>
        {
            var permission = state.Settings.DesktopPermissionGranted;
            state.Rules = edited.Rules; state.Settings = edited.Settings;
            state.Settings.DesktopPermissionGranted = permission;
            foreach (var group in state.Groups) { group.Color = edited.Settings.DefaultColor; group.Opacity = edited.Settings.DefaultOpacity; }
        });
        RegisterShortcut();
    }

    private void BuildTray()
    {
        var old = _tray.ContextMenuStrip; var menu = new Forms.ContextMenuStrip();
        void Add(string key, Action action, bool enabled = true) { var item = menu.Items.Add(Text.Get(key)); item.Enabled = enabled; item.Click += (_, _) => _app.Dispatcher.Invoke(() => Safe(action)); }
        Add("new", NewGroup); Add("organize", Organize); Add("undo", Undo, CanUndo);
        menu.Items.Add(new Forms.ToolStripSeparator()); Add("toggle", ToggleVisibility); Add("settings", ShowSettings);
        menu.Items.Add(new Forms.ToolStripSeparator()); Add("quit", Quit);
        _tray.ContextMenuStrip = menu; old?.Dispose();
    }
    private nint MessageHook(nint hwnd, int message, nint wp, nint lp, ref bool handled)
    { if (message == 0x312 && wp == 1) { Safe(ToggleVisibility); handled = true; } return 0; }
    private void RegisterShortcut()
    {
        Native.UnregisterHotKey(_messages.Handle, 1);
        var (modifiers, key) = Shortcut.Parse(State.Settings.Hotkey);
        if (!Native.RegisterHotKey(_messages.Handle, 1, modifiers | 0x4000, key)) Notify(Text.Get("shortcutConflict"));
    }
    private void UpdateWatchers()
    {
        if (_preview) return;
        var directories = State.Items.Select(i => Path.GetDirectoryName(i.Path)).Where(p => p is not null).Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _watchers.Keys.Where(p => !directories.Contains(p)).ToList()) { _watchers[path].Dispose(); _watchers.Remove(path); }
        foreach (var path in directories.Where(p => !_watchers.ContainsKey(p) && Directory.Exists(p)))
        {
            var watcher = new FileSystemWatcher(path) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
            watcher.Renamed += (_, e) => _app.Dispatcher.BeginInvoke(() => Safe(() =>
            {
                foreach (var item in State.Items.Where(i => string.Equals(i.Path, e.OldFullPath, StringComparison.OrdinalIgnoreCase))) item.Path = e.FullPath;
                _history.RenamePath(e.OldFullPath, e.FullPath); _session.Rename(e.OldFullPath, e.FullPath);
                _store.Save(State); Render(); UpdateWatchers();
            }));
            watcher.Created += (_, _) => _app.Dispatcher.BeginInvoke(() => Render());
            watcher.Deleted += (_, _) => _app.Dispatcher.BeginInvoke(() => Render());
            watcher.EnableRaisingEvents = true; _watchers.Add(path, watcher);
        }
    }
    private void Tick()
    {
        if (_preview || _quitting) return;
        try
        {
            if (!Native.IsWindow(_host))
            {
                _shell?.Dispose(); _shell = new(); _host = DesktopHost.FindHost();
                if (_session.Active) _shell.SetManagedFlags(0);
                Render(recreate: true);
            }
            if (_session.Active)
            {
                if ((_shell!.Flags & DesktopShell.ManagedFlags) != 0)
                {
                    var userFlags = _shell.Flags;
                    _session.Restore(_shell); _shell.SetManagedFlags(userFlags);
                    State.Settings.DesktopPermissionGranted = false; _store.Save(State);
                    SetVisible(false); Notify(Text.Get("pausedBody"));
                }
                else _session.Synchronize(State, _shell);
            }
        }
        catch (COMException) { _shell?.Dispose(); _shell = null; _host = 0; }
        catch (Exception exception)
        {
            _timer.Stop(); Report(exception);
        }
    }
    public void Quit()
    {
        if (_quitting) return;
        _timer.Stop();
        try { if (!_preview) { _store.Save(State); if (_session.Active) _session.Restore(_shell ??= new()); } }
        catch (Exception exception) { _timer.Start(); Report(exception); return; }
        _quitting = true; Dispose(); _app.Shutdown();
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _timer.Stop();
        EndDesktopDrag();
        Native.UnregisterHotKey(_messages.Handle, 1); _messages.Dispose();
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        foreach (var window in _windows.Values) { window.AllowClose = true; window.Close(); }
        _settingsWindow?.Close();
        _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Icon?.Dispose(); _tray.Dispose();
        _shell?.Dispose();
    }
}
