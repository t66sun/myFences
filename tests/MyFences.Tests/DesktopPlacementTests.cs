using MyFences.Core;
using Xunit;

namespace MyFences.Tests;

public sealed class DesktopPlacementTests
{
    [Fact] public void SingleIconBeginsAtDropPixelEvenWhenNotAlignedToGrid()
    {
        var result = DesktopPlacement.Plan(new(173, 127), new(0, 0, 800, 600), new(80, 90), [], 1);
        Assert.Equal(new ScreenPoint(173, 127), Assert.Single(result));
    }

    [Fact] public void BatchUsesRowMajorCellsAndAvoidsExistingIconBounds()
    {
        var occupied = new ScreenBounds(168, 108, 248, 188);
        var result = DesktopPlacement.Plan(new(88, 108), new(0, 0, 416, 416), new(80, 80), [occupied], 5);
        Assert.Equal(new[] { new ScreenPoint(88, 108), new(248, 108), new(328, 108), new(8, 188), new(88, 188) }, result);
        Assert.All(result, point => Assert.False(new ScreenBounds(point.X, point.Y, point.X + 80, point.Y + 80).Intersects(occupied)));
    }

    [Theory]
    [InlineData(1)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(2)]
    public void ScaledPixelSpacingAndEdgeDropKeepEveryIconInsideWorkArea(double scale)
    {
        var spacing = (int)Math.Round(75 * scale);
        var bounds = new ScreenBounds(-300, 100, 900, 800);
        var result = DesktopPlacement.Plan(new(899, 799), bounds, new(spacing, spacing), [], 12);
        Assert.Equal(12, result.Count); Assert.Equal(12, result.Distinct().Count());
        Assert.All(result, p => { Assert.InRange(p.X, bounds.Left, bounds.Right - spacing); Assert.InRange(p.Y, bounds.Top, bounds.Bottom - spacing); });
    }

    [Fact] public void FullDesktopRejectsWholeBatchInsteadOfPartiallyRemovingSelection()
    {
        var result = DesktopPlacement.Plan(new(20, 20), new(0, 0, 176, 176), new(80, 80), [new(0, 0, 176, 176)], 2);
        Assert.Empty(result);
    }

    [Fact] public void ReferencesWithoutDesktopIconsNeedNoDesktopCells()
        => Assert.Empty(DesktopPlacement.Plan(new(20, 20), new(0, 0, 10, 10), new(80, 80), [], 0));

    [Fact] public void SelectionOrderAndOneUndoRetainMixedReferenceMembershipAndCoordinates()
    {
        var state = AppState.Create("en"); var group = new FenceGroup(); state.Groups.Add(group);
        var paths = new[] { "first.txt", "external.txt", "third.txt" }.Select(p => Path.GetFullPath(Path.Combine(".local", "placement", p))).ToArray();
        Grouping.Assign(state, paths, group.Id);
        var ordered = Grouping.OrderSelection(state, [paths[2], paths[0], paths[1]]); Assert.Equal(paths, ordered);
        var history = new UndoHistory(); history.Record(state, [new(paths[0], 20, 30), new(paths[2], 100, 30)]);
        Grouping.Assign(state, ordered, null);
        Assert.All(state.Items, item => Assert.Null(item.GroupId));
        var undo = history.Peek(); Assert.Equal(paths, undo.State.Items.Select(i => i.Path));
        Assert.All(undo.State.Items, item => Assert.Equal(group.Id, item.GroupId));
        Assert.Equal(new[] { paths[0], paths[2] }, undo.DesktopCoordinates.Select(p => p.Path));
        history.RemoveLast(); Assert.False(history.CanUndo);
    }

    [Fact] public void NativeUndoMetadataTracksRenamesAndForgetsExternallyMovedFiles()
    {
        var path = Path.GetFullPath(Path.Combine(".local", "placement", "old.txt"));
        var moved = Path.GetFullPath(Path.Combine(".local", "placement", "moved.txt"));
        var renamed = Path.GetFullPath(Path.Combine(".local", "placement", "new.txt"));
        var state = AppState.Create("en"); Grouping.Assign(state, [path, moved], null);
        var history = new UndoHistory(); history.Record(state, [new(path, 10, 20), new(moved, 30, 40)]);
        history.RenamePath(path, renamed); history.ForgetPaths([moved]);
        Assert.Equal(new DesktopCoordinate(renamed, 10, 20), Assert.Single(history.Peek().DesktopCoordinates));
    }
}
