using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Collaboration;

public sealed record LabelView(Guid LabelId, string Name, string Colour, bool Archived, int UsageCount);

public sealed record WorkItemLabelsView(string ObjectType, Guid ObjectId, IReadOnlyList<LabelView> Labels);

public interface ILabelStore
{
    Task<IReadOnlyList<(Label Label, int UsageCount)>> ListAsync(Guid tenantId, bool includeArchived,
        CancellationToken cancellationToken);
    Task<Label?> FindAsync(Guid tenantId, Guid labelId, CancellationToken cancellationToken);
    Task<Label?> FindByNameAsync(Guid tenantId, string normalizedName, CancellationToken cancellationToken);
    Task AddAsync(Label label, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);

    Task<bool> IsTaskVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken);
    Task<bool> IsRequestVisibleAsync(RequestReadScope scope, Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Label>> LabelsOnAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        CancellationToken cancellationToken);
    Task<WorkItemLabel?> FindAttachmentAsync(Guid tenantId, CommentObjectType objectType, Guid objectId,
        Guid labelId, CancellationToken cancellationToken);
    Task AddAttachmentAsync(WorkItemLabel attachment, CancellationToken cancellationToken);
}

/// <summary>
/// Label catalogue and the labels carried by one work item (F6).
///
/// Managing the catalogue is a tenant administration capability, so it reuses the existing
/// <c>roles.configure</c> permission rather than inventing a new one, which would mean editing the
/// seeded permission catalogue. Attaching a label is gated by the item's own read policy: if you
/// cannot open the task or request, you cannot tag it.
/// </summary>
public sealed class LabelService(
    ITenantContext context,
    TenantMembershipAuthorizer membership,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    ILabelStore store,
    TimeProvider clock)
{
    public const string ManagePermission = "roles.configure";
    public const int MaxLabelsPerItem = 20;

    public static CommentObjectType ParseObjectType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "TASK" => CommentObjectType.Task,
        "REQUEST" => CommentObjectType.Request,
        _ => throw new ApplicationFault(FaultKind.Validation, "LABEL.OBJECT_TYPE",
            "Use TASK or REQUEST as the work item type.")
    };

    // -------------------------------------------------------------------------------------------
    // Catalogue
    // -------------------------------------------------------------------------------------------

    /// <summary>Any tenant member may read the catalogue: labels are shown on items they can see.</summary>
    public async Task<IReadOnlyList<LabelView>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        var rows = await store.ListAsync(tenantId, includeArchived, cancellationToken);
        return [.. rows.Select(row => Map(row.Label, row.UsageCount))];
    }

    public async Task<LabelView> CreateAsync(string name, string colour, CancellationToken cancellationToken)
    {
        var tenantId = await RequireManageAsync(cancellationToken);
        var actorId = context.UserId ?? throw ApplicationFault.NotFound();
        var clean = Clean(name);
        colour = CleanColour(colour);
        var existing = await store.FindByNameAsync(tenantId, Label.Normalize(clean), cancellationToken);
        if (existing is not null)
            throw new ApplicationFault(FaultKind.Conflict, "LABEL.DUPLICATE_NAME",
                "A label with this name already exists in this workspace.");
        var label = Label.Create(tenantId, clean, colour, actorId, clock.GetUtcNow());
        await store.AddAsync(label, cancellationToken);
        await store.SaveAsync(cancellationToken);
        return Map(label, 0);
    }

    public async Task<LabelView> UpdateAsync(Guid labelId, string name, string colour, CancellationToken cancellationToken)
    {
        var tenantId = await RequireManageAsync(cancellationToken);
        var label = await store.FindAsync(tenantId, labelId, cancellationToken) ?? throw ApplicationFault.NotFound();
        var clean = Clean(name);
        colour = CleanColour(colour);
        var clash = await store.FindByNameAsync(tenantId, Label.Normalize(clean), cancellationToken);
        if (clash is not null && clash.Id != label.Id)
            throw new ApplicationFault(FaultKind.Conflict, "LABEL.DUPLICATE_NAME",
                "A label with this name already exists in this workspace.");
        if (label.Rename(clean, colour, clock.GetUtcNow()))
            await store.SaveAsync(cancellationToken);
        return Map(label, 0);
    }

    /// <summary>Retires a label. Items keep the tag in their history; it simply stops being offered.</summary>
    public async Task<LabelView> ArchiveAsync(Guid labelId, bool archived, CancellationToken cancellationToken)
    {
        var tenantId = await RequireManageAsync(cancellationToken);
        var label = await store.FindAsync(tenantId, labelId, cancellationToken) ?? throw ApplicationFault.NotFound();
        var now = clock.GetUtcNow();
        if (archived ? label.Archive(now) : label.Restore(now))
            await store.SaveAsync(cancellationToken);
        return Map(label, 0);
    }

    // -------------------------------------------------------------------------------------------
    // Labels on one work item
    // -------------------------------------------------------------------------------------------

    public async Task<WorkItemLabelsView> ReadAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var (tenantId, _, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        var labels = await store.LabelsOnAsync(tenantId, type, objectId, cancellationToken);
        return new(Code(type), objectId, [.. labels.Select(label => Map(label, 0))]);
    }

    public async Task<WorkItemLabelsView> AttachAsync(string objectType, Guid objectId, Guid labelId,
        CancellationToken cancellationToken)
    {
        var (tenantId, actorId, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        var label = await store.FindAsync(tenantId, labelId, cancellationToken) ?? throw ApplicationFault.NotFound();
        if (label.ArchivedAt is not null)
            throw new ApplicationFault(FaultKind.Validation, "LABEL.ARCHIVED",
                "This label has been retired and cannot be applied.");
        var current = await store.LabelsOnAsync(tenantId, type, objectId, cancellationToken);
        if (current.Any(existing => existing.Id == labelId)) return await ReadAsync(objectType, objectId, cancellationToken);
        if (current.Count >= MaxLabelsPerItem)
            throw new ApplicationFault(FaultKind.Validation, "LABEL.TOO_MANY",
                $"A work item can carry at most {MaxLabelsPerItem} labels.");

        var now = clock.GetUtcNow();
        var attachment = await store.FindAttachmentAsync(tenantId, type, objectId, labelId, cancellationToken);
        if (attachment is null)
            await store.AddAttachmentAsync(
                WorkItemLabel.Create(tenantId, type, objectId, labelId, actorId, now), cancellationToken);
        else
            attachment.Restore();
        await store.SaveAsync(cancellationToken);
        return await ReadAsync(objectType, objectId, cancellationToken);
    }

    public async Task<WorkItemLabelsView> DetachAsync(string objectType, Guid objectId, Guid labelId,
        CancellationToken cancellationToken)
    {
        var (tenantId, _, type) = await RequireVisibleAsync(objectType, objectId, cancellationToken);
        var attachment = await store.FindAttachmentAsync(tenantId, type, objectId, labelId, cancellationToken);
        if (attachment is not null && attachment.Remove(clock.GetUtcNow()))
            await store.SaveAsync(cancellationToken);
        return await ReadAsync(objectType, objectId, cancellationToken);
    }

    // -------------------------------------------------------------------------------------------

    private static LabelView Map(Label label, int usage) =>
        new(label.Id, label.Name, label.Colour, label.ArchivedAt is not null, usage);

    private static string Code(CommentObjectType type) => type == CommentObjectType.Task ? "TASK" : "REQUEST";

    /// <summary>
    /// Managing the catalogue is permission-gated, so it goes through
    /// <see cref="IResourceAuthorizer"/>. Active membership alone is not enough:
    /// <see cref="TenantMembershipAuthorizer"/> says as much in its own contract.
    /// </summary>
    private async Task<Guid> RequireManageAsync(CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync(ManagePermission, cancellationToken);
        await authorizer.AuthorizeAsync(ManagePermission, new(tenantId), cancellationToken: cancellationToken);
        return tenantId;
    }

    private static string CleanColour(string? colour) => Label.TryColour(colour)
        ?? throw new ApplicationFault(FaultKind.Validation, "LABEL.COLOUR",
            "A label colour must be a #RRGGBB value.");

    private static string Clean(string? name)
    {
        var text = (name ?? "").Trim();
        if (text.Length == 0 || text.Length > Label.MaxNameLength)
            throw new ApplicationFault(FaultKind.Validation, "LABEL.NAME",
                $"A label name must contain 1 to {Label.MaxNameLength} characters.");
        if (text.Any(char.IsControl))
            throw new ApplicationFault(FaultKind.Validation, "LABEL.NAME", "Use plain text for a label name.");
        return text;
    }

    private async Task<(Guid TenantId, Guid ActorId, CommentObjectType Type)> RequireVisibleAsync(
        string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var type = ParseObjectType(objectType);
        if (objectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "LABEL.OBJECT_REQUIRED", "A work item id is required.");
        if (context.UserId is not { } userId || userId == Guid.Empty || context.TenantId is not { } tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
        var access = await snapshots.ResolveAsync(userId, tenantId, cancellationToken);
        if (access is null || access.UserId != userId || access.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");

        var visible = type == CommentObjectType.Task
            ? TaskReadPolicy.Resolve(access) is { Decision.Allowed: true, Scope: { } taskScope } &&
              await store.IsTaskVisibleAsync(taskScope, objectId, cancellationToken)
            : RequestReadPolicy.Resolve(access) is { Decision.Allowed: true, Scope: { } requestScope } &&
              await store.IsRequestVisibleAsync(requestScope, objectId, cancellationToken);
        if (!visible)
        {
            await audit.RecordDeniedAccessAsync(userId, tenantId,
                type == CommentObjectType.Task ? "tasks.read" : "requests.read",
                AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        return (tenantId, userId, type);
    }
}
