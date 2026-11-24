using BizFlow.Infrastructure.Collaboration;
using BizFlow.Infrastructure.Notifications;
using Hangfire;
using Hangfire.PostgreSql;
using BizFlow.Infrastructure.Workflows;

namespace BizFlow.Api.Configuration;

/// <summary>
/// Wires the Hangfire server and its recurring jobs, and only when
/// <see cref="FeatureFlagOptions.BackgroundJobs"/> is on. With the flag off nothing is registered,
/// no worker starts and the Hangfire schema is never created, so the application behaves exactly
/// as it did before these features existed.
/// </summary>
internal static class BackgroundJobRegistration
{
    internal const string WatchedItemActivityJob = "f3-watched-item-activity";
    internal const string DeadlineReminderJob = "f4-deadline-reminders";

    internal static bool Enabled(IConfiguration configuration) =>
        configuration.GetValue($"{FeatureFlagOptions.SectionName}:{nameof(FeatureFlagOptions.BackgroundJobs)}", false);

    internal static void AddBizFlowBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        // The notifier itself is always available so it can be driven directly by a test or a
        // future on-demand endpoint; only the scheduler is conditional.
        services.AddScoped<IWatchedItemActivityNotifier, WatchedItemActivityNotifier>();
        services.AddScoped<IDeadlineReminderDispatcher, DeadlineReminderDispatcher>();
        services.AddScoped<IWorkflowDeadlineDispatcher, WorkflowDeadlineDispatcher>();
        if (!Enabled(configuration)) return;
        var connection = configuration.GetConnectionString("BizFlow")
            ?? throw new InvalidOperationException("ConnectionStrings:BizFlow is required for background jobs.");
        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(connection)));
        services.AddHangfireServer();
    }

    internal static void UseBizFlowBackgroundJobs(this WebApplication app)
    {
        if (!Enabled(app.Configuration)) return;
        // The instance manager from DI, not Hangfire's static RecurringJob facade: the static one
        // reads JobStorage.Current, which is not initialised yet at this point in startup.
        var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
        // Every two minutes: late enough to batch a burst of edits, soon enough to feel live.
        jobs.AddOrUpdate<IWatchedItemActivityNotifier>(
            WatchedItemActivityJob,
            notifier => notifier.SweepAsync(CancellationToken.None),
            "*/2 * * * *");
        // Every five minutes is fine for marks measured in hours, and keeps the load low.
        jobs.AddOrUpdate<IDeadlineReminderDispatcher>(
            DeadlineReminderJob,
            dispatcher => dispatcher.SweepAsync(CancellationToken.None),
            "*/5 * * * *");
        jobs.AddOrUpdate<IWorkflowDeadlineDispatcher>("workflow-step-deadlines",
            dispatcher=>dispatcher.SweepAsync(CancellationToken.None),"*/5 * * * *");
        jobs.AddOrUpdate<TaskDigestService>("task-daily-digests",
            service=>service.SweepAsync(CancellationToken.None),"*/5 * * * *");
    }
}
