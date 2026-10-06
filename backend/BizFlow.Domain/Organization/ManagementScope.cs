using BizFlow.Domain.Common;

namespace BizFlow.Domain.Organization;

// Approved architecture A-02 / BR-003. Configuration is not a permission grant.
public sealed class ManagementScope
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public bool IncludeDescendants { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private ManagementScope() { }

    public static ManagementScope Create(Guid tenantId, Guid userId, Guid departmentId,
        bool includeDescendants, Guid createdBy, DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            DepartmentId = EntityRules.Id(departmentId, nameof(departmentId)),
            IncludeDescendants = includeDescendants,
            CreatedBy = EntityRules.Id(createdBy, nameof(createdBy)), CreatedAt = now.ToUniversalTime()
        };
}

public sealed record DepartmentHierarchyNode(Guid DepartmentId, Guid? ParentDepartmentId);
public sealed record ManagementScopeRoot(Guid DepartmentId, bool IncludeDescendants);

public static class ManagementScopeExpansion
{
    // Inputs must be loaded from one verified tenant. Unknown roots fail closed.
    // An iterative traversal and visited set also terminate on malformed legacy cycles.
    public static IReadOnlySet<Guid> Expand(IReadOnlyCollection<DepartmentHierarchyNode> hierarchy,
        IReadOnlyCollection<ManagementScopeRoot> roots)
    {
        var known = hierarchy.Select(x => x.DepartmentId).ToHashSet();
        var children = hierarchy.Where(x => x.ParentDepartmentId.HasValue)
            .ToLookup(x => x.ParentDepartmentId!.Value, x => x.DepartmentId);
        var result = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        foreach (var root in roots.Where(x => known.Contains(x.DepartmentId)))
        {
            result.Add(root.DepartmentId);
            if (root.IncludeDescendants) pending.Push(root.DepartmentId);
        }
        while (pending.TryPop(out var department))
        {
            if (!visited.Add(department)) continue;
            result.Add(department);
            foreach (var child in children[department]) pending.Push(child);
        }
        return result;
    }
}
