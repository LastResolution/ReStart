namespace ReStart.Core;

/// <summary>Plans local group displacement toward the incoming group's source side without moving unrelated entries.</summary>
internal static class GroupDisplacement
{
    private enum Side { Right, Left, Down, Up }
    private const double Epsilon = .001;

    internal static IReadOnlyList<GroupPlacement>? Plan(IReadOnlyList<GroupPlacement> current, int movingIndex,
        int column, int row, double left, int columns, int tileColumns, double cell)
    {
        var origin = current[movingIndex];
        if (origin.Group.IsStandalone) return null;
        var moving = origin with { Column = column, Row = row, Left = left, Top = row * cell / 2 };
        var dx = origin.Left - moving.Left;
        var dy = origin.Top - moving.Top;
        if (Math.Abs(dx) < Epsilon && Math.Abs(dy) < Epsilon) return null;
        var side = Math.Abs(dx) / origin.Width >= Math.Abs(dy) / origin.Height
            ? dx > 0 ? Side.Right : Side.Left
            : dy > 0 ? Side.Down : Side.Up;
        var collisions = current.Select((placement, index) => (Placement: placement, Index: index))
            .Where(p => p.Index != movingIndex && Intersects(moving, p.Placement)).ToArray();
        if (collisions.Length == 0 || collisions.Any(p => p.Placement.Group.IsStandalone)) return null;
        var horizontal = side is Side.Right or Side.Left;
        // A shallow approach keeps its recipient still, matching vertical
        // reorder intent and avoiding displacement from a grazing edge.
        if (collisions.Any(p => (horizontal
                ? Math.Min(moving.Left + moving.Width, p.Placement.Left + p.Placement.Width) - Math.Max(moving.Left, p.Placement.Left)
                : Math.Min(moving.Top + moving.Height, p.Placement.Top + p.Placement.Height) - Math.Max(moving.Top, p.Placement.Top))
            < Math.Min(horizontal ? moving.Width : moving.Height, horizontal ? p.Placement.Width : p.Placement.Height) / 2 - Epsilon)) return current;

        var displacedIds = collisions.Select(p => p.Index).ToHashSet();
        var fixedEntries = current.Where((_, i) => i != movingIndex && !displacedIds.Contains(i)).Append(moving).ToArray();
        var pitch = tileColumns * cell + GroupLayout.Gap;
        var rightLimit = Math.Max(columns * pitch - GroupLayout.Gap, Math.Max(current.Max(p => p.Left + p.Width), moving.Left + moving.Width));
        var placed = new List<GroupPlacement>();
        var result = current.ToArray(); result[movingIndex] = moving;
        foreach (var (recipient, index) in collisions.OrderBy(p => side switch
        {
            Side.Right => p.Placement.Left,
            Side.Left => -p.Placement.Left - p.Placement.Width,
            Side.Down => p.Placement.Top,
            _ => -p.Placement.Top - p.Placement.Height
        }).ThenBy(p => p.Index))
        {
            var candidate = Beside(recipient, moving, side, pitch, cell);
            // Several direct recipients may occupy the same strip. Keep their
            // order and advance only past recipients of this same collision.
            for (var attempt = 0; attempt < placed.Count; attempt++)
            {
                var blockers = placed.Where(p => Intersects(candidate, p)).ToArray();
                if (blockers.Length == 0) break;
                var edge = blockers.OrderBy(p => side switch
                {
                    Side.Right => -p.Left - p.Width,
                    Side.Left => p.Left,
                    Side.Down => -p.Top - p.Height,
                    _ => p.Top
                }).First();
                candidate = Beside(candidate, edge, side, pitch, cell);
            }
            if (candidate.Left < 0 || candidate.Left > 10000 * cell || candidate.Left + candidate.Width > rightLimit + Epsilon
                || candidate.Row is < 0 or > 20000 || fixedEntries.Concat(placed).Any(p => Intersects(candidate, p))) return null;
            placed.Add(candidate); result[index] = candidate;
        }
        return result;
    }

    private static GroupPlacement Beside(GroupPlacement recipient, GroupPlacement obstacle, Side side, double pitch, double cell)
    {
        var left = side switch
        {
            Side.Right => obstacle.Left + obstacle.Width + GroupLayout.Gap,
            Side.Left => obstacle.Left - recipient.Width - GroupLayout.Gap,
            _ => recipient.Left
        };
        // Use the same cell-origin representation that persistence and Pack
        // will read, including fractional gutter anchors at noninteger scale.
        left = left / cell * cell;
        var half = cell / 2;
        var row = side switch
        {
            Side.Down => (int)Math.Ceiling((obstacle.Top + obstacle.Height + GroupLayout.Gap) / half),
            Side.Up => (int)Math.Floor((obstacle.Top - recipient.Height - GroupLayout.Gap) / half),
            _ => recipient.Row
        };
        return recipient with { Column = Math.Clamp((int)Math.Floor(left / pitch), 0, 63), Row = row, Left = left, Top = row * half };
    }

    private static bool Intersects(GroupPlacement a, GroupPlacement b)
        => a.Left < b.Left + b.Width - Epsilon && a.Left + a.Width > b.Left + Epsilon
        && a.Top < b.Top + b.Height - Epsilon && a.Top + a.Height > b.Top + Epsilon;
}
