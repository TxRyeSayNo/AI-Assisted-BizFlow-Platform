using BizFlow.Application.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BizFlow.Infrastructure.Authentication;

public sealed class JwtPasswordResetTokenProvider(JwtSettings settings, PasswordResetPolicy policy, TimeProvider clock) : IPasswordResetTokenProvider
{
    public string Issue(PasswordResetTicket ticket, DateTimeOffset requestedAt)
    {
        if (ticket.ExpiresAt <= requestedAt || ticket.ExpiresAt > requestedAt.AddMinutes(policy.TokenLifetimeMinutes))
            throw new ArgumentException("Reset ticket expiry is outside policy.", nameof(ticket));
        var claims = new Dictionary<string, object>
        {
            ["sub"] = ticket.UserId.ToString(), ["security_stamp"] = ticket.SecurityStamp,
            ["reset_email"] = ticket.NormalizedEmail
        };
        if (ticket.TenantId is { } tenant) claims["tenant_id"] = tenant.ToString();
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer, Audience = settings.Audience + ":password-reset", Claims = claims,
            IssuedAt = requestedAt.UtcDateTime, NotBefore = requestedAt.UtcDateTime,
            Expires = ticket.ExpiresAt.UtcDateTime,
            SigningCredentials = new(settings.SigningKey(), SecurityAlgorithms.HmacSha256), TokenType = "password-reset+jwt"
        });
    }

    public async Task<PasswordResetTicket?> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = settings.ValidationParameters();
        parameters.ValidAudience = settings.Audience + ":password-reset";
        parameters.ValidTypes = ["password-reset+jwt"];
        parameters.ClockSkew = TimeSpan.Zero;
        parameters.LifetimeValidator = (notBefore, expires, _, _) => notBefore.HasValue && expires.HasValue &&
            notBefore.Value <= clock.GetUtcNow().UtcDateTime && expires.Value > clock.GetUtcNow().UtcDateTime;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        if (!result.IsValid) return null;
        string? Claim(string name) => result.ClaimsIdentity.FindFirst(name)?.Value;
        if (!Guid.TryParse(Claim("sub"), out var user) || user == Guid.Empty ||
            Claim("security_stamp") is not { Length: > 0 } stamp || Claim("reset_email") is not { Length: > 0 } email) return null;
        Guid? tenant = null;
        if (Claim("tenant_id") is { } value)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty) return null;
            tenant = id;
        }
        return new(user, tenant, stamp, email, new DateTimeOffset(result.SecurityToken.ValidTo, TimeSpan.Zero));
    }
}
