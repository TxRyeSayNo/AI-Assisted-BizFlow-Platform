using BizFlow.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;

namespace BizFlow.UnitTests.Notifications;

public sealed class NotificationEmailConfigurationTests
{
    [Fact]
    public void Notification_email_inherits_reset_settings_and_respects_explicit_disable_and_overrides()
    {
        var values = new Dictionary<string,string?> {
            ["ResetEmail:Enabled"]="true",["ResetEmail:Host"]="smtp.example.test",["ResetEmail:Port"]="465",
            ["ResetEmail:SslOnConnect"]="true",["ResetEmail:FromAddress"]="sender@example.test",
            ["ResetEmail:FrontendOrigin"]="https://bizflow.example.test",["ResetEmail:Username"]="sender",["ResetEmail:Password"]="test-only-secret"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var settings = NotificationEmailConfiguration.Resolve(configuration);
        Assert.True(settings.Enabled);Assert.True(settings.IsValid);Assert.Equal(465,settings.Port);
        Assert.Equal("test-only-secret",settings.Password);Assert.True(settings.SslOnConnect);
        configuration["NotificationEmail:FromName"]="BizFlow Operations";
        Assert.Equal("BizFlow Operations",NotificationEmailConfiguration.Resolve(configuration).FromName);
        configuration["NotificationEmail:Enabled"]="false";
        Assert.False(NotificationEmailConfiguration.Resolve(configuration).Enabled);
        Assert.True(configuration.GetValue<bool>("ResetEmail:Enabled"));
        configuration["NotificationEmail:Enabled"]="true";
        configuration["NotificationEmail:FrontendOrigin"]="http://public.example.test";
        Assert.False(NotificationEmailConfiguration.Resolve(configuration).IsValid);
    }
}
