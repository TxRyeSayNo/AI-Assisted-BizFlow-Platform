using BizFlow.Application.Authentication;
using BizFlow.Domain.Organization;

namespace BizFlow.UnitTests.Security;

public sealed class PasswordResetPolicyTests
{
    [Fact]
    public void Reset_changes_stamp_clears_operational_lockout_and_preserves_business_status()
    {
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateTenantUser(Guid.NewGuid(), "EMP", "employee@example.test", "Employee", "old-hash", now);
        var stamp = user.SecurityStamp;
        user.RecordFailedLogin(now, 1, TimeSpan.FromMinutes(15));
        user.ResetPassword("new-hash", now.AddMinutes(1));
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.True(user.CanAuthenticate(now.AddMinutes(1)));
    }
    [Theory]
    [InlineData("short", false)]
    [InlineData("aaaaaaaaaaaaaaaa", false)]
    [InlineData("twelve chars passphrase", true)]
    public void Password_policy_is_length_and_diversity_based(string password, bool accepted) =>
        Assert.Equal(accepted, new PasswordResetPolicy().Accepts(password));
}
