using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/ai/learned-choices")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiLearnedChoicesController(
    IAiLearnedChoiceStore store,
    ITenantContext tenantContext,
    TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LearnedChoiceDto>>> List(
        [FromQuery] string? semanticKey = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "AUTH.REQUIRED", "Authentication required.");
        var userId = tenantContext.UserId;

        var items = await store.ListChoicesAsync(tenantId, userId, semanticKey, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<LearnedChoiceDto>> Create(
        [FromBody] SaveLearnedChoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "AUTH.REQUIRED", "Authentication required.");
        var userId = tenantContext.UserId;

        var choice = await store.RecordChoiceAsync(
            tenantId,
            userId,
            command.SemanticKey,
            command.ChoiceValue,
            command.ChoiceLabel,
            command.Scope,
            clock.GetUtcNow(),
            cancellationToken);

        return Ok(new LearnedChoiceDto(
            choice.Id,
            choice.TenantId,
            choice.UserId,
            choice.SemanticKey,
            choice.ChoiceValue,
            choice.ChoiceLabel,
            choice.Scope,
            choice.UsageCount,
            choice.LastUsedAt,
            choice.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "AUTH.REQUIRED", "Authentication required.");
        var userId = tenantContext.UserId;

        var deleted = await store.DeleteChoiceAsync(tenantId, userId, id, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/promote")]
    public async Task<IActionResult> Promote(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "AUTH.REQUIRED", "Authentication required.");

        var promoted = await store.PromoteToTenantScopeAsync(tenantId, id, cancellationToken);
        if (!promoted)
            return NotFound();

        return Ok(new { success = true, message = "Promoted to tenant-wide choice successfully." });
    }
}

