using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;

namespace BizFlow.Application.Tasks;

public sealed record BulkItemResult(Guid TaskId, bool Succeeded, string? Code, string? Message);

public sealed record BulkResult(int Requested, int Succeeded, int Failed, IReadOnlyList<BulkItemResult> Items);

/// <summary>
/// Applies one action to several tasks at once (F10).
///
/// There is no bulk UPDATE here on purpose: the whole point is that each task goes through the
/// very same command a single task would, so permissions, the lifecycle policy, audit, SLA and
/// idempotency behave identically whether one task or fifty were selected. The cost is one round
/// trip per task; the benefit is that a bulk action can never become a back door around the rules.
///
/// Failures are per item. One task that cannot move does not roll back the ones that already did,
/// and the caller gets told exactly which ones failed and why.
/// </summary>
public sealed class TaskBulkOperations(
    TaskAssignmentCommand assignment,
    RecordService records)
{
    /// <summary>Bounded so one request cannot hold a connection for an unbounded time.</summary>
    public const int MaxItems = 50;

    public async Task<BulkResult> AssignAsync(IReadOnlyCollection<Guid> taskIds, string targetType, Guid targetId,
        string? note, CancellationToken cancellationToken)
    {
        var ids = Clean(taskIds);
        if (targetId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "BULK.TARGET_REQUIRED",
                "Choose who the selected tasks should go to.");
        var results = new List<BulkItemResult>(ids.Count);
        foreach (var taskId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunAsync(taskId, async () =>
                // The single-task assignment command, unchanged: it re-checks the caller's
                // authority over this particular task and writes its own audit entry.
                await assignment.AssignAsync(taskId, new(targetType, targetId, note), null, cancellationToken)));
        }
        return Summarize(ids.Count, results);
    }

    public async Task<BulkResult> ArchiveAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken)
    {
        var ids = Clean(taskIds);
        var results = new List<BulkItemResult>(ids.Count);
        foreach (var taskId in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunAsync(taskId, async () =>
                // The single-record archival command, unchanged.
                await records.ArchiveRecordAsync("task", taskId, cancellationToken)));
        }
        return Summarize(ids.Count, results);
    }

    private static List<Guid> Clean(IReadOnlyCollection<Guid> taskIds)
    {
        var ids = taskIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            throw new ApplicationFault(FaultKind.Validation, "BULK.EMPTY", "Select at least one task.");
        if (ids.Count > MaxItems)
            throw new ApplicationFault(FaultKind.Validation, "BULK.TOO_MANY",
                $"Select at most {MaxItems} tasks at a time.");
        return ids;
    }

    /// <summary>
    /// Runs one item and turns an application fault into a reported failure rather than letting it
    /// abort the whole batch. Anything that is not an application fault still propagates, because
    /// an unexpected error is not a per-item outcome.
    /// </summary>
    private static async Task<BulkItemResult> RunAsync(Guid taskId, Func<Task> action)
    {
        try
        {
            await action();
            return new(taskId, true, null, null);
        }
        catch (ApplicationFault fault)
        {
            return new(taskId, false, fault.Code, fault.Message);
        }
    }

    private static BulkResult Summarize(int requested, List<BulkItemResult> results) =>
        new(requested, results.Count(r => r.Succeeded), results.Count(r => !r.Succeeded), results);
}
