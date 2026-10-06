using BizFlow.Domain.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Authentication;

public sealed record AuthenticationCandidate(Guid UserId, Guid? TenantId, string? TenantName);
public sealed record SessionIdentity(Guid UserId, string FullName, Guid? TenantId, string? TenantName, string[] Permissions);
public sealed record AuthenticationResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, SessionIdentity Session)
{
    public override string ToString() => "[REDACTED AUTHENTICATION RESPONSE]";
}
public sealed record AccessTokenRequest(Guid UserId, Guid? TenantId, Guid FamilyId, string SecurityStamp);
public sealed record IssuedAccessToken(string Secret, DateTimeOffset ExpiresAt)
{
    public override string ToString() => "[REDACTED ACCESS TOKEN]";
}
public sealed record PasswordCheck(bool Valid, string? UpgradedHash = null);

public interface ICredentialVerifier
{
    PasswordCheck Verify(UserAccount user, string password);
    void VerifyDummy(string password);
    string Hash(UserAccount user, string password);
}
public interface IAuthenticationAttemptLimiter
{
    void Check(AuthenticationCandidate candidate);
}
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccessTokenRequest request, DateTimeOffset now);
}
public interface IAuthenticationStore
{
    Task<IReadOnlyList<AuthenticationCandidate>> FindCandidatesAsync(string normalizedIdentifier, string? tenantKey, CancellationToken cancellationToken);
    Task<AuthenticationCandidate?> FindSessionOwnerAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IAuthenticationTransaction?> BeginAsync(AuthenticationCandidate candidate, CancellationToken cancellationToken);
    Task<bool> ValidateAccessAsync(AccessTokenRequest identity, DateTimeOffset now, CancellationToken cancellationToken);
}

// Implementations serialize operations on the same user and commit account, session and audit together.
// No DbContext or IQueryable crosses this boundary.
public interface IAuthenticationTransaction : IAsyncDisposable
{
    UserAccount User { get; }
    string? TenantName { get; }
    Task<AccessSnapshot?> ResolveAccessAsync(CancellationToken cancellationToken);
    Task<AuthenticationSession?> FindSessionAsync(string hash, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeAllSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    void AddSession(AuthenticationSession session);
    void Audit(AuthenticationEvent action, DateTimeOffset now, Guid? familyId = null);
    Task CommitAsync(CancellationToken cancellationToken);
}

// Deployment policy, not additional business states. Configured centrally and validated at startup.
public sealed class AuthenticationPolicy
{
    public int FailedAttemptLimit { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
    public int RefreshLifetimeDays { get; init; } = 14;
    public void Validate()
    {
        if (FailedAttemptLimit is < 1 or > 100 || LockoutMinutes is < 1 or > 1440 || RefreshLifetimeDays is < 1 or > 90)
            throw new InvalidOperationException("Authentication policy values are outside supported bounds.");
    }
}
