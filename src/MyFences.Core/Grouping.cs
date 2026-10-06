namespace MyFences.Core;

public static class Grouping
{
    public static string Normalize(string path) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
    public static ItemReference? Find(AppState state, string path) => state.Items.FirstOrDefault(i => string.Equals(i.Path, Normalize(path), StringComparison.OrdinalIgnoreCase));

    public static string[] OrderSelection(AppState state, IEnumerable<string> paths)
    {
        var selected = paths.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return state.Items.Where(i => selected.Contains(i.Path)).Select(i => i.Path).ToArray();
    }

    public static void Assign(AppState state, IEnumerable<string> paths, Guid? groupId, int? insertionIndex = null)
    {
        if (groupId.HasValue && !state.Groups.Any(g => g.Id == groupId)) throw new ArgumentException("Unknown group.", nameof(groupId));
        var moved = paths.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Select(path =>
        {
            var item = Find(state, path) ?? new ItemReference { Path = path };
            state.Items.Remove(item);
            item.GroupId = groupId;
            item.Source = AssignmentSource.Manual;
            return item;
        }).ToList();
        var remaining = state.Items.Where(i => i.GroupId == groupId).ToList();
        var index = Math.Clamp(insertionIndex ?? remaining.Count, 0, remaining.Count);
        if (index < remaining.Count) state.Items.InsertRange(state.Items.IndexOf(remaining[index]), moved);
        else state.Items.AddRange(moved);
    }

    public static void DeleteGroup(AppState state, Guid id)
    {
        foreach (var item in state.Items.Where(i => i.GroupId == id)) { item.GroupId = null; item.Source = AssignmentSource.Manual; }
        foreach (var rule in state.Rules.Where(r => r.TargetGroupId == id)) rule.Enabled = false;
        state.Groups.RemoveAll(g => g.Id == id);
    }

    public static void ResumeRules(AppState state, IEnumerable<string> paths)
    {
        foreach (var path in paths)
            if (Find(state, path) is { } item) item.Source = AssignmentSource.Rule;
    }

    public static int Organize(AppState state, IEnumerable<DesktopEntry> desktop)
    {
        var changed = 0;
        foreach (var entry in desktop.DistinctBy(e => Normalize(e.Path), StringComparer.OrdinalIgnoreCase))
        {
            var item = Find(state, entry.Path);
            if (item?.Source == AssignmentSource.Manual) continue;
            var rule = state.Rules.FirstOrDefault(r => r.Matches(entry));
            if (rule is null || rule.TargetGroupId == Guid.Empty) continue;
            if (!state.Groups.Any(g => g.Id == rule.TargetGroupId))
                state.Groups.Add(new() { Id = rule.TargetGroupId, Name = rule.TargetName, X = 100 + state.Groups.Count % 3 * 380, Y = 80 + state.Groups.Count / 3 * 300, Color = state.Settings.DefaultColor, Opacity = state.Settings.DefaultOpacity });
            if (item?.GroupId == rule.TargetGroupId) continue;
            if (item is null) { item = new() { Path = Normalize(entry.Path) }; state.Items.Add(item); }
            item.GroupId = rule.TargetGroupId;
            item.Source = AssignmentSource.Rule;
            changed++;
        }
        return changed;
    }
}

public sealed class UndoHistory
{
    private readonly List<UndoRecord> _undo = [];
    public bool CanUndo => _undo.Count > 0;
    public void Record(AppState before, IEnumerable<DesktopCoordinate>? desktopCoordinates = null)
    {
        _undo.Add(new(before.Clone(), desktopCoordinates?.ToList() ?? []));
        if (_undo.Count > 50) _undo.RemoveAt(0);
    }
    public UndoRecord Peek() => _undo.Count == 0 ? throw new InvalidOperationException("Nothing to undo.") : _undo[^1];
    public void RemoveLast() { if (_undo.Count != 0) _undo.RemoveAt(_undo.Count - 1); }
    public AppState Undo() { var record = Peek(); RemoveLast(); return record.State; }
    public void RenamePath(string oldPath, string newPath)
    {
        foreach (var record in _undo)
        {
            foreach (var item in record.State.Items.Where(i => string.Equals(i.Path, oldPath, StringComparison.OrdinalIgnoreCase))) item.Path = newPath;
            for (var index = 0; index < record.DesktopCoordinates.Count; index++)
                if (string.Equals(record.DesktopCoordinates[index].Path, oldPath, StringComparison.OrdinalIgnoreCase)) record.DesktopCoordinates[index] = record.DesktopCoordinates[index] with { Path = newPath };
        }
    }
    public void ForgetPaths(IEnumerable<string> paths)
    {
        var removed = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var record in _undo) { record.State.Items.RemoveAll(i => removed.Contains(i.Path)); record.DesktopCoordinates.RemoveAll(i => removed.Contains(i.Path)); }
    }
}

public sealed record UndoRecord(AppState State, List<DesktopCoordinate> DesktopCoordinates);
