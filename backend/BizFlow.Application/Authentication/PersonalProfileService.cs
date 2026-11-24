using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Authentication;

namespace BizFlow.Application.Authentication;

public sealed record PersonalProfile(Guid UserId, string FullName, string EmployeeCode, string Email,
    string? DepartmentName, string? TenantName, string? TenantKey, string[] Roles, bool HasAvatar,
    DateTimeOffset CreatedAt, int MinimumPasswordLength = 12, string? Nickname = null);

public interface IPersonalProfileReader
{
    Task<PersonalProfile?> ReadAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken);
}

public sealed class PersonalProfileService(ITenantContext context, IAuthenticationStore store,
    IPersonalProfileReader reader, ICredentialVerifier passwords, IAuthenticationAttemptLimiter attempts,
    AuthenticationPolicy authenticationPolicy, PasswordResetPolicy policy, TimeProvider clock)
{
    private AuthenticationCandidate Candidate() => context.UserId is { } userId
        ? new(userId, context.TenantId, null) : throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Sign in first.");

    public async Task<PersonalProfile> ReadAsync(CancellationToken cancellationToken)
    {
        var candidate = Candidate();
        return (await reader.ReadAsync(candidate.UserId, candidate.TenantId, cancellationToken)
            ?? throw ApplicationFault.NotFound()) with { MinimumPasswordLength = policy.MinimumPasswordLength };
    }

    public async Task UpdateNicknameAsync(string nickname, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nickname) || nickname.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "PROFILE.INVALID_NICKNAME", "Nhập biệt danh từ 1 đến 200 ký tự.");
        var candidate = Candidate();
        await using var tx = await store.BeginAsync(candidate, cancellationToken) ?? throw ApplicationFault.NotFound();
        await EnsureActiveAsync(tx, cancellationToken);
        tx.User.SetNickname(nickname.Trim(), clock.GetUtcNow());
        tx.Audit(AuthenticationEvent.ProfileUpdated, clock.GetUtcNow());
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<string?> GetAvatarKeyAsync(CancellationToken cancellationToken)
    {
        await using var tx = await store.BeginAsync(Candidate(), cancellationToken) ?? throw ApplicationFault.NotFound();
        await EnsureActiveAsync(tx, cancellationToken);
        return tx.User.AvatarObjectKey;
    }

    public async Task<string?> SetAvatarKeyAsync(string? key, CancellationToken cancellationToken)
    {
        var candidate = Candidate();
        // Keys are server generated; never allow references to another account's object.
        if (key is not null && !key.StartsWith($"avatars/{candidate.UserId:D}/", StringComparison.Ordinal))
            throw new ApplicationFault(FaultKind.Validation, "PROFILE.INVALID_AVATAR", "Invalid avatar key.");
        await using var tx = await store.BeginAsync(candidate, cancellationToken) ?? throw ApplicationFault.NotFound();
        await EnsureActiveAsync(tx, cancellationToken);
        var old = tx.User.AvatarObjectKey;
        tx.User.SetAvatar(key, clock.GetUtcNow());
        tx.Audit(AuthenticationEvent.ProfileUpdated, clock.GetUtcNow());
        await tx.CommitAsync(cancellationToken);
        return old;
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var candidate = Candidate();
        attempts.Check(candidate);
        await using var tx = await store.BeginAsync(candidate, cancellationToken) ?? throw ApplicationFault.NotFound();
        await EnsureActiveAsync(tx, cancellationToken);
        var now = clock.GetUtcNow();
        if (string.IsNullOrEmpty(currentPassword) || currentPassword.Length > 1024 || !passwords.Verify(tx.User, currentPassword).Valid)
        {
            tx.User.RecordFailedLogin(now, authenticationPolicy.FailedAttemptLimit, TimeSpan.FromMinutes(authenticationPolicy.LockoutMinutes));
            tx.Audit(AuthenticationEvent.PasswordChangeFailed, now);
            await tx.CommitAsync(cancellationToken);
            throw new ApplicationFault(FaultKind.Validation, "AUTH.CURRENT_PASSWORD_INVALID", "Mật khẩu hiện tại không chính xác.");
        }
        if (!policy.Accepts(newPassword))
            throw new ApplicationFault(FaultKind.Validation, "AUTH.PASSWORD_POLICY", $"Mật khẩu phải có ít nhất {policy.MinimumPasswordLength} ký tự và {policy.MinimumDistinctCharacters} ký tự khác nhau.");
        if (currentPassword == newPassword)
            throw new ApplicationFault(FaultKind.Validation, "AUTH.PASSWORD_UNCHANGED", "Mật khẩu mới phải khác mật khẩu hiện tại.");
        tx.User.ResetPassword(passwords.Hash(tx.User, newPassword), now);
        await tx.RevokeAllSessionsAsync(now, cancellationToken);
        tx.Audit(AuthenticationEvent.PasswordChanged, now);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task EnsureActiveAsync(IAuthenticationTransaction tx, CancellationToken cancellationToken)
    {
        if (!tx.User.CanAuthenticate(clock.GetUtcNow()) || (await tx.ResolveAccessAsync(cancellationToken)) is not { IsUserActive: true, IsTenantActive: true })
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Sign in again.");
    }
}
