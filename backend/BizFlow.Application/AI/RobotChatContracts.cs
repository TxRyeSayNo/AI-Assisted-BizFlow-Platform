using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.AI;

public sealed record RobotChatCommand(
    [Required, StringLength(2000, MinimumLength = 1)] string Message,
    string? Locale = null);

public sealed record RobotActionCard(
    string ActionType, // "OPEN_DECOMPOSITION" | "OPEN_NOTIFICATIONS" | "VIEW_TASK"
    string Title,
    string? LinkUrl,
    object? Payload = null);

public sealed record RobotChatResponse(
    string Response,
    string? RecognizedCommand, // "/task" | "/notifications" | "QUERY_DEADLINE" | "QUERY_OVERDUE" | "QUERY_PENDING" | "GENERAL"
    IReadOnlyList<RobotActionCard>? ActionCards = null);

public interface IRobotChatService
{
    Task<RobotChatResponse> ProcessMessageAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken = default);
}
