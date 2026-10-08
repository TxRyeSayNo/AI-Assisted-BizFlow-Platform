using BizFlow.Application.Collaboration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/records")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RecordsController(RecordService recordService) : ControllerBase
{
    [HttpPost("{type}/{id:guid}/archive")]
    public async Task<ActionResult<RecordArchivalResult>> Archive(
        string type,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await recordService.ArchiveRecordAsync(type, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<RecordSearchPagedResult>> Search(
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "query")] string? query,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var searchText = !string.IsNullOrWhiteSpace(q) ? q : query;
        var searchQuery = new RecordSearchQuery(
            Query: searchText,
            Type: type,
            Status: status,
            Priority: priority,
            From: from,
            To: to,
            IncludeArchived: includeArchived,
            Page: page,
            PageSize: pageSize);

        var result = await recordService.SearchRecordsAsync(searchQuery, cancellationToken);
        return Ok(result);
    }
}
