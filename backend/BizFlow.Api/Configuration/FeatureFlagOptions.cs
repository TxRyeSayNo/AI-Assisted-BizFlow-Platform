namespace BizFlow.Api.Configuration;

/// <summary>
/// Switches for additive features. Every flag defaults to <c>false</c> so that removing its
/// configuration entry leaves the platform behaving exactly as it did before the feature existed;
/// the shipped appsettings files turn the flags on explicitly.
/// </summary>
public sealed class FeatureFlagOptions
{
    public const string SectionName = "Features";

    /// <summary>Kanban board read models and their endpoints (F1).</summary>
    public bool Kanban { get; set; }

    /// <summary>Deadline calendar read model and its endpoint (F2).</summary>
    public bool Calendar { get; set; }

    /// <summary>Watchers and "@" mentions, with their endpoints (F3).</summary>
    public bool Mentions { get; set; }

    /// <summary>Deadline reminder configuration endpoints (F4, INT-032).</summary>
    public bool DeadlineReminders { get; set; }

    /// <summary>Report downloads in xlsx, csv and pdf, plus the charts on the report page (F5).</summary>
    public bool ReportExport { get; set; }

    /// <summary>The label catalogue and the labels carried by work items (F6).</summary>
    public bool Labels { get; set; }

    /// <summary>Direct messages between colleagues, with their endpoints and realtime hub (F13).</summary>
    public bool DirectMessages { get; set; }

    /// <summary>Bulk assign and archive over the existing per-task commands (F10).</summary>
    public bool BulkActions { get; set; }

    /// <summary>The read-only activity feed inside a task or request (F11).</summary>
    public bool ActivityFeed { get; set; }

    /// <summary>
    /// Hangfire server plus the recurring jobs that feed watcher activity (F3) and deadline
    /// reminders (F4). Unlike the read-only features this one starts a worker and creates the
    /// Hangfire schema, so it stays off unless a deployment asks for it — that also keeps it out
    /// of every integration test, which boots the API in the Development environment.
    /// </summary>
    public bool BackgroundJobs { get; set; }
}
