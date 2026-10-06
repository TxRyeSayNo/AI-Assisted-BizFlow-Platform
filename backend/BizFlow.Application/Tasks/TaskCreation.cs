using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

public sealed record CreateTaskCommand(string Title, string? Description = null, string? Priority = null,
    string? Deadline = null, string[]? Checklist = null);
public sealed record TaskCreatedView(Guid TaskId, string Title, string Status, DateTimeOffset CreatedAt);
public sealed record StoredTaskCreation(string Fingerprint, TaskCreatedView Result);
public interface ITaskCreationStore
{
    Task<ITaskCreationTransaction> BeginAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken);
}
public interface ITaskCreationTransaction : IAsyncDisposable
{
    Task<StoredTaskCreation?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(WorkTask task, IReadOnlyList<TaskChecklistItem> checklist, AuditLog audit, CancellationToken cancellationToken);
}

public static partial class TaskCreationRules
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex AbsoluteDate();
    public static DateTimeOffset DatabaseTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }
    public static DateTimeOffset? ParseDeadline(string? value)
    {
        if (value is null) return null;
        if (!AbsoluteDate().IsMatch(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var result))
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_DEADLINE", "Supply an absolute ISO datetime with Z or an explicit UTC offset.");
        return DatabaseTime(result);
    }
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string? KeyHash(string? key)
    {
        if (key is null) return null;
        if (key.Length is < 1 or > 128 || key.Any(c => c is < '!' or > '~'))
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Use one opaque key of 1 to 128 visible ASCII characters.");
        return Hash(key);
    }
}

public sealed class TaskCreation(ITenantContext context, IResourceAuthorizer authorizer,
    ITaskCreationStore store, TimeProvider clock)
{
    private Task AuthorizeAsync(CancellationToken cancellationToken) =>
        authorizer.AuthorizeAsync("tasks.create", new(context.TenantId), cancellationToken: cancellationToken);

    public async Task<TaskCreatedView> CreateAsync(CreateTaskCommand input, string? key, CancellationToken cancellationToken)
    {
        await AuthorizeAsync(cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Task creation requires a tenant workspace.");
        var keyHash = TaskCreationRules.KeyHash(key);
        var deadline = TaskCreationRules.ParseDeadline(input.Deadline);
        var priorityCode = input.Priority?.Trim() ?? "MEDIUM";
        if (!Enum.GetValues<TaskPriority>().Any(p => TaskListCodes.Priority(p) == priorityCode))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a canonical task priority.");
        var priority = Enum.GetValues<TaskPriority>().Single(p => TaskListCodes.Priority(p) == priorityCode);
        WorkTask prototype;
        string[] titles;
        try
        {
            prototype = WorkTask.CreateDraft(tenantId, actorId, input.Title, clock.GetUtcNow(), input.Description, priority, deadline);
            titles = (input.Checklist ?? []).Select((title, index) => TaskChecklistItem.Create(prototype.Id, title, checked(index + 1)).Title).ToArray();
        }
        catch (ArgumentException)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a nonblank plain-text title and checklist titles up to 300 characters, and a valid plain-text description.");
        }
        var fingerprint = TaskCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1, title = prototype.Title, description = prototype.Description,
            priority = priorityCode, deadline = deadline?.ToString("O", CultureInfo.InvariantCulture), checklist = titles
        }));
        await using var transaction = await store.BeginAsync(tenantId, actorId, keyHash, cancellationToken);
        await AuthorizeAsync(cancellationToken); // A concurrent revocation may have completed while waiting for the replay lock.
        if (keyHash is not null && await transaction.FindReplayAsync(cancellationToken) is { } replay)
        {
            if (replay.Fingerprint != fingerprint)
                throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different task input.");
            return replay.Result;
        }
        var now = clock.GetUtcNow();
        if (deadline is not null && deadline <= now)
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_DEADLINE", "The deadline must be strictly later than server UTC now.");
        now = TaskCreationRules.DatabaseTime(now);
        var task = WorkTask.CreateDraft(tenantId, actorId, prototype.Title, now, prototype.Description, priority, deadline);
        var checklist = titles.Select((title, index) => TaskChecklistItem.Create(task.Id, title, index + 1)).ToArray();
        var audit = AuditLog.TaskCreated(task, checklist, now, keyHash, keyHash is null ? null : fingerprint);
        await transaction.CommitAsync(task, checklist, audit, cancellationToken);
        return new(task.Id, task.Title, "DRAFT", task.CreatedAt);
    }
}
