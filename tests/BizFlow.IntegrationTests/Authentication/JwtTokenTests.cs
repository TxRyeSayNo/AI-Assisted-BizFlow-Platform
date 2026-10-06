using System.Security.Cryptography;
using BizFlow.Application.Authentication;
using BizFlow.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BizFlow.IntegrationTests.Authentication;

public sealed class JwtTokenTests
{
    [Fact]
    public async Task Signature_issuer_audience_type_and_expiry_are_required()
    {
        var settings = new JwtSettings { Issuer = "tests", Audience = "tests", SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        var identity = new AccessTokenRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "stamp");
        var issuer = new JwtAccessTokenIssuer(settings);
        var token = issuer.Issue(identity, DateTimeOffset.UtcNow);
        var handler = new JsonWebTokenHandler();
        Assert.True((await handler.ValidateTokenAsync(token.Secret, settings.ValidationParameters())).IsValid);
        var wrongIssuer = settings.ValidationParameters(); wrongIssuer.ValidIssuer = "other";
        Assert.False((await handler.ValidateTokenAsync(token.Secret, wrongIssuer)).IsValid);
        var wrongAudience = settings.ValidationParameters(); wrongAudience.ValidAudience = "other";
        Assert.False((await handler.ValidateTokenAsync(token.Secret, wrongAudience)).IsValid);
        var wrongKey = settings.ValidationParameters(); wrongKey.IssuerSigningKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        Assert.False((await handler.ValidateTokenAsync(token.Secret, wrongKey)).IsValid);
        var wrongType = settings.ValidationParameters(); wrongType.ValidTypes = ["id+jwt"];
        Assert.False((await handler.ValidateTokenAsync(token.Secret, wrongType)).IsValid);
        var expired = issuer.Issue(identity, DateTimeOffset.UtcNow.AddHours(-1));
        Assert.False((await handler.ValidateTokenAsync(expired.Secret, settings.ValidationParameters())).IsValid);
    }

    [Fact]
    public void Missing_or_short_signing_key_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtSettings().SigningKey());
        Assert.Throws<InvalidOperationException>(() => new JwtSettings
        { Issuer = "tests", Audience = "tests", SigningKeyBase64 = Convert.ToBase64String(new byte[16]) }.SigningKey());
    }
}
