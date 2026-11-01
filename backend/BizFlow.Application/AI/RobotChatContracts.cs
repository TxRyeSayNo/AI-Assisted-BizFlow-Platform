using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.AI;

public sealed record RobotChatMessageDto(
    string Sender, // "user" | "robot"
    string Text);

public sealed record AgentQuestionOptionDto(
    string Id,
    string Label,
    bool IsDefault = false);

public sealed record AgentQuestionDto(
    string Id,
    string Title,
    string Type, // "single_choice" | "multiple_choice" | "text"
    IReadOnlyList<AgentQuestionOptionDto>? Options = null,
    bool Required = true,
    string? Placeholder = null,
    string? HelpText = null,
    bool AllowCustomInput = true);

public sealed record AgentClarificationFormDto(
    string FormId,
    string Topic,
    string ContextExplanation,
    string? RiskAnalysis,
    IReadOnlyList<AgentQuestionDto> Questions,
    string SubmitButtonText = "Xác nhận & Gửi câu trả lời",
    string? SkipButtonText = "Bỏ qua & Sử dụng mặc định");

public sealed record AgentAnswerDto(
    string QuestionId,
    IReadOnlyList<string>? SelectedOptionIds = null,
    string? CustomText = null);

public sealed record RobotChatCommand(
    [Required, StringLength(2000, MinimumLength = 1)] string Message,
    string? Locale = null,
    IReadOnlyList<RobotChatMessageDto>? History = null,
    string? FormId = null,
    IReadOnlyList<AgentAnswerDto>? FormAnswers = null);

public sealed record RobotActionCard(
    string ActionType, // "OPEN_DECOMPOSITION" | "OPEN_NOTIFICATIONS" | "VIEW_TASK" | "PREFILL_PROMPT" | "CONFIRM_ACTION" | "VIEW_BRANCHES"
    string Title,
    string? LinkUrl,
    object? Payload = null);

public sealed record RobotChatResponse(
    string Response,
    string? RecognizedCommand, // "/task" | "/notifications" | "QUERY_DEADLINE" | "QUERY_OVERDUE" | "QUERY_PENDING" | "AGENT_CLARIFICATION_REQUIRED" | "AGENT_DECISION_FINALIZED" | "GENERAL"
    IReadOnlyList<RobotActionCard>? ActionCards = null,
    AgentClarificationFormDto? Questionnaire = null,
    string? AgentThought = null);

public sealed record DepartmentManagerContactDto(
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid ManagerUserId,
    string ManagerName,
    string ManagerEmail);

public sealed record MeetingDispatchResultDto(
    int InAppNotificationsSent,
    int EmailsSent,
    IReadOnlyList<DepartmentManagerContactDto> Recipients);

public interface IAgentMeetingCoordinator
{
    Task<MeetingDispatchResultDto> DispatchMeetingNotificationAsync(
        Guid tenantId,
        Guid senderUserId,
        string meetingTopic,
        string meetingTime,
        string meetingLocation,
        IReadOnlyList<string> participatingDepartments,
        IReadOnlyList<string>? logistics,
        CancellationToken cancellationToken = default);
}

public interface IRobotChatService
{
    Task<RobotChatResponse> ProcessMessageAsync(
        RobotChatCommand command,
        CancellationToken cancellationToken = default);
}
