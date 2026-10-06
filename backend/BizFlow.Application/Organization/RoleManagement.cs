using System.Globalization;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Organization;

public sealed record RoleFilter(int Page = 1, int PageSize = 25, string? Search = null);
public sealed record PermissionOption(Guid PermissionId, string Code, string Module, string Action, string Scope);
public sealed record RoleRow(Guid RoleId, string Name, bool IsSystem, string Status, Guid[] PermissionIds, string ETag);
public sealed record RolePage(IReadOnlyList<RoleRow> Items, int Page, int PageSize, long Total, IReadOnlyList<PermissionOption> AvailablePermissions);

public interface IRoleStore
{
    Task<RolePage> ListAsync(Guid tenantId, RoleFilter filter, CancellationToken cancellationToken);
    Task<RoleRow> CreateAsync(Role role, AuditLog audit, CancellationToken cancellationToken);
    Task<IRolePermissionTransaction?> BeginPermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);
}

public interface IRolePermissionTransaction : IAsyncDisposable
{
    Role Role { get; }
    uint Version { get; }
    Guid[] PermissionIds { get; }
    Task<IReadOnlyList<Permission>> AllowedCatalogAsync(CancellationToken cancellationToken);
    Task<RoleRow> CommitAsync(Guid[] permissionIds, AuditLog audit, CancellationToken cancellationToken);
}

public sealed class RoleManagement(ITenantContext context, IResourceAuthorizer authorizer, ISecurityAuditWriter securityAudit, IRoleStore store, TimeProvider clock)
{
    public const string ConfigurePermission = "roles.configure";

    private async Task<Guid> AuthorizeAsync(CancellationToken cancellationToken)
    {
        // Tenant-plane authority only. The resource has no owner/department so narrower grants
        // cannot accidentally act as company-wide role administration.
        await authorizer.AuthorizeAsync(ConfigurePermission, new(context.TenantId), cancellationToken: cancellationToken);
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Role configuration requires a tenant workspace.");
    }

    public async Task<RolePage> ListAsync(RoleFilter filter, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue || search?.Length > 100)
            throw Validation();
        return await store.ListAsync(tenantId, filter with { Search = search }, cancellationToken);
    }

    public async Task<RoleRow> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw Validation();
        var now = clock.GetUtcNow();
        var role = Role.CreateCustom(tenantId, name, now);
        return await store.CreateAsync(role, AuditLog.RoleConfiguration(tenantId, context.UserId!.Value,
            role.Id, role.Name, null, [], now), cancellationToken);
    }

    public async Task<RoleRow> SetPermissionsAsync(Guid roleId, string? ifMatch, Guid[]? permissionIds, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);
        await using var transaction = await store.BeginPermissionsAsync(tenantId, roleId, cancellationToken);
        if (transaction is null)
        {
            await securityAudit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        // A waiter must not retain authority revoked while waiting for the role lock.
        await AuthorizeAsync(cancellationToken);
        var role = transaction.Role;
        if (role.IsSystem)
        {
            await securityAudit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.PermissionDenied, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ROLE.SYSTEM_PROTECTED", "System role permissions cannot be changed.");
        }
        if (ifMatch is null || ifMatch.Length < 3 || ifMatch[0] != '"' || ifMatch[^1] != '"' ||
            !uint.TryParse(ifMatch.AsSpan(1, ifMatch.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var expected))
            throw new ApplicationFault(FaultKind.Validation, "ROLE.ETAG_REQUIRED", "Send the role's current ETag in If-Match.");
        if (expected != transaction.Version)
            throw new ApplicationFault(FaultKind.Conflict, "ROLE.VERSION_CONFLICT", "This role changed. Reload it before saving.",
                new Dictionary<string, string[]> { ["etag"] = [RoleETag.Format(transaction.Version)], ["permissionIds"] = transaction.PermissionIds.Select(id => id.ToString()).ToArray() });
        if (permissionIds is null || permissionIds.Length > 500 || permissionIds.Any(id => id == Guid.Empty) || permissionIds.Distinct().Count() != permissionIds.Length)
            throw Validation();
        var catalog = await transaction.AllowedCatalogAsync(cancellationToken);
        var selected = catalog.Where(p => permissionIds.Contains(p.Id)).ToArray();
        if (selected.Length != permissionIds.Length)
        {
            await securityAudit.RecordDeniedAccessAsync(context.UserId, context.TenantId, ConfigurePermission, AccessDenial.PermissionDenied, cancellationToken);
            throw new ApplicationFault(FaultKind.Validation, "ROLE.PERMISSION_NOT_ALLOWED", "Choose permissions from this workspace's allowed catalog.");
        }
        var now = clock.GetUtcNow();
        role.ConfigurePermissions(selected.Select(p => p.ScopeType).ToArray(), now);
        var audit = AuditLog.RoleConfiguration(tenantId, context.UserId!.Value, role.Id, role.Name,
            transaction.PermissionIds, permissionIds, now);
        return await transaction.CommitAsync(permissionIds, audit, cancellationToken);
    }

    private static ApplicationFault Validation() => new(FaultKind.Validation, "VALIDATION.FAILED", "Use a valid role name, permission set and page size from 1 to 100.");
}

public static class RoleETag
{
    public static string Format(uint version) => "\"" + version.ToString(CultureInfo.InvariantCulture) + "\"";
}
