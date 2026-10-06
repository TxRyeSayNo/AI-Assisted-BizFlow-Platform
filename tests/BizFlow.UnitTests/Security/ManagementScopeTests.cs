using BizFlow.Domain.Organization;

namespace BizFlow.UnitTests.Security;

public sealed class ManagementScopeTests
{
    [Fact]
    public void Scope_preserves_configuration_and_requires_nonempty_ownership_ids()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var now = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(7));
        var scope = ManagementScope.Create(ids[0], ids[1], ids[2], true, ids[3], now);
        Assert.NotEqual(Guid.Empty, scope.Id);
        Assert.Equal(ids[0], scope.TenantId); Assert.Equal(ids[1], scope.UserId);
        Assert.Equal(ids[2], scope.DepartmentId); Assert.Equal(ids[3], scope.CreatedBy);
        Assert.True(scope.IncludeDescendants); Assert.Equal(TimeSpan.Zero, scope.CreatedAt.Offset);
        Assert.Equal(now, scope.CreatedAt);
        for (var index = 0; index < ids.Length; index++)
        {
            var invalid = (Guid[])ids.Clone(); invalid[index] = Guid.Empty;
            Assert.Throws<ArgumentException>(() => ManagementScope.Create(invalid[0], invalid[1], invalid[2], false, invalid[3], now));
        }
    }

    [Fact]
    public void Expansion_unions_roots_and_flagged_descendants_without_ancestors_or_siblings()
    {
        var ids = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
        DepartmentHierarchyNode[] hierarchy = [new(ids[0], null), new(ids[1], ids[0]),
            new(ids[2], ids[1]), new(ids[3], ids[2]), new(ids[4], ids[0]),
            new(ids[5], null), new(ids[6], ids[5])];
        var result = ManagementScopeExpansion.Expand(hierarchy, [new(ids[1], true), new(ids[2], false), new(ids[5], false)]);
        Assert.True(result.SetEquals([ids[1], ids[2], ids[3], ids[5]]));
        Assert.Empty(ManagementScopeExpansion.Expand(hierarchy, []));
        Assert.Empty(ManagementScopeExpansion.Expand(hierarchy, [new(Guid.NewGuid(), true)]));
    }

    [Fact]
    public void Overlapping_roots_cycles_and_deep_trees_terminate_without_losing_descendants()
    {
        var ids = Enumerable.Range(0, 20000).Select(_ => Guid.NewGuid()).ToArray();
        var hierarchy = ids.Select((id, index) => new DepartmentHierarchyNode(id, ids[(index + 1) % ids.Length])).ToArray();
        var result = ManagementScopeExpansion.Expand(hierarchy, [new(ids[0], false), new(ids[1], true), new(ids[2], true)]);
        Assert.Equal(ids.Length, result.Count);
    }
}
