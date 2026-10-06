using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using BizFlow.Application.Sla;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/sla-profiles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SlaProfilesController(SlaProfileCatalog profiles, SlaVersionCreation versions, SlaVersionHistory history) : ControllerBase
{
    [HttpGet]
    public Task<SlaProfilePage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default) =>
        profiles.ListAsync(new(page, pageSize, search, status), cancellationToken);
    [HttpPost]
    public async Task<IActionResult> Create(CreateSlaProfileInput input, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await profiles.CreateAsync(input.Name, cancellationToken));
    [HttpGet("{id:guid}/versions")]
    public Task<SlaVersionHistoryPage> History(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        history.ReadAsync(id, page, pageSize, cancellationToken);
    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(Guid id, CreateSlaVersionInput input, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await versions.CreateAsync(id,
            new(input.TargetMinutes, input.WarningMinutes, input.CalendarId, input.EscalationConfig), cancellationToken));
}

// Profile metadata only; unsupported version/calendar/status fields are rejected, not discarded.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSlaProfileInput([Required, StringLength(200)] string Name);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSlaVersionInput(int TargetMinutes, int WarningMinutes, Guid CalendarId, JsonElement? EscalationConfig = null);
