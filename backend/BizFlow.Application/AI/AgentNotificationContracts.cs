namespace BizFlow.Application.AI;

public sealed record AgentNotificationPreview(int RecipientCount, IReadOnlyList<string> Departments, string Title, string Content, IReadOnlyList<Guid> RecipientIds);
public sealed record AgentNotificationResult(int DeliveredCount, int AlreadyDeliveredCount, string Channel, Guid OperationId);

public interface IAgentNotificationService
{
    Task<AgentNotificationPreview> PreviewAsync(Guid tenantId, string toolName, string argsJson, CancellationToken cancellationToken = default);
    Task<AgentNotificationResult> SendAsync(Guid tenantId, Guid userId, string toolName, string argsJson, CancellationToken cancellationToken = default);
}
