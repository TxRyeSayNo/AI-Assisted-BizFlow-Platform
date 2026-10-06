using System.Security.Claims;
using System.Threading.RateLimiting;
using BizFlow.Application.Authentication;
using BizFlow.Application.Security;
using BizFlow.Infrastructure.Audit;
using BizFlow.Infrastructure.Authentication;
using BizFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Security;

public static class AuthenticationRegistration
{
    public static IServiceCollection AddBizFlowAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>().BindConfiguration("Jwt").Validate(s =>
        {
            try { _ = s.SigningKey(); return true; } catch (InvalidOperationException) { return false; }
        }, "Valid JWT issuer, audience and signing key configuration is required.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);
        services.AddOptions<AuthenticationPolicy>().BindConfiguration("Authentication").Validate(p =>
        {
            try { p.Validate(); return true; } catch (InvalidOperationException) { return false; }
        }, "Invalid authentication policy.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AuthenticationPolicy>>().Value);
        services.AddOptions<AuthenticationRateLimits>().BindConfiguration("Authentication:RateLimits")
            .Validate(p => p.IsValid, "Invalid authentication rate limits.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AuthenticationRateLimits>>().Value);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(
            configuration.GetConnectionString("BizFlow") ?? throw new InvalidOperationException("ConnectionStrings:BizFlow is required.")).Options);
        services.AddScoped<BizFlowDbContext>();
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddSingleton<ICredentialVerifier, IdentityCredentialVerifier>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<IAuthenticationAttemptLimiter, AuthenticationAttemptLimiter>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IAccessSnapshotProvider, AccessSnapshotProvider>();
        services.AddScoped<ISecurityAuditWriter, SecurityAuditWriter>();
        services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings>((options, settings) =>
        {
            options.MapInboundClaims = false; options.IncludeErrorDetails = false;
            options.TokenValidationParameters = settings.ValidationParameters();
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var identity = ReadIdentity(context.Principal);
                    if (identity is null || !await context.HttpContext.RequestServices.GetRequiredService<IAuthenticationStore>()
                        .ValidateAccessAsync(identity, context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), context.HttpContext.RequestAborted))
                        context.Fail("The session is no longer valid.");
                }
            };
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context =>
                RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => AuthenticationAttemptLimiter.Window(context.RequestServices.GetRequiredService<AuthenticationRateLimits>().PerIpPerMinute)));
        });
        return services;
    }

    private static AccessTokenRequest? ReadIdentity(ClaimsPrincipal? principal)
    {
        string? Single(string name)
        {
            var claims = principal?.FindAll(name).Select(x => x.Value).ToArray();
            return claims?.Length == 1 ? claims[0] : null;
        }
        if (!Guid.TryParse(Single("sub"), out var user) || user == Guid.Empty ||
            !Guid.TryParse(Single("sid"), out var family) || family == Guid.Empty || Single("security_stamp") is not { Length: > 0 } stamp)
            return null;
        Guid? tenant = null;
        if (principal!.HasClaim(x => x.Type == "tenant_id"))
        {
            if (!Guid.TryParse(Single("tenant_id"), out var id) || id == Guid.Empty) return null;
            tenant = id;
        }
        return new(user, tenant, family, stamp);
    }
}
