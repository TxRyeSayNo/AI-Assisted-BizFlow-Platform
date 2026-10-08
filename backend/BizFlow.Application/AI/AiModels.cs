using System;
using System.Collections.Generic;
using BizFlow.Domain.AI;

namespace BizFlow.Application.AI;

// FR-AI-001: Task Assistance
public sealed record TaskAssistanceCommand(
    string Prompt,
    Guid? ServiceId = null,
    Guid? DepartmentId = null);

public sealed record TaskDraftSuggestion(
    string Title,
    string Description,
    string Priority,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? ServiceId,
    string? ServiceName,
    int? SuggestedDurationDays,
    IReadOnlyList<string> ChecklistItems,
    decimal Confidence,
    string Reasoning);

// FR-AI-002: Task Assignment Recommendation
public sealed record TaskAssignmentRecommendationCommand(
    Guid? TaskId,
    string Title,
    string Description,
    Guid? DepartmentId = null);

public sealed record CandidateAssignee(
    Guid UserId,
    string FullName,
    string Email,
    int ActiveTaskCount,
    decimal Score);

public sealed record TaskAssignmentRecommendationResult(
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? RecommendedAssigneeId,
    string? RecommendedAssigneeName,
    decimal Confidence,
    string Rationale,
    IReadOnlyList<CandidateAssignee> Candidates);

// FR-AI-003: Task Parameters Recommendation
public sealed record TaskParametersRecommendationCommand(
    Guid? TaskId,
    string Title,
    string Description,
    Guid? ServiceId = null);

public sealed record TaskParametersRecommendationResult(
    string Priority,
    Guid? WorkflowVersionId,
    string? WorkflowName,
    Guid? SlaVersionId,
    string? SlaName,
    int? SuggestedDeadlineDays,
    IReadOnlyList<string> SuggestedChecklistItems,
    decimal Confidence,
    string Rationale);

// FR-AI-004: Task Breakdown
public sealed record TaskBreakdownCommand(
    Guid? TaskId,
    string Title,
    string Description,
    int? TargetSubtaskCount = null);

public sealed record SubtaskProposal(
    int OrderIndex,
    string Title,
    string Description,
    int? EstimatedDays,
    int? DependsOnOrderIndex);

public sealed record TaskBreakdownResult(
    IReadOnlyList<SubtaskProposal> Subtasks,
    decimal Confidence,
    string Strategy);

// FR-AI-005: Task Risk Evaluation
public sealed record TaskRiskEvaluationCommand(Guid TaskId);

public sealed record TaskRiskEvaluationResult(
    Guid TaskId,
    string RiskLevel,
    decimal RiskScore,
    IReadOnlyList<string> RiskFactors,
    IReadOnlyList<string> MitigationAdvice,
    DateTimeOffset EvaluatedAt);

// FR-AI-006: Task Summary
public sealed record TaskSummaryCommand(Guid TaskId);

public sealed record TaskSummaryResult(
    Guid TaskId,
    string Summary,
    IReadOnlyList<string> KeyMilestones,
    IReadOnlyList<string> PendingActions,
    DateTimeOffset GeneratedAt);

// FR-AI-007: Multi-Intent Request Analysis
public sealed record RequestMultiIntentCommand(
    Guid? RequestId,
    string Title,
    string Description);

public sealed record IntentProposal(
    int IntentIndex,
    string Title,
    string Description,
    Guid? SuggestedServiceId,
    string? SuggestedServiceName,
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryName,
    decimal Confidence);

public sealed record MultiIntentAnalysisResult(
    bool IsMultiIntent,
    IReadOnlyList<IntentProposal> Intents,
    decimal Confidence,
    string Rationale);

// FR-AI-008: Request Split
public sealed record SplitRequestItem(
    string Title,
    string Description,
    Guid? ServiceId = null,
    Guid? CategoryId = null,
    string? Priority = null);

public sealed record SplitRequestCommand(
    IReadOnlyList<SplitRequestItem> Splits);

public sealed record ChildRequestSummary(
    Guid RequestId,
    string Title,
    string Status,
    Guid ServiceId,
    Guid CategoryId);

public sealed record SplitRequestResult(
    Guid ParentRequestId,
    IReadOnlyList<ChildRequestSummary> Children,
    int SplitCount);

// FR-AI-009: Request Routing Recommendation
public sealed record RequestRoutingRecommendationCommand(
    Guid? RequestId,
    string Title,
    string Description,
    Guid? ServiceId = null);

public sealed record AlternativeRoute(
    Guid ServiceId,
    string ServiceName,
    Guid? DepartmentId,
    string? DepartmentName,
    decimal Score);

public sealed record RequestRoutingRecommendationResult(
    Guid? RecommendedServiceId,
    string? RecommendedServiceName,
    Guid? RecommendedCategoryId,
    string? RecommendedCategoryName,
    Guid? RecommendedDepartmentId,
    string? RecommendedDepartmentName,
    string Priority,
    decimal Confidence,
    string Rationale,
    IReadOnlyList<AlternativeRoute> Alternatives);

// FR-AI-010: AI Action Execution
public sealed record ExecuteAiActionCommand(
    string ToolName,
    string ArgsJson,
    bool Confirmed = false);

public sealed record ExecuteAiActionResult(
    Guid ActionId,
    string ToolName,
    string AuthorizationResult,
    string ExecutionStatus,
    string? ResultRef,
    string? Message,
    object? Output = null);

// Context data structures for grounding AI operations
public sealed record AiServiceInfo(Guid ServiceId, string Code, string Name, string Description, IReadOnlyList<AiCategoryInfo> Categories);
public sealed record AiCategoryInfo(Guid CategoryId, string Code, string Name);
public sealed record AiDepartmentInfo(Guid DepartmentId, string Code, string Name);
public sealed record AiUserInfo(Guid UserId, string FullName, string Email, Guid? DepartmentId, string RoleName, int ActiveTaskCount);
public sealed record AiWorkflowInfo(Guid WorkflowVersionId, Guid WorkflowId, string Name, int VersionNo);
public sealed record AiSlaInfo(Guid SlaVersionId, Guid SlaProfileId, string Name, int TargetMinutes);

public sealed record AiTenantContextData(
    Guid TenantId,
    IReadOnlyList<AiServiceInfo> Services,
    IReadOnlyList<AiDepartmentInfo> Departments,
    IReadOnlyList<AiUserInfo> Users,
    IReadOnlyList<AiWorkflowInfo> Workflows,
    IReadOnlyList<AiSlaInfo> Slas);

public sealed record AiTaskContextData(
    Guid TaskId,
    Guid TenantId,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid? DepartmentId,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? Deadline,
    int ChecklistItemCount,
    int CompletedChecklistItemCount,
    int ProgressReportCount,
    int LastReportedPercent,
    IReadOnlyList<string> ProgressNotes,
    IReadOnlyList<string> Comments);

public sealed record AiRequestContextData(
    Guid RequestId,
    Guid TenantId,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid ServiceId,
    string ServiceName,
    Guid CategoryId,
    string CategoryName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Comments);
