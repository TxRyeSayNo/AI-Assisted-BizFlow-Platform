using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Tasks;

public interface IRecurringTaskDispatcher
{
    /// <summary>Creates a task for every schedule that is due. Returns how many were created.</summary>
    Task<int> SweepAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Creates the tasks that recurring templates are due to produce (F8).
///
/// The job acts as the person who set the schedule up, not as an anonymous system actor: that
/// keeps the generated task's creator a real, accountable account, and it is what lets the write
/// pass the context's own persistence boundary unchanged. A template whose owner is no longer
/// active is skipped rather than retried, and its schedule still moves on so it cannot pile up.
///
/// Each template is handled in its own transaction, so one failure neither rolls back nor blocks
/// the others. The schedule always steps forward from "now", never from the slot it missed, so an
/// outage cannot queue a backlog of past occurrences.
/// </summary>
public sealed class RecurringTaskDispatcher(
    DbContextOptions<BizFlowDbContext> options,
    TimeProvider clock,
    ILogger<RecurringTaskDispatcher> logger) : IRecurringTaskDispatcher
{
    /// <summary>A guard so one sweep can never run away with the database.</summary>
    public const int MaxPerSweep = 200;

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var created = 0;
        foreach (var due in await DueAsync(now, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                if (await RunAsync(due, now, cancellationToken)) created++;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // One broken template must not stop the rest of the sweep.
                logger.LogWarning(failure, "Recurring template {TemplateId} failed to produce a task.", due.TemplateId);
            }
        }
        return created;
    }

    private async Task<bool> RunAsync(DueTemplate due, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Open the workspace as the template's owner, so the created task has a real creator and the
        // ordinary persistence boundary applies untouched.
        await using var db = new BizFlowDbContext(options, new JobContext(due.TenantId, due.OwnerId));
        var template = await db.TaskTemplates
            .SingleOrDefaultAsync(t => t.Id == due.TemplateId, cancellationToken);
        // Re-read under the tenant filter: the template may have been archived or rescheduled since
        // the scan, and the owner may have been deactivated.
        if (template is null || template.ArchivedAt is not null || template.NextRunAt is null ||
            template.NextRunAt > now) return false;

        var ownerActive = await db.Users.AnyAsync(u => u.Id == due.OwnerId, cancellationToken);
        if (!ownerActive)
        {
            logger.LogInformation(
                "Recurring template {TemplateId} skipped: its owner is no longer an active account.",
                template.Id);
            template.RecordRun(now);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        // Created as a draft, exactly as a person creating it by hand would leave it: no state
        // change is invented here, and nothing is assigned automatically.
        db.Add(WorkTask.CreateDraft(template.TenantId, due.OwnerId, template.TitleTemplate, now,
            template.Description, Priority(template.Priority), template.DeadlineFor(now)));
        template.RecordRun(now);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The template's priority code as the task entity's own enum. An unrecognised code falls back
    /// to the task default rather than failing the sweep.
    /// </summary>
    private static TaskPriority Priority(string? code) =>
        Enum.TryParse<TaskPriority>(code, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : TaskPriority.Medium;

    private async Task<IReadOnlyList<DueTemplate>> DueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // One scan across tenants to find what is due; each one is then opened in its own workspace.
        await using var db = new BizFlowDbContext(options, new JobContext(null, null));
        return await db.Database
            .SqlQuery<DueTemplate>($"""
                SELECT "TaskTemplateId" AS "TemplateId", "TenantId", "CreatedBy" AS "OwnerId"
                FROM "TaskTemplate"
                WHERE "NextRunAt" IS NOT NULL AND "ArchivedAt" IS NULL AND "NextRunAt" <= {now}
                ORDER BY "NextRunAt"
                LIMIT {MaxPerSweep}
                """)
            .ToListAsync(cancellationToken);
    }

    private sealed record DueTemplate(Guid TemplateId, Guid TenantId, Guid OwnerId);

    private sealed class JobContext(Guid? tenantId, Guid? userId) : BizFlow.Application.Security.ITenantContext
    {
        public Guid? UserId { get; } = userId;
        public Guid? TenantId { get; } = tenantId;
    }
}
