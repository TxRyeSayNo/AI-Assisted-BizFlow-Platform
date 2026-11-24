using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

public sealed record SavedFilterView(Guid SavedFilterId, string Scope, string Name, string Query,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public interface ISavedFilterStore
{
    Task<IReadOnlyList<SavedFilter>> ListAsync(Guid tenantId, Guid userId, string scope,
        CancellationToken cancellationToken);
    Task<SavedFilter?> FindAsync(Guid tenantId, Guid userId, Guid savedFilterId, CancellationToken cancellationToken);
    Task<SavedFilter?> FindByNameAsync(Guid tenantId, Guid userId, string scope, string normalizedName,
        CancellationToken cancellationToken);
    Task<int> CountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task AddAsync(SavedFilter filter, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A person's own saved list filters (F12).
///
/// These are private to their owner, so every operation is scoped to the signed-in user as well as
/// the tenant and no permission beyond active membership is involved — there is nothing here anyone
/// else can read. A saved filter grants no visibility of its own either: the list endpoints
/// re-validate the query and re-apply their read policy on every call, so saving a filter can never
/// widen what its owner may see.
/// </summary>
public sealed class SavedFilterService(
    ITenantContext context,
    TenantMembershipAuthorizer membership,
    ISavedFilterStore store,
    TimeProvider clock)
{
    /// <summary>Enough for real use, low enough that nobody can fill a table with them.</summary>
    public const int MaxPerUser = 50;

    public async Task<IReadOnlyList<SavedFilterView>> ListAsync(string? scope, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireAsync(cancellationToken);
        var rows = await store.ListAsync(tenantId, userId, Scope(scope), cancellationToken);
        return [.. rows.Select(Map)];
    }

    public async Task<SavedFilterView> CreateAsync(string? scope, string name, string? query,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireAsync(cancellationToken);
        var cleanScope = Scope(scope);
        var now = clock.GetUtcNow();

        var existing = await store.FindByNameAsync(tenantId, userId, cleanScope,
            SavedFilter.Normalize(Clean(name)), cancellationToken);
        if (existing is { RemovedAt: null })
            throw new ApplicationFault(FaultKind.Conflict, "FILTER.DUPLICATE_NAME",
                "You already have a saved filter with this name.");
        if (existing is not null)
        {
            // Reuse the row the owner deleted earlier: the unique index covers removed rows too.
            Guard(() => existing.Restore(Clean(name), query ?? "", now));
            await store.SaveAsync(cancellationToken);
            return Map(existing);
        }

        if (await store.CountAsync(tenantId, userId, cancellationToken) >= MaxPerUser)
            throw new ApplicationFault(FaultKind.Validation, "FILTER.TOO_MANY",
                $"You can keep at most {MaxPerUser} saved filters.");

        SavedFilter filter = null!;
        Guard(() => filter = SavedFilter.Create(tenantId, userId, cleanScope, Clean(name), query ?? "", now));
        await store.AddAsync(filter, cancellationToken);
        await store.SaveAsync(cancellationToken);
        return Map(filter);
    }

    public async Task<SavedFilterView> UpdateAsync(Guid savedFilterId, string name, string? query,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireAsync(cancellationToken);
        var filter = await store.FindAsync(tenantId, userId, savedFilterId, cancellationToken);
        if (filter is null || filter.RemovedAt is not null) throw ApplicationFault.NotFound();

        var clash = await store.FindByNameAsync(tenantId, userId, filter.Scope,
            SavedFilter.Normalize(Clean(name)), cancellationToken);
        if (clash is { RemovedAt: null } && clash.Id != filter.Id)
            throw new ApplicationFault(FaultKind.Conflict, "FILTER.DUPLICATE_NAME",
                "You already have a saved filter with this name.");

        Guard(() => filter.Edit(Clean(name), query ?? "", clock.GetUtcNow()));
        await store.SaveAsync(cancellationToken);
        return Map(filter);
    }

    public async Task DeleteAsync(Guid savedFilterId, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = await RequireAsync(cancellationToken);
        var filter = await store.FindAsync(tenantId, userId, savedFilterId, cancellationToken);
        if (filter is null || filter.RemovedAt is not null) throw ApplicationFault.NotFound();
        if (filter.Remove(clock.GetUtcNow())) await store.SaveAsync(cancellationToken);
    }

    private static SavedFilterView Map(SavedFilter f) =>
        new(f.Id, f.Scope, f.Name, f.Query, f.CreatedAt, f.UpdatedAt);

    private async Task<(Guid TenantId, Guid UserId)> RequireAsync(CancellationToken cancellationToken)
    {
        var tenantId = await membership.RequireAsync("notifications.read", cancellationToken);
        if (context.UserId is not { } userId || userId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
        return (tenantId, userId);
    }

    private static string Scope(string? scope)
    {
        try
        {
            return SavedFilter.CleanScope(scope);
        }
        catch (ArgumentException)
        {
            throw new ApplicationFault(FaultKind.Validation, "FILTER.SCOPE",
                "Use TASK or REQUEST as the saved filter scope.");
        }
    }

    private static string Clean(string? name)
    {
        var text = (name ?? "").Trim();
        if (text.Length == 0 || text.Length > SavedFilter.MaxNameLength)
            throw new ApplicationFault(FaultKind.Validation, "FILTER.NAME",
                $"A filter name must contain 1 to {SavedFilter.MaxNameLength} characters.");
        if (text.Any(char.IsControl))
            throw new ApplicationFault(FaultKind.Validation, "FILTER.NAME", "Use plain text for a filter name.");
        return text;
    }

    /// <summary>Turns the domain's argument guards into 422s rather than 500s.</summary>
    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException failure)
        {
            throw new ApplicationFault(FaultKind.Validation, "FILTER.INVALID", failure.Message);
        }
    }
}
