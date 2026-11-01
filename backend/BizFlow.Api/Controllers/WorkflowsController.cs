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
        CancellationToken cancellationToken = default) =>
        workflows.ListAsync(new(page, pageSize, search, businessType, status), cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkflowInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.CreateAsync(input.Name, input.BusinessType, cancellationToken);
        return Created($"/api/v1/workflow-versions/{result.LatestVersionId}", result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await workflows.DeleteWorkflowAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateWorkflowStatusInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.UpdateStatusAsync(id, input.Status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    public Task<IReadOnlyList<WorkflowVersionSummary>> ListVersions(Guid id, CancellationToken cancellationToken) =>
        workflows.GetWorkflowVersionsAsync(id, cancellationToken);

    [HttpGet("/api/v1/workflow-versions/{id:guid}")]
    public Task<WorkflowVersionView> ReadVersion(Guid id, CancellationToken cancellationToken) =>
        workflows.ReadVersionAsync(id, cancellationToken);

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateNextVersion(Guid id, CreateWorkflowVersionInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.CreateNextVersionAsync(id, cancellationToken);
        return Created($"/api/v1/workflow-versions/{result.LatestVersionId}", result);
    }

    [HttpPut("/api/v1/workflow-versions/{id:guid}")]
    public async Task<IActionResult> SaveDraft(Guid id, SaveDraftInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.SaveDraftAsync(id, new(input.DefinitionJson, input.Steps, input.Transitions, input.Name), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/name")]
    public async Task<IActionResult> UpdateName(Guid id, UpdateWorkflowNameInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.UpdateNameAsync(id, input.Name, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/v1/workflow-versions/{id:guid}/validate")]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken)
    {
        var result = await workflows.ValidateVersionAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/v1/workflow-versions/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, PublishWorkflowInput input, CancellationToken cancellationToken)
    {
        var result = await workflows.PublishVersionAsync(id, new(input.ServiceId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("lookup/departments")]
    public Task<IReadOnlyList<WorkflowLookupItem>> GetDepartments(CancellationToken cancellationToken) =>
        workflows.GetActiveDepartmentsAsync(cancellationToken);

    [HttpGet("lookup/roles")]
    public Task<IReadOnlyList<WorkflowLookupItem>> GetRoles(CancellationToken cancellationToken) =>
        workflows.GetActiveRolesAsync(cancellationToken);

    [HttpGet("lookup/services")]
    public Task<IReadOnlyList<WorkflowServiceLookupItem>> GetServices(CancellationToken cancellationToken) =>
        workflows.GetServicesAsync(cancellationToken);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWorkflowInput([Required, StringLength(200)] string Name, [Required] string BusinessType = "REQUEST");

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWorkflowVersionInput;

public sealed record SaveDraftInput(
    [Required] string DefinitionJson,
    IReadOnlyList<WorkflowStepDraftInput>? Steps = null,
    IReadOnlyList<WorkflowTransitionDraftInput>? Transitions = null,
    string? Name = null);

public sealed record PublishWorkflowInput(Guid? ServiceId = null);

public sealed record UpdateWorkflowStatusInput([Required] string Status);

public sealed record UpdateWorkflowNameInput([Required, StringLength(200)] string Name);
