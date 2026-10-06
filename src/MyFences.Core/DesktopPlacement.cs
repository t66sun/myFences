namespace MyFences.Core;

public readonly record struct ScreenPoint(int X, int Y);
public readonly record struct ScreenBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Intersects(ScreenBounds other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}

// Recovery/undo coordinates use IFolderView's client pixel space. Preview positions are screen pixels.
public sealed record DesktopCoordinate(string Path, int X, int Y);
public sealed record DesktopScreenCoordinate(string Path, int X, int Y);

public static class DesktopPlacement
{
    // Positions are physical screen pixels, including on scaled displays. The first available
    // cell begins at the drop point; following cells read left to right, then top to bottom.
    public static IReadOnlyList<ScreenPoint> Plan(ScreenPoint drop, ScreenBounds workArea, ScreenPoint spacing, IEnumerable<ScreenBounds> occupied, int count)
    {
        if (count == 0) return [];
        if (count < 0 || spacing.X <= 0 || spacing.Y <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        const int margin = 8;
        var left = workArea.Left + margin; var top = workArea.Top + margin;
        var right = workArea.Right - margin - spacing.X; var bottom = workArea.Bottom - margin - spacing.Y;
        if (right < left || bottom < top) return [];
        var anchor = new ScreenPoint(Math.Clamp(drop.X, left, right), Math.Clamp(drop.Y, top, bottom));
        var firstX = anchor.X - (anchor.X - left) / spacing.X * spacing.X;
        var firstY = anchor.Y - (anchor.Y - top) / spacing.Y * spacing.Y;
        var cells = new List<ScreenPoint>();
        for (var y = firstY; y <= bottom; y += spacing.Y)
            for (var x = firstX; x <= right; x += spacing.X) cells.Add(new(x, y));
        var start = cells.IndexOf(anchor);
        var obstacles = occupied.ToList(); var result = new List<ScreenPoint>();
        for (var offset = 0; offset < cells.Count && result.Count < count; offset++)
        {
            var cell = cells[(start + offset) % cells.Count];
            var bounds = new ScreenBounds(cell.X, cell.Y, cell.X + spacing.X, cell.Y + spacing.Y);
            if (obstacles.Any(bounds.Intersects)) continue;
            result.Add(cell);
        }
        return result.Count == count ? result : [];
    }
}
