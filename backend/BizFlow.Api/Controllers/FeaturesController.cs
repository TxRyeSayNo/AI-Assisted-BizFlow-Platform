using BizFlow.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Reports which additive features this deployment has enabled, so the client can hide an entry
/// point instead of letting the user walk into a disabled endpoint. These flags are deployment
/// configuration, never an authorization decision: the server still enforces permissions itself.
/// </summary>
[ApiController, Authorize, Route("api/v1/features")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FeaturesController(IOptions<FeatureFlagOptions> features) : ControllerBase
{
    public sealed record FeatureFlagsView(bool Kanban, bool Calendar, bool Mentions,
        bool DeadlineReminders, bool ReportExport, bool Labels, bool DirectMessages,
        bool BulkActions, bool ActivityFeed, bool TaskDependencies);

    [HttpGet]
    public FeatureFlagsView Get() => new(features.Value.Kanban, features.Value.Calendar,
        features.Value.Mentions, features.Value.DeadlineReminders, features.Value.ReportExport,
        features.Value.Labels, features.Value.DirectMessages, features.Value.BulkActions,
        features.Value.ActivityFeed, features.Value.TaskDependencies);
}
