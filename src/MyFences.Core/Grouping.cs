namespace MyFences.Core;

public static class Grouping
{
    public static string Normalize(string path) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
    public static ItemReference? Find(AppState state, string path) => state.Items.FirstOrDefault(i => string.Equals(i.Path, Normalize(path), StringComparison.OrdinalIgnoreCase));

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
    private readonly List<AppState> _undo = [];
    public bool CanUndo => _undo.Count > 0;
    public void Record(AppState before)
    {
        _undo.Add(before.Clone());
        if (_undo.Count > 50) _undo.RemoveAt(0);
    }
    public AppState Undo() => _undo.Count == 0 ? throw new InvalidOperationException("Nothing to undo.") : TakeLast();
    public void RenamePath(string oldPath, string newPath)
    {
        foreach (var state in _undo)
            foreach (var item in state.Items.Where(i => string.Equals(i.Path, oldPath, StringComparison.OrdinalIgnoreCase))) item.Path = newPath;
    }
    public void ForgetPaths(IEnumerable<string> paths)
    {
        var removed = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var state in _undo) state.Items.RemoveAll(i => removed.Contains(i.Path));
    }
    private AppState TakeLast() { var state = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); return state; }
}
