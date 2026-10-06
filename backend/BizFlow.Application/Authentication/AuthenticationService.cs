using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Authentication;

public sealed class AuthenticationService(IAuthenticationStore store, ICredentialVerifier passwords,
    IRefreshTokenGenerator refreshTokens, IAccessTokenIssuer accessTokens, IAuthenticationAttemptLimiter attempts,
    AuthenticationPolicy policy, TimeProvider clock)
{
    public async Task<AuthenticationResponse> LoginAsync(string identifier, string password, string? tenantKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 320 || string.IsNullOrEmpty(password) || password.Length > 1024 || tenantKey?.Length > 80)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Please correct the invalid fields.");
        var candidates = await store.FindCandidatesAsync(identifier.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim().ToLowerInvariant(), cancellationToken);
        if (candidates.Count != 1)
        {
            passwords.VerifyDummy(password);
            if (candidates.Count > 1 && string.IsNullOrWhiteSpace(tenantKey))
                throw new ApplicationFault(FaultKind.Conflict, "AUTH.TENANT_CONTEXT_REQUIRED", "Enter your company's workspace key to continue.",
                    new Dictionary<string, string[]> { ["companyNames"] = candidates.Select(x => x.TenantName).OfType<string>().Distinct().ToArray() });
            throw InvalidCredentials();
        }
        attempts.Check(candidates[0]);
        await using var transaction = await store.BeginAsync(candidates[0], cancellationToken);
        if (transaction is null) { passwords.VerifyDummy(password); throw InvalidCredentials(); }
        var now = clock.GetUtcNow();
        var check = passwords.Verify(transaction.User, password);
        var access = await transaction.ResolveAccessAsync(cancellationToken);
        if (!check.Valid || !Allowed(transaction, access, now))
        {
            if (!check.Valid) transaction.User.RecordFailedLogin(now, policy.FailedAttemptLimit, TimeSpan.FromMinutes(policy.LockoutMinutes));
            transaction.Audit(AuthenticationEvent.LoginFailed, now);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidCredentials();
        }
        transaction.User.RecordSuccessfulLogin(now, check.UpgradedHash);
        var refresh = refreshTokens.Generate();
        var session = AuthenticationSession.Create(transaction.User.Id, refresh.Hash, now, now.AddDays(policy.RefreshLifetimeDays));
        transaction.AddSession(session);
        transaction.Audit(AuthenticationEvent.LoginSucceeded, now, session.FamilyId);
        var response = Response(transaction, access!, refresh.Secret, session.FamilyId, now);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<AuthenticationResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        string hash;
        if (string.IsNullOrEmpty(refreshToken)) throw InvalidRefresh();
        try { hash = refreshTokens.Hash(refreshToken); }
        catch (ArgumentException) { throw InvalidRefresh(); }
        var owner = await store.FindSessionOwnerAsync(hash, cancellationToken);
        if (owner is null) throw InvalidRefresh();
        attempts.Check(owner);
        await using var transaction = await store.BeginAsync(owner, cancellationToken);
        if (transaction is null) throw InvalidRefresh();
        var session = await transaction.FindSessionAsync(hash, cancellationToken);
        if (session is null) throw InvalidRefresh();
        var now = clock.GetUtcNow();
        // Under the per-user lock, replay cannot race past another rotation or revocation.
        if (session.RevokedAt is not null)
        {
            await transaction.RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            transaction.Audit(AuthenticationEvent.ReplayDetected, now, session.FamilyId);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidRefresh();
        }
        var access = await transaction.ResolveAccessAsync(cancellationToken);
        if (!session.CanRefresh(now) || !Allowed(transaction, access, now))
        {
            await transaction.RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            transaction.Audit(AuthenticationEvent.RefreshDenied, now, session.FamilyId);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidRefresh();
        }
        var refresh = refreshTokens.Generate();
        transaction.AddSession(session.Rotate(refresh.Hash, now));
        transaction.Audit(AuthenticationEvent.RefreshSucceeded, now, session.FamilyId);
        var response = Response(transaction, access!, refresh.Secret, session.FamilyId, now);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static bool Allowed(IAuthenticationTransaction transaction, AccessSnapshot? access, DateTimeOffset now) =>
        transaction.User.CanAuthenticate(now) && access is { IsUserActive: true, IsTenantActive: true } &&
        access.UserId == transaction.User.Id && access.TenantId == transaction.User.TenantId;

    private AuthenticationResponse Response(IAuthenticationTransaction transaction, AccessSnapshot access,
        string refreshToken, Guid familyId, DateTimeOffset now)
    {
        var user = transaction.User;
        var token = accessTokens.Issue(new(user.Id, user.TenantId, familyId, user.SecurityStamp), now);
        return new(token.Secret, refreshToken, token.ExpiresAt,
            new(user.Id, user.FullName, user.TenantId, transaction.TenantName, access.Grants.Select(x => x.Code).Distinct().Order().ToArray()));
    }
    private static ApplicationFault InvalidCredentials() => new(FaultKind.Unauthenticated, "AUTH.INVALID_CREDENTIALS", "The credentials or workspace are invalid.");
    private static ApplicationFault InvalidRefresh() => new(FaultKind.Unauthenticated, "AUTH.INVALID_REFRESH", "Sign in again to continue.");
}
