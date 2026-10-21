namespace ReStart.Core;

/// <summary>Stores root tiles and an optional position on the full-screen group grid.</summary>
public sealed class TileGroup
{
    /// <summary>Gets or sets an absolute horizontal board anchor in logical cells; null uses the legacy group-column anchor.</summary>
    public double? X { get; set; }
    /// <summary>Gets or sets whether this entry is one independent tile with no group header or reserved group width.</summary>
    public bool IsStandalone { get; set; }
    /// <summary>Gets or sets a stable identifier unique within the layout.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Gets or sets a user-owned group label.</summary>
    public string Name { get; set; } = L.T(K.NewGroup);
    /// <summary>Gets or sets a horizontal group-column anchor; null uses automatic placement for legacy layouts.</summary>
    public int? Column { get; set; }
    /// <summary>Gets or sets the vertical anchor in half-cell steps; independent tiles share the named groups' tile baseline.</summary>
    public int? Row { get; set; }
    /// <summary>Gets or sets the root tiles in this group.</summary>
    public List<Tile> Tiles { get; set; } = [];
}
