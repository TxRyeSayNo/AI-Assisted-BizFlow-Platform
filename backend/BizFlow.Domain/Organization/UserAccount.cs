using BizFlow.Domain.Common;

namespace BizFlow.Domain.Organization;

public enum UserStatus { Active, Inactive, Locked }

public sealed class UserAccount
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    // Account plane discriminator, not a permission grant. All authority still comes from permissions.
    public bool IsPlatformAdministrator { get; private set; }
    public string EmployeeCode { get; private set; } = "";
    public string NormalizedEmployeeCode { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string NormalizedEmail { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string SecurityStamp { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public Guid? DepartmentId { get; private set; }
    public UserStatus Status { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public bool MustChangePassword { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    private UserAccount() { }

    public bool CanAuthenticate(DateTimeOffset now) => Status == UserStatus.Active && DeletedAt is null &&
        !MustChangePassword && (LockoutEnd is null || LockoutEnd <= now);

    public bool CanResetPassword => Status == UserStatus.Active && DeletedAt is null;

    public void ResetPassword(string passwordHash, DateTimeOffset now)
    {
        if (!CanResetPassword) throw new InvalidOperationException("This account cannot reset its password.");
        PasswordHash = EntityRules.Text(passwordHash, 2048, nameof(passwordHash));
        SecurityStamp = Guid.NewGuid().ToString("N");
        MustChangePassword = false;
        AccessFailedCount = 0; LockoutEnd = null; UpdatedAt = now.ToUniversalTime();
    }

    public void RecordFailedLogin(DateTimeOffset now, int failureLimit, TimeSpan lockoutDuration)
    {
        if (failureLimit < 1 || lockoutDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(failureLimit));
        if (!CanAuthenticate(now)) return;
        if (LockoutEnd is not null) { AccessFailedCount = 0; LockoutEnd = null; }
        AccessFailedCount++;
        if (AccessFailedCount >= failureLimit) LockoutEnd = now.ToUniversalTime().Add(lockoutDuration);
        UpdatedAt = now.ToUniversalTime();
    }

    public void RecordSuccessfulLogin(DateTimeOffset now, string? upgradedHash = null)
    {
        if (!CanAuthenticate(now)) throw new InvalidOperationException("This account cannot authenticate.");
        AccessFailedCount = 0; LockoutEnd = null; LastLoginAt = now.ToUniversalTime(); UpdatedAt = now.ToUniversalTime();
        if (upgradedHash is not null) PasswordHash = EntityRules.Text(upgradedHash, 2048, nameof(upgradedHash));
    }

    public static UserAccount CreateTenantUser(Guid tenantId, string employeeCode, string email,
        string fullName, string passwordHash, DateTimeOffset now, Guid? departmentId = null) =>
        Create(EntityRules.Id(tenantId, nameof(tenantId)), employeeCode, email, fullName, passwordHash, now, departmentId);

    public static UserAccount CreatePlatformAdministrator(string employeeCode, string email,
        string fullName, string passwordHash, DateTimeOffset now) =>
        Create(null, employeeCode, email, fullName, passwordHash, now, null);

    private static UserAccount Create(Guid? tenantId, string employeeCode, string email, string fullName,
        string passwordHash, DateTimeOffset now, Guid? departmentId)
    {
        var code = EntityRules.Text(employeeCode, 50, nameof(employeeCode));
        var address = EntityRules.Email(email);
        return new UserAccount
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, IsPlatformAdministrator = tenantId is null,
            EmployeeCode = code, NormalizedEmployeeCode = code.ToUpperInvariant(), Email = address,
            NormalizedEmail = address.ToUpperInvariant(), FullName = EntityRules.Text(fullName, 200, nameof(fullName)),
            PasswordHash = EntityRules.Text(passwordHash, 2048, nameof(passwordHash)),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            DepartmentId = departmentId is { } id ? EntityRules.Id(id, nameof(departmentId)) : null,
            Status = UserStatus.Active, CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
        };
    }
}
