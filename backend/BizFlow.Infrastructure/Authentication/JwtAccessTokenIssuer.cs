using BizFlow.Application.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BizFlow.Infrastructure.Authentication;

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKeyBase64 { get; init; } = "";
    public int AccessLifetimeMinutes { get; init; } = 15;
    public SymmetricSecurityKey SigningKey()
    {
        byte[] key;
        try { key = Convert.FromBase64String(SigningKeyBase64); }
        catch (FormatException) { throw new InvalidOperationException("JWT signing key must be base64 encoded."); }
        if (key.Length < 32 || string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience) || AccessLifetimeMinutes is < 1 or > 60)
            throw new InvalidOperationException("JWT issuer, audience and a signing key of at least 256 bits are required; access lifetime must be 1–60 minutes.");
        return new SymmetricSecurityKey(key);
    }
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true, ValidAudience = Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = SigningKey(), RequireSignedTokens = true,
        ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(15),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidTypes = ["at+jwt"], NameClaimType = "sub"
    };
}

public sealed class JwtAccessTokenIssuer(JwtSettings settings) : IAccessTokenIssuer
{
    private readonly SigningCredentials credentials = new(settings.SigningKey(), SecurityAlgorithms.HmacSha256);
    public IssuedAccessToken Issue(AccessTokenRequest request, DateTimeOffset now)
    {
        var expiry = now.AddMinutes(settings.AccessLifetimeMinutes);
        var claims = new Dictionary<string, object>
        {
            ["sub"] = request.UserId.ToString(), ["sid"] = request.FamilyId.ToString(),
            ["security_stamp"] = request.SecurityStamp, ["jti"] = Guid.NewGuid().ToString("N")
        };
        if (request.TenantId is { } tenantId) claims["tenant_id"] = tenantId.ToString();
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer, Audience = settings.Audience, Claims = claims,
            IssuedAt = now.UtcDateTime, NotBefore = now.UtcDateTime, Expires = expiry.UtcDateTime,
            SigningCredentials = credentials, TokenType = "at+jwt"
        });
        return new(token, expiry);
    }
}
