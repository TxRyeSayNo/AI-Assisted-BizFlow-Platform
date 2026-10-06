using System.Threading.RateLimiting;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;

namespace BizFlow.Api.Security;

public sealed class AuthenticationRateLimits
{
    public int PerIpPerMinute { get; init; } = 30;
    public int PerUserPerMinute { get; init; } = 30;
    public int PerTenantPerMinute { get; init; } = 300;
    public bool IsValid => PerIpPerMinute is >= 1 and <= 10000 && PerUserPerMinute is >= 1 and <= 10000 && PerTenantPerMinute is >= 1 and <= 100000;
}

public sealed class AuthenticationAttemptLimiter : IAuthenticationAttemptLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<AuthenticationCandidate> limiter;
    public AuthenticationAttemptLimiter(AuthenticationRateLimits limits)
    {
        limiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<AuthenticationCandidate, Guid>(c => RateLimitPartition.GetFixedWindowLimiter(c.UserId,
                _ => Window(limits.PerUserPerMinute))),
            PartitionedRateLimiter.Create<AuthenticationCandidate, string>(c => RateLimitPartition.GetFixedWindowLimiter(
                c.TenantId?.ToString() ?? "platform", _ => Window(limits.PerTenantPerMinute))));
    }
    public void Check(AuthenticationCandidate candidate)
    {
        using var lease = limiter.AttemptAcquire(candidate);
        if (!lease.IsAcquired) throw new ApplicationFault(FaultKind.TooManyRequests, "RATE_LIMIT.EXCEEDED", "Too many requests. Please try again later.");
    }
    internal static FixedWindowRateLimiterOptions Window(int limit) => new()
    { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true };
    public void Dispose() => limiter.Dispose();
}
