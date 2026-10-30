using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReStart.App.Services;

internal sealed record NativeJumpRequest(int Version, string Operation, string AppId, string Key = "", string Target = "");
internal sealed record NativeJumpItem(string Key, string Name, string Target, bool IsDirectory, bool IsLink, bool IsSeparator = false);
internal sealed record NativeJumpSnapshot(bool TasksSupported, bool PinsSupported, NativeJumpItem[] Tasks, NativeJumpItem[] Pinned)
{
    internal static NativeJumpSnapshot Empty => new(false, false, [], []);
}
internal sealed record NativeJumpReply(bool Success, string Error, string AppId, NativeJumpSnapshot Snapshot);

/// <summary>Bounds the private parent/worker protocol; no executable or argument string is accepted from a caller.</summary>
internal static class NativeJumpContract
{
    internal const int MaximumInputBytes = 128 * 1024;
    internal const int MaximumOutputBytes = 256 * 1024;
    internal static readonly JsonSerializerOptions Json = new()
    {
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    internal static bool ValidRequest(NativeJumpRequest? request) => request is { Version: 1 }
        && Safe(request.AppId, 256, false) && Safe(request.Key, 64) && Safe(request.Target, 32767)
        && request.Operation switch
        {
            "read" => request.Key.Length == 0 && request.Target.Length == 0,
            "pin" => request.Key.Length == 0 && request.Target.Length > 0,
            "unpin" or "invoke-task" or "invoke-pinned" => IsKey(request.Key) && request.Target.Length == 0,
            _ => false
        };

    internal static bool ValidReply(NativeJumpReply? reply, string owner) => reply != null
        && string.Equals(reply.AppId, owner, StringComparison.OrdinalIgnoreCase)
        && reply.Error is "" or "unsupported" or "unavailable" or "invalid-request"
        && reply.Snapshot is { Tasks: { Length: <= 64 }, Pinned: { Length: <= 64 } } snapshot
        && snapshot.Tasks.Concat(snapshot.Pinned).All(item => item != null && Safe(item.Name, 512)
            && Safe(item.Target, 32767) && (item.IsSeparator ? item.Key == "" : IsKey(item.Key)))
        && snapshot.Pinned.All(item => !item.IsSeparator)
        && snapshot.Tasks.Where(item => !item.IsSeparator).Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() == snapshot.Tasks.Count(item => !item.IsSeparator)
        && snapshot.Pinned.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() == snapshot.Pinned.Length;

    private static bool Safe(string? value, int maximum, bool empty = true) => value != null && value.Length <= maximum
        && (empty || value.Length > 0) && !value.Any(char.IsControl);
    private static bool IsKey(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
