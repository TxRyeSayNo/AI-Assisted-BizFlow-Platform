using BizFlow.Api.Configuration;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// The activity feed inside one task or request (F11). Read-only, gated by that item's own read
/// policy rather than by the auditor's <c>audit.read</c> permission, and it exposes only the shape
/// of each event — action, actor, time — never the before/after payloads.
/// </summary>
[ApiController, Authorize, Route("api/v1/work-items/{objectType}/{objectId:guid}/activity")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkItemActivityController(
    WorkItemActivityQuery activity,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet]
    public Task<ActivityPage> List(string objectType, Guid objectId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!features.Value.ActivityFeed) throw ApplicationFault.NotFound();
        return activity.ListAsync(objectType, objectId, page, pageSize, cancellationToken);
    }
}

/// <summary>
/// Applies one action to several tasks (F10). Each task goes through the same single-task command
/// it would otherwise, so nothing here can bypass the lifecycle policy, permissions, audit or SLA;
/// the response reports success or failure per task rather than all-or-nothing.
/// </summary>
[ApiController, Authorize, Route("api/v1/tasks/bulk")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TaskBulkController(
    TaskBulkOperations bulk,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record AssignInput(Guid[] TaskIds, string TargetType, Guid TargetId, string? Note = null);

    [HttpPost("assign")]
    public Task<BulkResult> Assign(AssignInput input, CancellationToken cancellationToken)
    {
        Require();
        return bulk.AssignAsync(input.TaskIds ?? [], input.TargetType, input.TargetId, input.Note, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ArchiveInput(Guid[] TaskIds);

    [HttpPost("archive")]
    public Task<BulkResult> Archive(ArchiveInput input, CancellationToken cancellationToken)
    {
        Require();
        return bulk.ArchiveAsync(input.TaskIds ?? [], cancellationToken);
    }

    private void Require()
    {
        if (!features.Value.BulkActions) throw ApplicationFault.NotFound();
    }
}
