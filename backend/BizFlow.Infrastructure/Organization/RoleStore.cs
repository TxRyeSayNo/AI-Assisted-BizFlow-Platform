using BizFlow.Application.Common;
using BizFlow.Application.Organization;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BizFlow.Infrastructure.Organization;

public sealed class RoleStore(BizFlowDbContext db, ITenantContext context) : IRoleStore
{
    private void AssertTenant(Guid tenantId)
    {
        if (context.UserId is null || context.UserId == Guid.Empty || context.TenantId != tenantId || tenantId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Role configuration requires a tenant workspace.");
    }

    private IQueryable<Role> VisibleRoles(Guid tenantId) => db.Roles.Where(role => role.TenantId == tenantId ||
        (role.IsSystem && !db.RolePermissions.IgnoreQueryFilters().Any(link => link.RoleId == role.Id &&
            db.Permissions.IgnoreQueryFilters().Any(permission => permission.Id == link.PermissionId && permission.ScopeType == PermissionScope.Platform))));

    public async Task<RolePage> ListAsync(Guid tenantId, RoleFilter filter, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var query = VisibleRoles(tenantId).AsNoTracking();
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(role => role.Name.ToUpper().Contains(normalized));
        }
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(role => role.IsSystem).ThenBy(role => role.Name).ThenBy(role => role.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(role => new { role.Id, role.Name, role.IsSystem, role.Status, Version = EF.Property<uint>(role, "Version") })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(role => role.Id).ToArray();
        var links = await db.RolePermissions.AsNoTracking().Where(link => ids.Contains(link.RoleId)).ToListAsync(cancellationToken);
        var catalog = await db.Permissions.AsNoTracking().Where(p => p.ScopeType != PermissionScope.Platform).OrderBy(p => p.Code).ToListAsync(cancellationToken);
        return new(rows.Select(role => new RoleRow(role.Id, role.Name, role.IsSystem, role.Status.ToString().ToUpperInvariant(),
                links.Where(link => link.RoleId == role.Id).Select(link => link.PermissionId).Order().ToArray(), RoleETag.Format(role.Version))).ToArray(),
            filter.Page, filter.PageSize, total,
            catalog.Select(p => new PermissionOption(p.Id, p.Code, p.Module, p.Action, p.ScopeType.ToString().ToUpperInvariant())).ToArray());
    }

    public async Task<RoleRow> CreateAsync(Role role, AuditLog audit, CancellationToken cancellationToken)
    {
        AssertTenant(role.TenantId ?? Guid.Empty);
        db.Roles.Add(role);
        db.AuditLogs.Add(audit);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException failure) when (failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ApplicationFault(FaultKind.Conflict, "ROLE.NAME_EXISTS", "A role with this name already exists in this workspace."); }
        return Row(role, db.Entry(role).Property<uint>("Version").CurrentValue, []);
    }

    public async Task<IRolePermissionTransaction?> BeginPermissionsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        AssertTenant(tenantId);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Role row is the aggregate lock, including junction-table-only changes. Tenant predicate
            // is explicit in SQL and normal EF filters remain enabled.
            var role = await db.Roles.FromSqlInterpolated($"""
                SELECT *, xmin FROM "Role" WHERE "RoleId" = {roleId}
                AND ("TenantId" = {tenantId} OR "IsSystem") FOR UPDATE
                """).SingleOrDefaultAsync(cancellationToken);
            if (role is null || !await VisibleRoles(tenantId).AnyAsync(r => r.Id == roleId, cancellationToken))
            { await transaction.DisposeAsync(); return null; }
            var links = await db.RolePermissions.Where(link => link.RoleId == roleId).ToListAsync(cancellationToken);
            return new PermissionTransaction(db, transaction, role, links);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private static RoleRow Row(Role role, uint version, Guid[] ids) =>
        new(role.Id, role.Name, role.IsSystem, role.Status.ToString().ToUpperInvariant(), ids.Order().ToArray(), RoleETag.Format(version));

    private sealed class PermissionTransaction(BizFlowDbContext db, IDbContextTransaction transaction, Role role, List<RolePermission> links) : IRolePermissionTransaction
    {
        public Role Role => role;
        public uint Version => db.Entry(role).Property<uint>("Version").CurrentValue;
        public Guid[] PermissionIds => links.Select(link => link.PermissionId).Order().ToArray();
        public async Task<IReadOnlyList<Permission>> AllowedCatalogAsync(CancellationToken cancellationToken) =>
            await db.Permissions.AsNoTracking().Where(p => p.ScopeType != PermissionScope.Platform).ToListAsync(cancellationToken);

        public async Task<RoleRow> CommitAsync(Guid[] permissionIds, AuditLog audit, CancellationToken cancellationToken)
        {
            db.RolePermissions.RemoveRange(links.Where(link => !permissionIds.Contains(link.PermissionId)));
            var old = PermissionIds.ToHashSet();
            db.RolePermissions.AddRange(permissionIds.Where(id => !old.Contains(id)).Select(id => new RolePermission(role.Id, id)));
            // Always update the aggregate xmin, even if the clock has not advanced or the set is equal.
            db.Entry(role).Property(r => r.UpdatedAt).IsModified = true;
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var result = Row(role, Version, permissionIds);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
