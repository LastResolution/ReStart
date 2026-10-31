namespace ReStart.App.Services;

/// <summary>Recovers only orphaned, validated ownership records while holding the same cross-process lock as acquisition.</summary>
internal static class TaskbarRecovery
{
    /// <summary>Restores an abandoned record or reports that recovery is blocked, without replacing another live owner's record.</summary>
    /// <param name="store">Private durable store; invalid records remain untouched.</param>
    /// <param name="backend">Identity-validating restoration backend; tests supply an inert fake.</param>
    /// <param name="isAlive">Returns whether this exact process ID and start-time identity is still alive; uncertainty must return true.</param>
    /// <param name="expectedParent">Optional watchdog owner identity. A record for any other process is left untouched.</param>
    /// <returns>True for no pending record, completed recovery, or a different watchdog owner; false when recovery is blocked or fails.</returns>
    internal static bool Run(ITaskbarRecoveryStore store, ITaskbarLeaseBackend backend, Func<int, long, bool> isAlive,
        (int Id, long Started)? expectedParent = null)
    {
        try
        {
            using var ownership = store.Acquire();
            var state = store.Read();
            if (state == null) return true;
            if (!state.IsValid()) return false;
            if (expectedParent is { } parent && (state.ParentId != parent.Id || state.ParentStarted != parent.Started)) return true;
            if (isAlive(state.ParentId, state.ParentStarted)) return false;
            backend.Restore(state);
            store.Delete(state.Token);
            return true;
        }
        catch (Exception error) when (TaskbarLease.Expected(error)) { return false; }
    }
}
