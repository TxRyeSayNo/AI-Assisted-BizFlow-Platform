using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

public sealed class CompanyRegistrationQueryTests
{
    [Fact]
    public async Task Authorization_precedes_validation_and_storage()
    {
        var store = new Store();
        var service = new CompanyRegistrationQuery(new Authorizer(false), store);
        await Assert.ThrowsAsync<ApplicationFault>(() => service.ListAsync(new(0, 200, "unknown", null), default));
        Assert.False(store.Called);
    }

    [Theory]
    [InlineData(0, 25, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 25, "APPROVED")]
    [InlineData(int.MaxValue, 100, null)]
    public async Task Invalid_page_or_noncanonical_status_never_reaches_storage(int page, int size, string? status)
    {
        var store = new Store();
        var service = new CompanyRegistrationQuery(new Authorizer(true), store);
        var error = await Assert.ThrowsAsync<ApplicationFault>(() => service.ListAsync(new(page, size, status, null), default));
        Assert.Equal("VALIDATION.FAILED", error.Code);
        Assert.False(store.Called);
    }

    [Fact]
    public async Task Valid_query_is_normalized_after_exact_platform_permission_check()
    {
        var store = new Store();
        var service = new CompanyRegistrationQuery(new Authorizer(true), store);
        await service.ListAsync(new(1, 25, "PENDING", "  Example  "), default);
        Assert.True(store.Called);
        Assert.Equal("Example", store.Query!.Search);
    }

    private sealed class Authorizer(bool allow) : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(PlatformPermissions.ReadCompanyRegistrations, permission);
            Assert.Null(resource.TenantId);
            if (!allow) throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Denied.");
            return Task.CompletedTask;
        }
    }
    private sealed class Store : ICompanyRegistrationReader
    {
        public bool Called { get; private set; }
        public CompanyRegistrationFilter? Query { get; private set; }
        public Task<CompanyRegistrationPage> ListAsync(CompanyRegistrationFilter query, CancellationToken cancellationToken)
        {
            Called = true; Query = query;
            return Task.FromResult(new CompanyRegistrationPage([], query.Page, query.PageSize, 0));
        }
    }
}
