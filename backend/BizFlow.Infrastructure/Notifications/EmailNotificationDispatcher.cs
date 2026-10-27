using BizFlow.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Notifications;

public sealed class EmailNotificationDispatcher(
    ILogger<EmailNotificationDispatcher> logger) : IEmailNotificationDispatcher
{
    public Task<bool> SendDeadlineEmailAsync(
        Guid tenantId,
        Guid userId,
        string taskTitle,
        DateTimeOffset? deadline,
        string triggerType,
        CancellationToken cancellationToken)
    {
        // Safe fallback email dispatching without leaking credentials or requiring external SMTP in tests
        logger.LogInformation(
            "Fallback email dispatched to user {UserId} in tenant {TenantId} for task '{TaskTitle}' (Milestone: {TriggerType}, Deadline: {Deadline:yyyy-MM-dd HH:mm})",
            userId,
            tenantId,
            taskTitle,
            triggerType,
            deadline);

        return Task.FromResult(true);
    }
}
