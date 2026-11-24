using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// What a task waits for, and what waits on it (F7). The links are advisory: the task lifecycle is
/// untouched, so a task whose predecessor is still open can still be started — the view simply says
/// so. Both ends of a link must be tasks the caller can already open.
/// </summary>
[ApiController, Authorize, Route("api/v1/tasks/{taskId:guid}/dependencies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TaskDependenciesController(
    TaskDependencyService dependencies,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record AddInput(Guid DependsOnTaskId);

    [HttpGet]
    public Task<TaskDependencyView> Read(Guid taskId, CancellationToken cancellationToken)
    {
        Require();
        return dependencies.ReadAsync(taskId, cancellationToken);
    }

    [HttpPost]
    public Task<TaskDependencyView> Add(Guid taskId, AddInput input, CancellationToken cancellationToken)
    {
        Require();
        return dependencies.AddAsync(taskId, input.DependsOnTaskId, cancellationToken);
    }

    [HttpDelete("{dependsOnTaskId:guid}")]
    public Task<TaskDependencyView> Remove(Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken)
    {
        Require();
        return dependencies.RemoveAsync(taskId, dependsOnTaskId, cancellationToken);
    }

    private void Require()
    {
        if (!features.Value.TaskDependencies) throw ApplicationFault.NotFound();
    }
}
