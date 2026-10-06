using BizFlow.Domain.Organization;

namespace BizFlow.UnitTests.Security;

public sealed class AccountAuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);
    private static UserAccount Account() => UserAccount.CreateTenantUser(Guid.NewGuid(), "EMP", "employee@example.test", "Employee", "hash", Now);

    [Fact]
    public void Lockout_is_temporary_operational_metadata_not_a_new_business_state()
    {
        var user = Account();
        for (var i = 0; i < 5; i++) user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        Assert.False(user.CanAuthenticate(Now));
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Throws<InvalidOperationException>(() => user.RecordSuccessfulLogin(Now));
        user.RecordFailedLogin(Now.AddMinutes(1), 5, TimeSpan.FromMinutes(15));
        Assert.Equal(Now.AddMinutes(15), user.LockoutEnd);
        Assert.True(user.CanAuthenticate(Now.AddMinutes(15)));
        user.RecordFailedLogin(Now.AddMinutes(15), 5, TimeSpan.FromMinutes(15));
        Assert.Equal(1, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public void Successful_login_clears_failures_and_accepts_Identity_hash_upgrade()
    {
        var user = Account();
        user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        user.RecordSuccessfulLogin(Now.AddMinutes(1), "upgraded-hash");
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
        Assert.Equal("upgraded-hash", user.PasswordHash);
        Assert.Equal(Now.AddMinutes(1), user.LastLoginAt);
    }
}
