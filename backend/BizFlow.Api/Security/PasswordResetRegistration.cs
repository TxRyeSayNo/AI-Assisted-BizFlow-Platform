using BizFlow.Application.Authentication;
using BizFlow.Infrastructure.Authentication;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Security;

public static class PasswordResetRegistration
{
    public static IServiceCollection AddBizFlowPasswordReset(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PasswordResetPolicy>().BindConfiguration("PasswordReset")
            .Validate(p => p.IsValid, "Invalid password reset policy.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PasswordResetPolicy>>().Value);
        services.AddOptions<ResetEmailSettings>().BindConfiguration("ResetEmail")
            .Validate(p => p.IsValid, "Invalid reset email settings. Require TLS except explicit loopback SMTP testing.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResetEmailSettings>>().Value);
        services.AddSingleton<IPasswordResetTokenProvider, JwtPasswordResetTokenProvider>();
        services.AddSingleton<IPasswordResetEmailSender, PasswordResetEmailSender>();
        services.AddScoped<PasswordResetService>();
        services.AddScoped<PasswordResetDeliveryService>();
        services.AddScoped<PasswordResetEmailJob>();
        services.AddHangfire((_, setup) => setup.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(
                configuration.GetConnectionString("Hangfire") ?? configuration.GetConnectionString("BizFlow") ??
                    throw new InvalidOperationException("A Hangfire PostgreSQL connection is required.")),
                new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true,
                    StartupConnectionMaxRetries = 0, AllowDegradedModeWithoutStorage = false }));
        services.AddSingleton<IPasswordResetQueue>(sp => new PasswordResetQueue(
            () => sp.GetRequiredService<IBackgroundJobClient>(), sp.GetRequiredService<ResetEmailSettings>()));
        services.AddHostedService<PasswordResetJobServer>();
        // Deliberately no public Hangfire dashboard or job-management endpoint.
        return services;
    }
}
