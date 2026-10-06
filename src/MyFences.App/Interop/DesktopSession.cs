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
    public IReadOnlyList<DesktopCoordinate> OriginalCoordinates(IEnumerable<string> paths)
    {
        var selected = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _snapshot?.Icons.Where(i => selected.Contains(i.Path)).Select(i => new DesktopCoordinate(i.Path, i.X, i.Y)).ToList() ?? [];
    }
    public void Synchronize(AppState state, DesktopShell shell, IReadOnlyList<DesktopCoordinate>? releaseCoordinates = null, IReadOnlyList<DesktopCoordinate>? captureCoordinates = null)
    {
        if (_snapshot is null) return;
        var grouped = state.Items.Where(i => i.GroupId.HasValue && state.Groups.Any(g => g.Id == i.GroupId)).Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var desktop = shell.Enumerate();
        // Journal each requested destination before moving its icon. A crash must restore the
        // user's new desktop placement, rather than an obsolete position from before capture.
        foreach (var coordinate in releaseCoordinates ?? [])
        {
            if (grouped.Contains(coordinate.Path)) continue;
            var index = _snapshot.Icons.FindIndex(i => string.Equals(i.Path, coordinate.Path, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _snapshot.Icons[index] = _snapshot.Icons[index] with { X = coordinate.X, Y = coordinate.Y };
            else if (desktop.FirstOrDefault(i => string.Equals(i.Path, coordinate.Path, StringComparison.OrdinalIgnoreCase)) is { } icon)
                _snapshot.Icons.Add(icon with { X = coordinate.X, Y = coordinate.Y });
        }
        if (releaseCoordinates is { Count: > 0 }) Save();
        foreach (var previous in _snapshot.Icons.Where(i => !grouped.Contains(i.Path)).ToList())
        {
            if (!shell.Position(previous.Path, previous.X, previous.Y) && (File.Exists(previous.Path) || Directory.Exists(previous.Path))) throw new IOException("Windows could not position the desktop icon.");
            _snapshot.Icons.Remove(previous); Save();
        }
        foreach (var icon in desktop.Where(i => grouped.Contains(i.Path)))
        {
            var index = _snapshot.Icons.FindIndex(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
            var original = captureCoordinates?.FirstOrDefault(i => string.Equals(i.Path, icon.Path, StringComparison.OrdinalIgnoreCase));
            if (index < 0) { _snapshot.Icons.Add(original is null ? icon : icon with { X = original.X, Y = original.Y }); Save(); }
            else if (original is not null) { _snapshot.Icons[index] = icon with { X = original.X, Y = original.Y }; Save(); }
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
        _snapshot = snapshot;
        shell.SetManagedFlags(0);
        foreach (var icon in snapshot.Icons.ToList())
        {
            if (!shell.Position(icon.Path, icon.X, icon.Y) && (File.Exists(icon.Path) || Directory.Exists(icon.Path)))
                throw new IOException("Windows could not restore the desktop icon. Its recovery record has been kept.");
            snapshot.Icons.Remove(icon); Save();
        }
        shell.SetManagedFlags(snapshot.OriginalFlags);
        _snapshot = null;
        File.Delete(_path);
    }
    private void Save() => StateStore.WriteAtomic(_path, _snapshot!);
}
