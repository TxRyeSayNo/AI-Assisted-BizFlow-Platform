using System.Security.Cryptography;
using BizFlow.Application.Authentication;
using BizFlow.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BizFlow.IntegrationTests.Authentication;

public sealed class PasswordResetTokenTests
{
    [Fact]
    public async Task Reset_tokens_have_an_independent_purpose_and_exact_expiry()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var settings = new JwtSettings { Issuer = "tests", Audience = "tests", SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        var policy = new PasswordResetPolicy();
        var provider = new JwtPasswordResetTokenProvider(settings, policy, clock);
        var ticket = new PasswordResetTicket(Guid.NewGuid(), Guid.NewGuid(), "stamp", "USER@EXAMPLE.TEST", clock.Now.AddMinutes(15));
        var reset = provider.Issue(ticket, clock.Now);
        Assert.NotNull(await provider.ValidateAsync(reset, default));
        Assert.False((await new JsonWebTokenHandler().ValidateTokenAsync(reset, settings.ValidationParameters())).IsValid);
        var access = new JwtAccessTokenIssuer(settings).Issue(new(ticket.UserId, ticket.TenantId, Guid.NewGuid(), ticket.SecurityStamp), clock.Now);
        Assert.Null(await provider.ValidateAsync(access.Secret, default));
        Assert.Null(await provider.ValidateAsync("invalid-token", default));
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Null(await provider.ValidateAsync(reset, default));
    }
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
