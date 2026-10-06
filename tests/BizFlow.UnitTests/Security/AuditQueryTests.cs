using BizFlow.Application.Audit;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

public sealed class AuditQueryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Exact_plane_permission_precedes_filter_validation_and_storage(bool tenantPlane)
    {
        var tenantId = tenantPlane ? Guid.NewGuid() : (Guid?)null;
        var reader = new Reader();
        var service = new AuditQuery(new Context(tenantId), new Authorizer(tenantId, false), reader);
        await Assert.ThrowsAsync<ApplicationFault>(() => service.ListAsync(new(From: "invalid"), default));
        Assert.Null(reader.Criteria);
    }

    [Theory]
    [InlineData("2026-10-01")]
    [InlineData("2026-10-01T00:00:00")]
    [InlineData("2026-02-30T00:00:00Z")]
    [InlineData("2026-10-01T00:00:00+25:00")]
    public async Task Invalid_or_implicit_time_zones_do_not_reach_storage(string timestamp)
    {
        var tenantId = Guid.NewGuid(); var reader = new Reader();
        var service = new AuditQuery(new Context(tenantId), new Authorizer(tenantId, true), reader);
        Assert.Equal("VALIDATION.FAILED", (await Assert.ThrowsAsync<ApplicationFault>(() => service.ListAsync(new(From: timestamp), default))).Code);
        Assert.Null(reader.Criteria);
    }

    [Fact]
    public async Task Valid_filter_preserves_exact_codes_and_converts_explicit_offsets_to_UTC()
    {
        var tenantId = Guid.NewGuid(); var reader = new Reader();
        var service = new AuditQuery(new Context(tenantId), new Authorizer(tenantId, true), reader);
        await service.ListAsync(new(Action: " ROLE.CREATED ", ActorType: " USER ", From: "2026-10-01T07:00:00+07:00", Until: "2026-10-02T00:00:00Z"), default);
        Assert.Equal("ROLE.CREATED", reader.Criteria!.Action); Assert.Equal("USER", reader.Criteria.ActorType);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), reader.Criteria.From); Assert.Equal(tenantId, reader.TenantId);
    }

    private sealed record Context(Guid? TenantId) : ITenantContext { public Guid? UserId => Guid.Parse("019f7a64-0000-7000-8000-000000000010"); }
    private sealed class Authorizer(Guid? tenant, bool allowed) : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(tenant, resource.TenantId); Assert.Equal(tenant is null ? PlatformPermissions.ReadAudit : AuditQuery.TenantReadPermission, permission);
            if (!allowed) throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Denied");
            return Task.CompletedTask;
        }
    }
    private sealed class Reader : IAuditReader
    {
        public AuditCriteria? Criteria { get; private set; }
        public Guid? TenantId { get; private set; }
        public Task<AuditPage> ListAsync(Guid? tenantId, AuditCriteria criteria, CancellationToken cancellationToken)
        { TenantId = tenantId; Criteria = criteria; return Task.FromResult(new AuditPage([], criteria.Page, criteria.PageSize, 0)); }
    }
}
