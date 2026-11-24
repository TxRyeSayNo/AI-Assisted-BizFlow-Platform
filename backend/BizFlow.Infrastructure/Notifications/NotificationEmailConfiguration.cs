using BizFlow.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;

namespace BizFlow.Infrastructure.Notifications;

public static class NotificationEmailConfiguration
{
    // Share reset-password SMTP credentials by default. Explicit notification overrides win,
    // including Enabled=false; never silently fall back from an invalid explicit override.
    public static ResetEmailSettings Resolve(IConfiguration configuration)
    {
        var settings = configuration.GetSection("ResetEmail").Get<ResetEmailSettings>() ?? new();
        configuration.GetSection("NotificationEmail").Bind(settings);
        return settings;
    }
}
