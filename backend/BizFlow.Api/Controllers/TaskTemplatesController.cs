using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Task templates (F9) and their optional recurrence (F8) — one catalogue, because "a template"
/// and "a task that repeats" are the same thing with and without a schedule.
///
/// Creating a task from a template goes through the ordinary task creation command, so the
/// permission check, the idempotency guard, the audit entry and the lifecycle policy all apply
/// exactly as they do when a person types the fields in.
/// </summary>
[ApiController, Authorize, Route("api/v1/task-templates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TaskTemplatesController(
    TaskTemplateService templates,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TemplateInput(string Name, string TitleTemplate, string? Description = null,
        string? Priority = null, int? DeadlineOffsetDays = null, string? RecurrenceFrequency = null,
        int? RecurrenceDay = null, int? RecurrenceHourUtc = null);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ArchiveInput(bool Archived);

    [HttpGet]
    public Task<IReadOnlyList<TaskTemplateView>> List([FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        Require();
        return templates.ListAsync(includeArchived, cancellationToken);
    }

    [HttpPost]
    public Task<TaskTemplateView> Create(TemplateInput input, CancellationToken cancellationToken)
    {
        Require();
        return templates.CreateAsync(Map(input), cancellationToken);
    }

    [HttpPut("{templateId:guid}")]
    public Task<TaskTemplateView> Update(Guid templateId, TemplateInput input, CancellationToken cancellationToken)
    {
        Require();
        return templates.UpdateAsync(templateId, Map(input), cancellationToken);
    }

    [HttpPost("{templateId:guid}/archive")]
    public Task<TaskTemplateView> Archive(Guid templateId, ArchiveInput input, CancellationToken cancellationToken)
    {
        Require();
        return templates.ArchiveAsync(templateId, input.Archived, cancellationToken);
    }

    /// <summary>Creates one task from the template, honouring an Idempotency-Key like any create.</summary>
    [HttpPost("{templateId:guid}/tasks")]
    public Task<TaskCreatedView> Instantiate(Guid templateId,
        [FromHeader(Name = "Idempotency-Key")] string? key = null, CancellationToken cancellationToken = default)
    {
        Require();
        return templates.InstantiateAsync(templateId, key, cancellationToken);
    }

    private static TaskTemplateInput Map(TemplateInput input) =>
        new(input.Name, input.TitleTemplate, input.Description, input.Priority, input.DeadlineOffsetDays,
            input.RecurrenceFrequency, input.RecurrenceDay, input.RecurrenceHourUtc);

    private void Require()
    {
        if (!features.Value.TaskTemplates) throw ApplicationFault.NotFound();
    }
}

/// <summary>
/// A person's own saved list filters (F12). Private to their owner: there is no endpoint here that
/// reads anyone else's, and a saved filter grants no visibility — the list endpoints re-validate
/// the query and re-apply their read policy on every call.
/// </summary>
[ApiController, Authorize, Route("api/v1/saved-filters")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SavedFiltersController(
    SavedFilterService filters,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateInput(string Scope, string Name, string? Query = null);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record UpdateInput(string Name, string? Query = null);

    [HttpGet]
    public Task<IReadOnlyList<SavedFilterView>> List([FromQuery] string scope = "TASK",
        CancellationToken cancellationToken = default)
    {
        Require();
        return filters.ListAsync(scope, cancellationToken);
    }

    [HttpPost]
    public Task<SavedFilterView> Create(CreateInput input, CancellationToken cancellationToken)
    {
        Require();
        return filters.CreateAsync(input.Scope, input.Name, input.Query, cancellationToken);
    }

    [HttpPut("{savedFilterId:guid}")]
    public Task<SavedFilterView> Update(Guid savedFilterId, UpdateInput input, CancellationToken cancellationToken)
    {
        Require();
        return filters.UpdateAsync(savedFilterId, input.Name, input.Query, cancellationToken);
    }

    [HttpDelete("{savedFilterId:guid}")]
    public async Task<IActionResult> Delete(Guid savedFilterId, CancellationToken cancellationToken)
    {
        Require();
        await filters.DeleteAsync(savedFilterId, cancellationToken);
        return NoContent();
    }

    private void Require()
    {
        if (!features.Value.SavedFilters) throw ApplicationFault.NotFound();
    }
}
