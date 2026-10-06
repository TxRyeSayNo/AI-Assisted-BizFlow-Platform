using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Sla;

namespace BizFlow.Application.Sla;

public sealed record SlaProfileFilter(int Page = 1, int PageSize = 25, string? Search = null, string? Status = null);
public sealed record SlaProfileRow(Guid SlaProfileId, string Name, string Status);
public sealed record SlaProfilePage(IReadOnlyList<SlaProfileRow> Items, int Page, int PageSize, long Total);
public interface ISlaProfileStore
{
    Task<SlaProfilePage> ListAsync(Guid tenantId, SlaProfileFilter filter, CancellationToken cancellationToken);
    Task<SlaProfileRow> CreateAsync(SlaProfile profile, AuditLog audit, CancellationToken cancellationToken);
}

public sealed class SlaProfileCatalog(ITenantContext context, IResourceAuthorizer authorizer, ISlaProfileStore store, TimeProvider clock)
{
    public const string ReadPermission = "sla.read";
    public const string ConfigurePermission = "sla.configure";
    private async Task<Guid> AuthorizeAsync(string permission, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(permission, new(context.TenantId), cancellationToken: cancellationToken);
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "SLA configuration requires a tenant workspace.");
    }
    public async Task<SlaProfilePage> ListAsync(SlaProfileFilter filter, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ReadPermission, cancellationToken);
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            search?.Length > 200 || status is not (null or "DRAFT" or "ACTIVE" or "INACTIVE")) throw Validation();
        return await store.ListAsync(tenantId, filter with { Search = search, Status = status }, cancellationToken);
    }
    public async Task<SlaProfileRow> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(ConfigurePermission, cancellationToken);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw Validation();
        var profile = SlaProfile.CreateDraft(tenantId, name);
        return await store.CreateAsync(profile, AuditLog.SlaProfileCreated(tenantId, context.UserId!.Value,
            profile.Id, profile.Name, clock.GetUtcNow()), cancellationToken);
    }
    private static ApplicationFault Validation() => new(FaultKind.Validation, "VALIDATION.FAILED", "Use a name up to 200 characters, a supported profile status and a page size from 1 to 100.");
}
