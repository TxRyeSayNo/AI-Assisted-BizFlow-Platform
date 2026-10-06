using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;

namespace BizFlow.UnitTests.Security;

public sealed class IdentityEntityTests
{
    [Fact]
    public void Registration_starts_pending_and_normalizes_company_code()
    {
        var company = Company.Register(" demo ", "Company", "admin@example.test", DateTimeOffset.UtcNow);
        Assert.Equal(CompanyStatus.Pending, company.Status);
        Assert.Equal("DEMO", company.Code);
    }

    [Fact]
    public void Tenant_user_requires_tenant_and_platform_account_has_no_tenant_or_department()
    {
        Assert.Throws<ArgumentException>(() => UserAccount.CreateTenantUser(Guid.Empty, "EMP1", "e@example.test", "Employee", "hash", DateTimeOffset.UtcNow));
        var platform = UserAccount.CreatePlatformAdministrator("PLATFORM", "admin@example.test", "Administrator", "hash", DateTimeOffset.UtcNow);
        Assert.True(platform.IsPlatformAdministrator);
        Assert.Null(platform.TenantId);
        Assert.Null(platform.DepartmentId);
        var tenant = UserAccount.CreateTenantUser(Guid.NewGuid(), " emp1 ", "Employee@example.test", "Employee", "hash", DateTimeOffset.UtcNow);
        Assert.False(tenant.IsPlatformAdministrator);
        Assert.Equal("EMP1", tenant.NormalizedEmployeeCode);
        Assert.Equal("EMPLOYEE@EXAMPLE.TEST", tenant.NormalizedEmail);
    }

    [Theory]
    [InlineData("Bad Workspace", "UTC")]
    [InlineData("valid", "Not/A_Timezone")]
    public void Tenant_rejects_invalid_slug_or_non_IANA_timezone(string key, string zone)
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), key, "Company", zone, DateTimeOffset.UtcNow));
    }
}
