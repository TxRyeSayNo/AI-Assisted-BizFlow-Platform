using BizFlow.Api.Configuration;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Watchers and "@" mentions for a task or request (F3). All endpoints are new; the comment,
/// task and request controllers are untouched. The work item type is TASK or REQUEST.
/// </summary>
[ApiController, Authorize, Route("api/v1/work-items/{objectType}/{objectId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkItemWatchersController(WorkItemWatchService watchers, IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet("watch")]
    public Task<WorkItemWatchStatus> Status(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        Require();
        return watchers.GetStatusAsync(objectType, objectId, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record WatchInput;

    [HttpPut("watch")]
    public Task<WorkItemWatchStatus> Watch(string objectType, Guid objectId, WatchInput input, CancellationToken cancellationToken)
    {
        Require();
        return watchers.WatchAsync(objectType, objectId, cancellationToken);
    }

    [HttpDelete("watch")]
    public Task<WorkItemWatchStatus> Unwatch(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        Require();
        return watchers.UnwatchAsync(objectType, objectId, cancellationToken);
    }

    [HttpGet("mention-candidates")]
    public Task<IReadOnlyList<WorkItemParticipantInfo>> MentionCandidates(string objectType, Guid objectId,
        CancellationToken cancellationToken)
    {
        Require();
        return watchers.GetMentionCandidatesAsync(objectType, objectId, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record AddWatcherInput(Guid UserId);

    [HttpPost("watchers")]
    public Task<WorkItemWatchStatus> AddWatcher(string objectType, Guid objectId, AddWatcherInput input,
        CancellationToken cancellationToken)
    {
        Require();
        return watchers.AddParticipantWatcherAsync(objectType, objectId, input.UserId, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record MentionInput(Guid CommentId, Guid[] UserIds);

    [HttpPost("mentions")]
    public Task<WorkItemMentionResult> Mention(string objectType, Guid objectId, MentionInput input,
        CancellationToken cancellationToken)
    {
        Require();
        return watchers.RecordMentionsAsync(objectType, objectId, input.CommentId,
            input.UserIds ?? [], cancellationToken);
    }

    private void Require()
    {
        if (!features.Value.Mentions) throw ApplicationFault.NotFound();
    }
}
