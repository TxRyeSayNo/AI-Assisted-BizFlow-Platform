using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

public sealed record TaskTemplateView(Guid TemplateId, string Name, string TitleTemplate, string? Description,
    string? Priority, int? DeadlineOffsetDays, string? RecurrenceFrequency, int? RecurrenceDay,
    int? RecurrenceHourUtc, DateTimeOffset? NextRunAt, DateTimeOffset? LastRunAt, bool Archived);

public sealed record TaskTemplateInput(string Name, string TitleTemplate, string? Description = null,
    string? Priority = null, int? DeadlineOffsetDays = null, string? RecurrenceFrequency = null,
    int? RecurrenceDay = null, int? RecurrenceHourUtc = null);

public interface ITaskTemplateStore
{
    Task<IReadOnlyList<TaskTemplate>> ListAsync(Guid tenantId, bool includeArchived, CancellationToken cancellationToken);
    Task<TaskTemplate?> FindAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken);
    Task<TaskTemplate?> FindByNameAsync(Guid tenantId, string normalizedName, CancellationToken cancellationToken);
    Task AddAsync(TaskTemplate template, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The task template catalogue (F9) and its optional recurrence (F8).
///
/// Creating a task from a template goes through the ordinary <see cref="TaskCreation"/> command, so
/// nothing here can bypass the permission check, the idempotency guard, the audit entry or the
/// lifecycle policy — the template only supplies the field values a person would have typed.
///
/// Managing the catalogue is a tenant administration capability, so it reuses the existing
/// <c>roles.configure</c> permission rather than adding one to the seeded catalogue.
/// </summary>
public sealed class TaskTemplateService(
    ITenantContext context,
    TenantMembershipAuthorizer membership,
    IResourceAuthorizer authorizer,
    ITaskTemplateStore store,
    TaskCreation creation,
    TimeProvider clock)
{
    public const string ManagePermission = "roles.configure";

    /// <summary>Any tenant member may read the catalogue; using a template still needs tasks.create.</summary>
    public async Task<IReadOnlyList<TaskTemplateView>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var rows = await store.ListAsync(tenantId, includeArchived, cancellationToken);
        return [.. rows.Select(Map)];
    }

    public async Task<TaskTemplateView> CreateAsync(TaskTemplateInput input, CancellationToken cancellationToken)
    {
        var tenantId = await RequireManageAsync(cancellationToken);
        var actorId = context.UserId ?? throw ApplicationFault.NotFound();
        var name = Clean(input.Name, TaskTemplate.MaxNameLength, "TEMPLATE.NAME");
        var title = Clean(input.TitleTemplate, TaskTemplate.MaxTitleLength, "TEMPLATE.TITLE");
        if (await store.FindByNameAsync(tenantId, TaskTemplate.Normalize(name), cancellationToken) is not null)
            throw new ApplicationFault(FaultKind.Conflict, "TEMPLATE.DUPLICATE_NAME",
                "A template with this name already exists in this workspace.");

        var now = clock.GetUtcNow();
        var template = Guard(() => TaskTemplate.Create(tenantId, name, title, input.Description, input.Priority,
            input.DeadlineOffsetDays, actorId, now));
        Guard(() =>
        {
            template.Schedule(Frequency(input.RecurrenceFrequency), input.RecurrenceDay, input.RecurrenceHourUtc, now);
            return template;
        });
        await store.AddAsync(template, cancellationToken);
        await store.SaveAsync(cancellationToken);
        return Map(template);
    }

    public async Task<TaskTemplateView> UpdateAsync(Guid templateId, TaskTemplateInput input,
        CancellationToken cancellationToken)
    {
        var tenantId = await RequireManageAsync(cancellationToken);
        var template = await store.FindAsync(tenantId, templateId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        var name = Clean(input.Name, TaskTemplate.MaxNameLength, "TEMPLATE.NAME");
        var title = Clean(input.TitleTemplate, TaskTemplate.MaxTitleLength, "TEMPLATE.TITLE");
        var clash = await store.FindByNameAsync(tenantId, TaskTemplate.Normalize(name), cancellationToken);
        if (clash is not null && clash.Id != template.Id)
            throw new ApplicationFault(FaultKind.Conflict, "TEMPLATE.DUPLICATE_NAME",
                "A template with this name already exists in this workspace.");

        var now = clock.GetUtcNow();
        Guard(() =>
        {
            template.Edit(name, title, input.Description, input.Priority, input.DeadlineOffsetDays, now);
            template.Schedule(Frequency(input.RecurrenceFrequency), input.RecurrenceDay, input.RecurrenceHourUtc, now);
            return template;
        });
        await store.SaveAsync(cancellationToken);
        return Map(template);
    }

    /// <summary>Retires a template. Tasks it already produced are untouched, and it stops firing.</summary>
    public async Task<TaskTemplateView> ArchiveAsync(Guid templateId, bool archived, CancellationToken cancellationToken)
    {
        var tenantId = await RequireManageAsync(cancellationToken);
        var template = await store.FindAsync(tenantId, templateId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        var now = clock.GetUtcNow();
        if (archived ? template.Archive(now) : template.Restore(now))
            await store.SaveAsync(cancellationToken);
        return Map(template);
    }

    /// <summary>
    /// Creates one task from a template (F9). The caller needs <c>tasks.create</c>, which
    /// <see cref="TaskCreation"/> enforces; reading the catalogue is not authority to use it.
    /// </summary>
    public async Task<TaskCreatedView> InstantiateAsync(Guid templateId, string? key,
        CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var template = await store.FindAsync(tenantId, templateId, cancellationToken)
            ?? throw ApplicationFault.NotFound();
        if (template.ArchivedAt is not null)
            throw new ApplicationFault(FaultKind.Validation, "TEMPLATE.ARCHIVED",
                "This template has been retired and cannot be used.");
        return await creation.CreateAsync(CommandFor(template, clock.GetUtcNow()), key, cancellationToken);
    }

    /// <summary>
    /// The create command a template produces. Shared with the recurring job so a scheduled task and
    /// a hand-made one are built the same way.
    /// </summary>
    public static CreateTaskCommand CommandFor(TaskTemplate template, DateTimeOffset now) =>
        new(template.TitleTemplate, template.Description, template.Priority,
            template.DeadlineFor(now)?.ToUniversalTime().ToString("O"));

    private static TaskTemplateView Map(TaskTemplate t) =>
        new(t.Id, t.Name, t.TitleTemplate, t.Description, t.Priority, t.DeadlineOffsetDays,
            t.RecurrenceFrequency, t.RecurrenceDay, t.RecurrenceHourUtc, t.NextRunAt, t.LastRunAt,
            t.ArchivedAt is not null);

    private async Task<Guid> RequireManageAsync(CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync(ManagePermission, cancellationToken);
        await authorizer.AuthorizeAsync(ManagePermission, new(tenantId), cancellationToken: cancellationToken);
        return tenantId;
    }

    private static string? Frequency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string Clean(string? value, int max, string code)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0 || text.Length > max)
            throw new ApplicationFault(FaultKind.Validation, code, $"Use 1 to {max} characters.");
        if (text.Any(char.IsControl))
            throw new ApplicationFault(FaultKind.Validation, code, "Use plain text.");
        return text;
    }

    /// <summary>
    /// Turns the domain's argument guards into application faults, so a bad recurrence is a 422 and
    /// never a 500.
    /// </summary>
    private static TaskTemplate Guard(Func<TaskTemplate> action)
    {
        try
        {
            return action();
        }
        catch (ArgumentException failure)
        {
            throw new ApplicationFault(FaultKind.Validation, "TEMPLATE.INVALID", failure.Message);
        }
    }
}
