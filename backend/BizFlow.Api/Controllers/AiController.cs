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
    IAiStore aiStore,
    IAiVectorMemoryStore? vectorMemoryStore = null,
    IProactiveAgentScanner? proactiveScanner = null,
    IAiSelfReflectionService? selfReflectionService = null) : ControllerBase
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

    private Guid GetTenantId() =>
        HttpContext.User.FindFirst("TenantId")?.Value is { } tidStr && Guid.TryParse(tidStr, out var tid)
            ? tid
            : throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "Active tenant context required.");

    // --- Pillar 1: Long-term Vector Memory & RAG ---
    [HttpPost("memory")]
    public async Task<ActionResult<AiMemoryItemDto>> StoreMemory(
        [FromBody] StoreMemoryCommand command,
        CancellationToken cancellationToken = default)
    {
        if (vectorMemoryStore == null)
            throw new ApplicationFault(FaultKind.Unavailable, "AI.MEMORY_UNAVAILABLE", "Vector memory service is not available.");

        var tenantId = GetTenantId();
        var item = await vectorMemoryStore.StoreMemoryAsync(
            tenantId, command.MemoryScope, command.Title, command.Content, command.MetadataJson, cancellationToken);

        return Ok(new AiMemoryItemDto(
            item.Id, item.MemoryScope, item.Title, item.Content, item.MetadataJson, item.CreatedAt));
    }

    [HttpPost("memory/search")]
    public async Task<ActionResult<IReadOnlyList<AiMemorySearchResult>>> SearchMemory(
        [FromBody] SearchMemoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (vectorMemoryStore == null)
            return Ok(Array.Empty<AiMemorySearchResult>());

        var tenantId = GetTenantId();
        var results = await vectorMemoryStore.SearchMemoriesAsync(
            tenantId, query.Query, query.MemoryScope, query.MaxResults, query.MinScore, cancellationToken);

        return Ok(results);
    }

    [HttpGet("memory/recent")]
    public async Task<ActionResult<IReadOnlyList<AiMemoryItemDto>>> GetRecentMemories(
        [FromQuery] string? scope = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (vectorMemoryStore == null)
            return Ok(Array.Empty<AiMemoryItemDto>());

        var tenantId = GetTenantId();
        var list = await vectorMemoryStore.GetRecentMemoriesAsync(tenantId, scope, limit, cancellationToken);
        var dtos = list.Select(m => new AiMemoryItemDto(
            m.Id, m.MemoryScope, m.Title, m.Content, m.MetadataJson, m.CreatedAt)).ToList();

        return Ok(dtos);
    }

    // --- Pillar 2: Proactive Autonomous Agent ---
    [HttpPost("proactive/trigger-scan")]
    public async Task<ActionResult<ProactiveScanResult>> TriggerProactiveScan(
        CancellationToken cancellationToken = default)
    {
        if (proactiveScanner == null)
            throw new ApplicationFault(FaultKind.Unavailable, "AI.SCANNER_UNAVAILABLE", "Proactive scanner service is not available.");

        var tenantId = GetTenantId();
        var result = await proactiveScanner.RunProactiveScanAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("proactive/insights")]
    public async Task<ActionResult<IReadOnlyList<ProactiveInsightDto>>> GetProactiveInsights(
        CancellationToken cancellationToken = default)
    {
        if (proactiveScanner == null)
            return Ok(Array.Empty<ProactiveInsightDto>());

        var tenantId = GetTenantId();
        var insights = await proactiveScanner.GetActiveProactiveInsightsAsync(tenantId, cancellationToken);
        return Ok(insights);
    }

    // --- Pillar 3: Self-Reflection & Adaptive Learning ---
    [HttpGet("reflection/metrics")]
    public async Task<ActionResult<ReflectionMetricsDto>> GetReflectionMetrics(
        CancellationToken cancellationToken = default)
    {
        if (selfReflectionService == null)
            throw new ApplicationFault(FaultKind.Unavailable, "AI.REFLECTION_UNAVAILABLE", "Self-reflection service is not available.");

        var tenantId = GetTenantId();
        var metrics = await selfReflectionService.GetReflectionMetricsAsync(tenantId, cancellationToken);
        return Ok(metrics);
    }

    [HttpGet("reflection/rules")]
    public async Task<ActionResult<IReadOnlyList<ReflectionCritiqueRule>>> GetCritiqueRules(
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        if (selfReflectionService == null)
            return Ok(Array.Empty<ReflectionCritiqueRule>());

        var tenantId = GetTenantId();
        var rules = await selfReflectionService.GetCritiqueRulesAsync(tenantId, type, cancellationToken);
        return Ok(rules);
    }
}
