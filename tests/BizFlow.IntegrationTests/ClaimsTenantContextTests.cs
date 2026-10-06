using System.Security.Claims;
using BizFlow.Api.Security;
using Microsoft.AspNetCore.Http;

namespace BizFlow.IntegrationTests;

public sealed class ClaimsTenantContextTests
{
    [Fact]
    public void Reads_identity_only_from_authenticated_principal_not_request_values()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", userId.ToString()), new("tenant_id", tenantId.ToString())], "Test"))
        };
        http.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
        http.Request.QueryString = new QueryString("?tenantId=" + Guid.NewGuid());
        var context = new ClaimsTenantContext(new HttpContextAccessor { HttpContext = http });
        Assert.Equal(userId, context.UserId);
        Assert.Equal(tenantId, context.TenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Unauthenticated_claims_cannot_establish_context(string? authenticationType)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", Guid.NewGuid().ToString()), new("tenant_id", Guid.NewGuid().ToString())], authenticationType))
        };
        var context = new ClaimsTenantContext(new HttpContextAccessor { HttpContext = http });
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
    }

    [Fact]
    public void Duplicate_or_empty_identity_claims_fail_closed()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new("sub", Guid.Empty.ToString()),
                new("tenant_id", Guid.NewGuid().ToString()),
                new("tenant_id", Guid.NewGuid().ToString())], "Test"))
        };
        var context = new ClaimsTenantContext(new HttpContextAccessor { HttpContext = http });
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
    }
}
