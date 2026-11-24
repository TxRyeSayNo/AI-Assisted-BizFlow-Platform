using BizFlow.Application.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkflowRuntimeController(IWorkflowRuntimeStore runtime) : ControllerBase
{
    [HttpGet("workflow-calendars")]
    public Task<IReadOnlyList<WorkflowCalendarOption>> Calendars(CancellationToken ct) => runtime.CalendarsAsync(ct);
    [HttpGet("requests/{id:guid}/workflow")]
    public Task<WorkflowRuntimeView?> Get(Guid id, CancellationToken ct) => runtime.GetAsync(id, ct);
    [HttpPost("requests/{id:guid}/workflow/start")]
    public Task<WorkflowRuntimeView> Start(Guid id, CancellationToken ct) => runtime.StartAsync(id, ct);
    [HttpPost("requests/{id:guid}/workflow/steps/{stepId:guid}/decisions")]
    public Task<WorkflowRuntimeView> Decide(Guid id, Guid stepId, WorkflowDecisionInput input, CancellationToken ct) => runtime.DecideAsync(id, stepId, input, ct);
    [HttpGet("workflow-inbox")]
    public Task<WorkflowInboxPage> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) => runtime.InboxAsync(page, pageSize, ct);
    [HttpGet("services/{id:guid}/form")]
    public Task<IReadOnlyList<WorkflowFormField>> Form(Guid id, CancellationToken ct) => runtime.FormAsync(id, ct);
    [HttpGet("workflow-metrics")]
    public Task<WorkflowMetrics> Metrics([FromQuery] int days = 30, CancellationToken ct = default) => runtime.MetricsAsync(days, ct);
    [HttpPost("requests/{id:guid}/workflow/steps/{stepId:guid}/delegations")]
    public Task<WorkflowRuntimeView> Delegate(Guid id, Guid stepId, WorkflowDelegationInput input, CancellationToken ct) => runtime.DelegateAsync(id, stepId, input, ct);
    [HttpPost("requests/{id:guid}/workflow/delegations/{delegationId:guid}/revoke")]
    public Task<WorkflowRuntimeView> Revoke(Guid id, Guid delegationId, WorkflowRevokeDelegationInput input, CancellationToken ct) => runtime.RevokeDelegationAsync(id, delegationId, input.Revision, ct);
    [HttpPost("requests/{id:guid}/workflow/steps/{stepId:guid}/sla/pause")]
    public Task<WorkflowRuntimeView> PauseSla(Guid id, Guid stepId, WorkflowSlaInput input, CancellationToken ct) => runtime.PauseSlaAsync(id, stepId, input, false, ct);
    [HttpPost("requests/{id:guid}/workflow/steps/{stepId:guid}/sla/resume")]
    public Task<WorkflowRuntimeView> ResumeSla(Guid id, Guid stepId, WorkflowSlaInput input, CancellationToken ct) => runtime.PauseSlaAsync(id, stepId, input, true, ct);
    [HttpPost("requests/{id:guid}/workflow/steps/{stepId:guid}/agent/retry")]
    public Task<WorkflowRuntimeView> RetryAgent(Guid id, Guid stepId, WorkflowAgentRetryInput input, CancellationToken ct) => runtime.RetryAgentAsync(id, stepId, input, ct);
    [HttpGet("requests/{id:guid}/workflow/steps/{stepId:guid}/agent/history")]
    public Task<IReadOnlyList<WorkflowAgentAttemptView>> AgentHistory(Guid id, Guid stepId, CancellationToken ct) => runtime.AgentHistoryAsync(id, stepId, ct);
}
