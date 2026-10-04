using System.Text.Json;
using MyFences.Core;

namespace MyFences.App.Interop;

internal sealed class RecoverySnapshot
{
    public uint OriginalFlags { get; set; }
    public List<DesktopIcon> Icons { get; set; } = [];
}

internal sealed class DesktopSession(string dataDirectory)
{
    private readonly string _path = Path.Combine(dataDirectory, "desktop-recovery.json");
    private RecoverySnapshot? _snapshot;
    public bool Active => _snapshot is not null;
    public bool RecoveryPending => File.Exists(_path);
    public void Begin(DesktopShell shell)
    {
        if (Active) return;
        _snapshot = new() { OriginalFlags = shell.Flags };
        Save();
        shell.SetManagedFlags(0);
    }
    public void Synchronize(AppState state, DesktopShell shell)
    {
        if (_snapshot is null) return;
        var grouped = state.Items.Where(i => i.GroupId.HasValue && state.Groups.Any(g => g.Id == i.GroupId)).Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desktop = shell.Enumerate();
        foreach (var previous in _snapshot.Icons.Where(i => !grouped.Contains(i.Path)).ToList())
        {
            shell.Position(previous.Path, previous.X, previous.Y);
            _snapshot.Icons.Remove(previous); Save();
        }
        foreach (var icon in desktop.Where(i => grouped.Contains(i.Path)))
        {
            if (!_snapshot.Icons.Any(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase)))
            { _snapshot.Icons.Add(icon); Save(); }
            if (icon.X > -1500 || icon.Y > -1500) shell.Position(icon.Path, -2000, -2000);
        }
    }
    public void Rename(string oldPath, string newPath)
    {
        if (_snapshot is null) return;
        var index = _snapshot.Icons.FindIndex(i => string.Equals(i.Path, oldPath, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        _snapshot.Icons[index] = _snapshot.Icons[index] with { Path = newPath }; Save();
    }
    public void Restore(DesktopShell shell)
    {
        var snapshot = _snapshot ?? (File.Exists(_path) ? JsonSerializer.Deserialize<RecoverySnapshot>(File.ReadAllText(_path)) : null);
        if (snapshot is null)
        {
            if (File.Exists(_path)) throw new InvalidDataException("The desktop recovery file contains no snapshot.");
            return;
        }
        // Free positioning must stay enabled until all owned icons have been restored.
        shell.SetManagedFlags(0);
        foreach (var icon in snapshot.Icons) shell.Position(icon.Path, icon.X, icon.Y);
        shell.SetManagedFlags(snapshot.OriginalFlags);
        _snapshot = null;
        File.Delete(_path);
    }
    private void Save() => StateStore.WriteAtomic(_path, _snapshot!);
}
