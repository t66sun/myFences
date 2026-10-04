using MyFences.Core;
using Xunit;

namespace MyFences.Tests;

public class GroupingTests
{
    private static string FilePath(string name) => Path.Combine(Path.GetTempPath(), "MyFences tests", name);

    [Fact] public void UndoTracksPhysicalRenamesAndDoesNotRecreateExternallyMovedReferences()
    {
        var state = AppState.Create("en"); var history = new UndoHistory();
        var oldPath = FilePath("old.txt"); var newPath = FilePath("renamed.txt"); var moved = FilePath("moved.txt");
        Grouping.Assign(state, [oldPath, moved], null); history.Record(state);
        history.RenamePath(oldPath, newPath); history.ForgetPaths([moved]);
        var restored = history.Undo();
        Assert.Equal(newPath, Assert.Single(restored.Items).Path);
        Assert.Null(restored.Items[0].GroupId);
    }

    [Fact] public void PresetsCreateOnlyMatchedGroupsAndDoNotDuplicateItems()
    {
        var state = AppState.Create("en");
        DesktopEntry[] files = [new(FilePath("notes.PDF"), ItemKind.File), new(FilePath("unknown.xyz"), ItemKind.File)];
        Assert.Equal(1, Grouping.Organize(state, files));
        Assert.Equal("Documents", Assert.Single(state.Groups).Name);
        Assert.Single(state.Items);
        Assert.Equal(0, Grouping.Organize(state, files));
    }

    [Fact] public void FirstEnabledMatchingRuleWins()
    {
        var state = AppState.Create("en");
        var custom = new ClassificationRule { Name = "PDF", Extensions = "pdf", TargetName = "Priority", TargetGroupId = Guid.NewGuid() };
        state.Rules.Insert(0, custom);
        Grouping.Organize(state, [new(FilePath("a.pdf"), ItemKind.File)]);
        Assert.Equal(custom.TargetGroupId, state.Items[0].GroupId);
        custom.Enabled = false;
        Grouping.Organize(state, [new(FilePath("a.pdf"), ItemKind.File)]);
        Assert.NotEqual(custom.TargetGroupId, state.Items[0].GroupId);
    }

    [Fact] public void ManualPlacementIncludingBareDesktopSurvivesOrganizeUntilReleased()
    {
        var state = AppState.Create("en");
        var group = new FenceGroup { Name = "Manual" }; state.Groups.Add(group);
        var path = FilePath("notes.txt");
        Grouping.Assign(state, [path], group.Id);
        Assert.Equal(0, Grouping.Organize(state, [new(path, ItemKind.File)]));
        Grouping.Assign(state, [path], null);
        Assert.Equal(0, Grouping.Organize(state, [new(path, ItemKind.File)]));
        Assert.Null(state.Items[0].GroupId);
        Grouping.ResumeRules(state, [path]);
        Assert.Equal(1, Grouping.Organize(state, [new(path, ItemKind.File)]));
    }

    [Fact] public void GroupDeletionUngroupsItemsAndDisablesTargetRules()
    {
        var state = AppState.Create("en");
        var path = FilePath("a.txt");
        Grouping.Organize(state, [new(path, ItemKind.File)]);
        var id = state.Items[0].GroupId!.Value;
        Grouping.DeleteGroup(state, id);
        Assert.Empty(state.Groups);
        Assert.Null(state.Items[0].GroupId);
        Assert.Equal(AssignmentSource.Manual, state.Items[0].Source);
        Assert.False(state.Rules.Single(r => r.TargetGroupId == id).Enabled);
    }

    [Fact] public void MultiItemReorderingPreservesOrderAndUniqueReferences()
    {
        var state = AppState.Create("en"); var group = new FenceGroup(); state.Groups.Add(group);
        var paths = new[] { "a.txt", "b.txt", "c.txt", "d.txt" }.Select(FilePath).ToArray();
        Grouping.Assign(state, paths, group.Id);
        Grouping.Assign(state, [paths[2], paths[3]], group.Id, 0);
        Assert.Equal(new[] { paths[2], paths[3], paths[0], paths[1] }, state.Items.Select(i => i.Path));
        Grouping.Assign(state, [paths[0], paths[0]], group.Id);
        Assert.Equal(4, state.Items.Count);
    }

    [Fact] public void BatchUndoRestoresGroupsAssignmentsAndManualOverrides()
    {
        var state = AppState.Create("en"); var history = new UndoHistory();
        var group = new FenceGroup(); state.Groups.Add(group);
        Grouping.Assign(state, [FilePath("manual.pdf")], null);
        history.Record(state);
        Grouping.Organize(state, [new(FilePath("new.pdf"), ItemKind.File), new(FilePath("manual.pdf"), ItemKind.File)]);
        Assert.Equal(2, state.Items.Count);
        state = history.Undo();
        Assert.Single(state.Groups); Assert.Single(state.Items); Assert.Null(state.Items[0].GroupId);
        Assert.False(history.CanUndo);
    }

    [Fact] public void PersistenceRoundTripRetainsOrderRulesAndManualUngroupedState()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var state = AppState.Create("zh-CN"); var store = new StateStore(directory);
            Grouping.Assign(state, [FilePath("manual.txt")], null);
            state.Rules.Reverse(); store.Save(state);
            var restored = store.Load("en");
            Assert.Equal("zh-CN", restored.Settings.Language);
            Assert.Equal(state.Rules.Select(r => r.TargetGroupId), restored.Rules.Select(r => r.TargetGroupId));
            Assert.Equal(AssignmentSource.Manual, restored.Items[0].Source);
            Assert.Null(restored.Items[0].GroupId);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact] public void CorruptLayoutIsReportedAndNotOverwritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(directory); var store = new StateStore(directory);
            File.WriteAllText(store.LayoutPath, "{broken");
            Assert.Throws<System.Text.Json.JsonException>(() => store.Load("en"));
            Assert.Equal("{broken", File.ReadAllText(store.LayoutPath));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
