namespace ReStart.Core;

/// <summary>Reuses vacated slots in one horizontal or vertical stack, measured from the committed baseline.</summary>
internal static class GroupReorder
{
    internal static IReadOnlyList<GroupPlacement>? Plan(IReadOnlyList<GroupPlacement> current, int movingIndex,
        double left, int row, int tileColumns, double cell)
    {
        const double epsilon = .001;
        var moving = current[movingIndex];
        if (moving.Group.IsStandalone) return null;
        var top = row * cell / 2;
        var vertical = Math.Abs(left - moving.Left) < epsilon;
        if (vertical ? Math.Abs(top - moving.Top) < epsilon : Math.Abs(top - moving.Top) >= epsilon) return null;
        double Position(GroupPlacement p) => vertical ? p.Top : p.Left;
        double Length(GroupPlacement p) => vertical ? p.Height : p.Width;
        double Across(GroupPlacement p) => vertical ? p.Left : p.Top;
        double Breadth(GroupPlacement p) => vertical ? p.Width : p.Height;
        var position = vertical ? top : left;
        var forward = position > Position(moving);
        var lane = current.Select((placement, index) => (Placement: placement, Index: index))
            .Where(p => !p.Placement.Group.IsStandalone && Across(p.Placement) < Across(moving) + Breadth(moving) - epsilon
                && Across(p.Placement) + Breadth(p.Placement) > Across(moving) + epsilon)
            .OrderBy(p => Position(p.Placement)).ThenBy(p => p.Index).ToArray();
        var overlapping = lane.Any(p => p.Index != movingIndex && position < Position(p.Placement) + Length(p.Placement)
            && position + Length(moving) > Position(p.Placement));
        var source = Array.FindIndex(lane, p => p.Index == movingIndex);
        var destination = source;
        if (forward)
        {
            for (var i = source + 1; i < lane.Length; i++)
            {
                var target = lane[i].Placement;
                if (position + Length(moving) < Position(target) + Math.Min(Length(moving), Length(target)) / 2 - epsilon) break;
                destination = i;
            }
        }
        else
        {
            for (var i = source - 1; i >= 0; i--)
            {
                var target = lane[i].Placement;
                if (position > Position(target) + Length(target) - Math.Min(Length(moving), Length(target)) / 2 + epsilon) break;
                destination = i;
            }
        }
        if (source == destination) return overlapping ? current : null;
        var run = lane.Skip(Math.Min(source, destination)).Take(Math.Abs(source - destination) + 1).ToArray();
        // Parallel stacks do not form a single sequence; leave them to local
        // directional displacement and ordinary collision handling.
        for (var i = 1; i < run.Length; i++)
            if (Position(run[i].Placement) < Position(run[i - 1].Placement) + Length(run[i - 1].Placement) - epsilon) return null;
        var reordered = run.Where(p => p.Index != movingIndex).ToList();
        reordered.Insert(forward ? reordered.Count : 0, lane[source]);
        var result = current.ToArray();
        var next = Position(run[0].Placement);
        var pitch = tileColumns * cell + GroupLayout.Gap;
        GroupPlacement At(GroupPlacement p, double value) => vertical
            ? p with { Row = (int)Math.Round(value / (cell / 2), MidpointRounding.AwayFromZero), Top = value }
            : p with { Column = Math.Clamp((int)Math.Floor(value / pitch), 0, 63), Left = value / cell * cell };
        for (var i = 0; i < reordered.Count; i++)
        {
            var candidate = At(reordered[i].Placement, next);
            if (candidate.Row is < 0 or > 20000 || candidate.Left < 0 || candidate.Left > 10000 * cell) return null;
            result[reordered[i].Index] = candidate;
            if (i < run.Length - 1)
                next += Length(reordered[i].Placement) + Math.Max(0, Position(run[i + 1].Placement) - Position(run[i].Placement) - Length(run[i].Placement));
        }
        // Retain the reordered recipients after passing them, while allowing
        // the dragged group to continue into empty space beyond the last slot.
        if (!overlapping && (forward ? position > Position(result[movingIndex]) : position < Position(result[movingIndex])))
            result[movingIndex] = At(result[movingIndex], position);
        return result;
    }
}
