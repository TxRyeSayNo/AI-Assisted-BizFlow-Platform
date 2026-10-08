using System;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Domain.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/ai")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiController(
    AiAssistanceService aiService,
    IAiStore aiStore) : ControllerBase
{
    // FR-AI-001: Assist task creation
    [HttpPost("task-assistance")]
    public async Task<ActionResult<TaskDraftSuggestion>> AssistTaskCreation(
        [FromBody] TaskAssistanceCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.AssistTaskCreationAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-002: Recommend task assignment
    [HttpPost("task-assignment-recommendation")]
    public async Task<ActionResult<TaskAssignmentRecommendationResult>> RecommendTaskAssignment(
        [FromBody] TaskAssignmentRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.RecommendTaskAssignmentAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-003: Recommend task parameters
    [HttpPost("task-parameters")]
    public async Task<ActionResult<TaskParametersRecommendationResult>> RecommendTaskParameters(
        [FromBody] TaskParametersRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.RecommendTaskParametersAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-004: Break down task
    [HttpPost("task-breakdown")]
    public async Task<ActionResult<TaskBreakdownResult>> BreakdownTask(
        [FromBody] TaskBreakdownCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.BreakdownTaskAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-005: Monitor task risk
    [HttpPost("task-risk")]
    public async Task<ActionResult<TaskRiskEvaluationResult>> EvaluateTaskRisk(
        [FromBody] TaskRiskEvaluationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.EvaluateTaskRiskAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-006: Summarize task progress
    [HttpPost("task-summary")]
    public async Task<ActionResult<TaskSummaryResult>> SummarizeTaskProgress(
        [FromBody] TaskSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.SummarizeTaskProgressAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-007: Analyze multi-intent request
    [HttpPost("request-multi-intent")]
    public async Task<ActionResult<MultiIntentAnalysisResult>> AnalyzeRequestMultiIntent(
        [FromBody] RequestMultiIntentCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.AnalyzeRequestMultiIntentAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-009: Recommend request routing
    [HttpPost("request-routing")]
    public async Task<ActionResult<RequestRoutingRecommendationResult>> RecommendRequestRouting(
        [FromBody] RequestRoutingRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.RecommendRequestRoutingAsync(command, cancellationToken);
        return Ok(result);
    }

    // FR-AI-010: Execute authorized AI action
    [HttpPost("actions/execute")]
    public async Task<ActionResult<ExecuteAiActionResult>> ExecuteAction(
        [FromBody] ExecuteAiActionCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await aiService.ExecuteActionAsync(command, cancellationToken);
        return Ok(result);
    }

    // Recommendation human decision recording (Accepted / Overridden / Dismissed)
    public sealed record RecordRecommendationDecisionDto(string Decision);

    [HttpPost("recommendations/{id:guid}/decision")]
    public async Task<IActionResult> RecordRecommendationDecision(
        Guid id,
        [FromBody] RecordRecommendationDecisionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<HumanDecision>(dto.Decision, true, out var decision))
            throw new ApplicationFault(FaultKind.Validation, "AI.INVALID_DECISION", "Decision must be ACCEPTED, OVERRIDDEN, or DISMISSED.");

        // Context check
        var tenantId = HttpContext.User.FindFirst("TenantId")?.Value is { } tidStr && Guid.TryParse(tidStr, out var tid)
            ? tid
            : throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "Active tenant context required.");

        var userId = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value is { } uidStr && Guid.TryParse(uidStr, out var uid)
            ? uid
            : throw new ApplicationFault(FaultKind.Forbidden, "USER.REQUIRED", "Active user context required.");

        await aiStore.RecordDecisionAsync(tenantId, id, decision, userId, cancellationToken);
        return NoContent();
    }
}
