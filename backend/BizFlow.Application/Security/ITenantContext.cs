namespace BizFlow.Application.Security;

/// <summary>Identity from the validated principal; never from headers, routes or DTOs.</summary>
public interface ITenantContext
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
}

/// <summary>Only an internal workflow worker supplies this capability; HTTP claims never do.</summary>
public interface IWorkflowAgentContext : ITenantContext;
