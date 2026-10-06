using BizFlow.Api.Security;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;

namespace BizFlow.IntegrationTests.Authentication;

public sealed class AuthenticationLimitTests
{
    [Fact]
    public void User_and_tenant_quotas_are_independent_and_do_not_block_another_tenant()
    {
        using var limiter = new AuthenticationAttemptLimiter(new AuthenticationRateLimits
        { PerUserPerMinute = 1, PerTenantPerMinute = 2 });
        var tenant = Guid.NewGuid();
        var first = new AuthenticationCandidate(Guid.NewGuid(), tenant, "Company A");
        limiter.Check(first);
        Assert.Equal(FaultKind.TooManyRequests, Assert.Throws<ApplicationFault>(() => limiter.Check(first)).Kind);
        limiter.Check(new(Guid.NewGuid(), tenant, "Company A"));
        Assert.Equal(FaultKind.TooManyRequests, Assert.Throws<ApplicationFault>(() =>
            limiter.Check(new(Guid.NewGuid(), tenant, "Company A"))).Kind);
        limiter.Check(new(Guid.NewGuid(), Guid.NewGuid(), "Company B"));
    }
}
