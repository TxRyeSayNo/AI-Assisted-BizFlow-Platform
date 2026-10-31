using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/ai/documents")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiDocumentSummarizerController(IAiDocumentSummarizerService summarizerService) : ControllerBase
{
    [HttpPost("summarize")]
    public async Task<ActionResult<DocumentSummarizeResult>> SummarizeDocument(
        [FromBody] DocumentSummarizeCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await summarizerService.SummarizeDocumentAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("create-tasks")]
    public async Task<ActionResult<CreateTasksFromSummaryResponse>> CreateTasksFromSummary(
        [FromBody] CreateTasksFromSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await summarizerService.CreateTasksFromSummaryAsync(command, cancellationToken);
        return Ok(result);
    }
}

