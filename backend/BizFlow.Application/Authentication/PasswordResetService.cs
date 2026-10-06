using BizFlow.Application.Common;
using BizFlow.Domain.Authentication;

namespace BizFlow.Application.Authentication;

public sealed class PasswordResetService(IAuthenticationStore store, IPasswordResetTokenProvider tokens,
    ICredentialVerifier passwords, IAuthenticationAttemptLimiter attempts, IPasswordResetQueue queue,
    PasswordResetPolicy policy, TimeProvider clock)
{
    public void Request(string identifier, string? tenantKey)
    {
        ValidateIdentifier(identifier, tenantKey);
        // Queue every valid-shaped request, including nonexistent/ambiguous identities. The public
        // response and queue-availability behavior must not reveal account existence.
        queue.Enqueue(identifier.Trim().ToUpperInvariant(), NormalizeTenant(tenantKey), clock.GetUtcNow());
    }

    public async Task ResetAsync(string identifier, string? tenantKey, string token, string newPassword, CancellationToken cancellationToken)
    {
        ValidateIdentifier(identifier, tenantKey);
        if (string.IsNullOrEmpty(token) || token.Length > 4096) throw InvalidToken();
        var ticket = await tokens.ValidateAsync(token, cancellationToken);
        if (ticket is null) throw InvalidToken();
        var candidate = new AuthenticationCandidate(ticket.UserId, ticket.TenantId, null);
        attempts.Check(candidate);
        await using var transaction = await store.BeginAsync(candidate, cancellationToken);
        if (transaction is null) throw InvalidToken();
        var user = transaction.User;
        var normalized = identifier.Trim().ToUpperInvariant();
        var access = await transaction.ResolveAccessAsync(cancellationToken);
        if (clock.GetUtcNow() >= ticket.ExpiresAt || !user.CanResetPassword || access?.IsTenantActive != true || user.SecurityStamp != ticket.SecurityStamp ||
            user.NormalizedEmail != ticket.NormalizedEmail ||
            (normalized != user.NormalizedEmployeeCode && normalized != user.NormalizedEmail)) throw InvalidToken();
        // If a caller supplies workspace context, it must resolve to this exact signed identity.
        if (NormalizeTenant(tenantKey) is { } key)
        {
            var matches = await store.FindCandidatesAsync(normalized, key, cancellationToken);
            if (!matches.Any(c => c.UserId == user.Id && c.TenantId == user.TenantId)) throw InvalidToken();
        }
        if (!policy.Accepts(newPassword))
            throw new ApplicationFault(FaultKind.Validation, "AUTH.PASSWORD_POLICY", "The new password does not meet the password policy.",
                new Dictionary<string, string[]> { ["newPassword"] = [$"Use {policy.MinimumPasswordLength}–1024 characters and at least {policy.MinimumDistinctCharacters} distinct characters."] });
        var now = clock.GetUtcNow();
        user.ResetPassword(passwords.Hash(user, newPassword), now);
        await transaction.RevokeAllSessionsAsync(now, cancellationToken);
        transaction.Audit(AuthenticationEvent.PasswordResetSucceeded, now);
        await transaction.CommitAsync(cancellationToken);
    }

    internal static void ValidateIdentifier(string identifier, string? tenantKey)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 320 || tenantKey?.Length > 80)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Please correct the invalid fields.");
    }
    internal static string? NormalizeTenant(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static ApplicationFault InvalidToken() => new(FaultKind.Unauthenticated, "AUTH.INVALID_RESET", "The reset link is invalid or expired. Request a new one.");
}

// The background adapter invokes Application policy; it never decides account eligibility itself.
public sealed class PasswordResetDeliveryService(IAuthenticationStore store, IPasswordResetTokenProvider tokens, IAuthenticationAttemptLimiter attempts,
    IPasswordResetEmailSender email, PasswordResetPolicy policy, TimeProvider clock)
{
    public async Task DeliverAsync(string normalizedIdentifier, string? tenantKey, DateTimeOffset requestedAt, CancellationToken cancellationToken)
    {
        PasswordResetService.ValidateIdentifier(normalizedIdentifier, tenantKey);
        var now = clock.GetUtcNow();
        if (requestedAt > now || now >= requestedAt.AddMinutes(policy.TokenLifetimeMinutes)) return;
        var candidates = await store.FindCandidatesAsync(normalizedIdentifier, tenantKey, cancellationToken);
        if (candidates.Count != 1) return;
        attempts.Check(candidates[0]);
        string token;
        string address;
        await using (var transaction = await store.BeginAsync(candidates[0], cancellationToken))
        {
            if (transaction is null || !transaction.User.CanResetPassword) return;
            var access = await transaction.ResolveAccessAsync(cancellationToken);
            if (access?.IsTenantActive != true || string.IsNullOrWhiteSpace(transaction.User.Email)) return;
            var user = transaction.User;
            // A request made before this identity existed cannot target a subsequently created account.
            if (user.CreatedAt > requestedAt) return;
            token = tokens.Issue(new(user.Id, user.TenantId, user.SecurityStamp, user.NormalizedEmail,
                requestedAt.AddMinutes(policy.TokenLifetimeMinutes)), requestedAt);
            address = user.Email;
            transaction.Audit(AuthenticationEvent.PasswordResetRequested, now);
            await transaction.CommitAsync(cancellationToken);
        }
        // Never hold a row lock across SMTP. Token stamp/email binding makes a later account change safe.
        await email.SendAsync(address, token, cancellationToken);
    }
}
