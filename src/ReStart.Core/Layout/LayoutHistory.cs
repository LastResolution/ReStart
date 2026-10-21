using System.Text.Json;

namespace ReStart.Core;

/// <summary>Keeps bounded, in-memory layout undo/redo snapshots without retaining preferences or writing history to disk.</summary>
/// <param name="maximumEdits">Maximum retained edits in addition to the current baseline; values below one are treated as one.</param>
/// <param name="maximumBytes">Approximate managed-memory budget for UTF-16 JSON snapshots, in bytes; at least the current state is retained.</param>
public sealed class LayoutHistory(int maximumEdits = 100, long maximumBytes = 32 * 1024 * 1024)
{
    private readonly List<string> _states = [];
    private int _position;
    private long _bytes;

    /// <summary>Gets whether an earlier layout can be restored.</summary>
    public bool CanUndo => _position > 0;
    /// <summary>Gets whether a previously undone layout can be restored.</summary>
    public bool CanRedo => _position + 1 < _states.Count;

    /// <summary>Starts a fresh editing history, for example after loading the local document.</summary>
    /// <param name="document">Document whose groups establish the fresh history baseline.</param>
    public void Reset(LayoutDocument document)
    {
        _states.Clear(); _bytes = 0; _position = 0;
        Append(Serialize(document.Groups));
    }

    /// <summary>Records one successfully saved operation; identical saves do not consume history or discard redo.</summary>
    /// <param name="document">Document whose successfully saved groups are recorded; preferences are excluded.</param>
    public void Record(LayoutDocument document)
    {
        var state = Serialize(document.Groups);
        if (_states.Count == 0) { Reset(document); return; }
        if (_states[_position] == state) return;
        while (_states.Count > _position + 1) RemoveAt(_states.Count - 1);
        Append(state); _position++;
        while (_states.Count > 1 && (_states.Count > Math.Max(1, maximumEdits) + 1 || _bytes > Math.Max(1, maximumBytes)))
        { RemoveAt(0); _position--; }
    }

    /// <summary>Restores the previous tile/group snapshot, preserving current preferences and upgrade metadata.</summary>
    /// <param name="document">Document receiving restored group copies; preferences and upgrade metadata remain intact.</param>
    /// <returns>True when a previous snapshot replaced the groups; false when already at the oldest state.</returns>
    public bool Undo(LayoutDocument document)
    {
        if (!CanUndo) return false;
        document.Groups = Deserialize(_states[--_position]);
        return true;
    }

    /// <summary>Restores the next tile/group snapshot, preserving current preferences and upgrade metadata.</summary>
    /// <param name="document">Document receiving restored group copies; preferences and upgrade metadata remain intact.</param>
    /// <returns>True when the next snapshot replaced the groups; false when already at the newest state.</returns>
    public bool Redo(LayoutDocument document)
    {
        if (!CanRedo) return false;
        document.Groups = Deserialize(_states[++_position]);
        return true;
    }

    /// <summary>Restores the latest successful snapshot without changing the history cursor, for a failed write.</summary>
    /// <param name="document">Document receiving restored group copies; preferences and upgrade metadata remain intact.</param>
    public void RestoreCurrent(LayoutDocument document)
    {
        if (_states.Count > 0) document.Groups = Deserialize(_states[_position]);
    }

    /// <summary>Copies groups, tiles and folder children without sharing mutable objects with the source.</summary>
    /// <param name="groups">Group tree to clone through the bounded layout JSON representation.</param>
    /// <returns>A detached deep copy of groups, tiles and folder children with the same stable IDs.</returns>
    public static List<TileGroup> CopyGroups(IEnumerable<TileGroup> groups) => Deserialize(Serialize(groups));

    private static string Serialize(IEnumerable<TileGroup> groups) => JsonSerializer.Serialize(groups, LayoutStore.JsonOptions);
    private static List<TileGroup> Deserialize(string json) => JsonSerializer.Deserialize<List<TileGroup>>(json, LayoutStore.JsonOptions)!;
    // Count the retained UTF-16 strings and allow for object/list overhead rather
    // than undercounting ASCII snapshots using their smaller UTF-8 file size.
    private static long StorageBytes(string state) => (long)state.Length * sizeof(char) + 64;
    private void Append(string state) { _states.Add(state); _bytes += StorageBytes(state); }
    private void RemoveAt(int index) { _bytes -= StorageBytes(_states[index]); _states.RemoveAt(index); }
}
