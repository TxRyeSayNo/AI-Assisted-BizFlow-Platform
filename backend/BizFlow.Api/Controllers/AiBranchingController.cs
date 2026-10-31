using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/ai/branching")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiBranchingController(IAiBranchingService branchingService) : ControllerBase
{
    [HttpPost("plan")]
    public async Task<ActionResult<BranchingPlanResult>> PlanBranching(
        [FromBody] BranchPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await branchingService.PlanBranchingAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ExecuteBranchingPlanResult>> ExecuteBranchingPlan(
        [FromBody] ExecuteBranchingPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await branchingService.ExecuteBranchingPlanAsync(command, cancellationToken);
        return Ok(result);
    }
}

