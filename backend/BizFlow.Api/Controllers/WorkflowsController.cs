using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BizFlow.Application.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/workflows")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkflowsController(WorkflowLibrary workflows) : ControllerBase
{
    [HttpGet]
    public Task<WorkflowPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? businessType = null, [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) => workflows.ListAsync(new(page, pageSize, search, businessType, status), cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkflowInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.CreateAsync(input.Name, input.BusinessType, cancellationToken);
        return Created($"/api/v1/workflow-versions/{result.LatestVersionId}", result);
    }

    [HttpGet("/api/v1/workflow-versions/{id:guid}")]
    public Task<WorkflowVersionView> ReadVersion(Guid id, CancellationToken cancellationToken) => workflows.ReadVersionAsync(id, cancellationToken);

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateNextVersion(Guid id, CreateWorkflowVersionInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.CreateNextVersionAsync(id, cancellationToken);
        return Created($"/api/v1/workflow-versions/{result.LatestVersionId}", result);
    }
}

// Reject unsupported authoring/status/tenant fields instead of silently dropping supplied configuration.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWorkflowInput([Required, StringLength(200)] string Name, [Required] string BusinessType = "REQUEST");

// Explicit empty-draft command: no client-selected tenant, number, status or copied graph.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWorkflowVersionInput;
