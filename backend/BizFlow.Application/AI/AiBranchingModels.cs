using System;
using System.Collections.Generic;

namespace BizFlow.Application.AI;

public sealed record BranchPlanCommand(
    string SourceType, // "Request" | "Task" | "FreeText"
    Guid? SourceId,
    string Title,
    string Description,
    int? MaxBranches = 5,
    bool EnforceCrossDepartment = true);

public sealed record BranchPlanNode(
    int BranchIndex,
    string BranchType, // "Request" | "Task"
    string Title,
    string Description,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ServiceId,
    string? ServiceName,
    Guid? CategoryId,
    string? CategoryName,
    string Priority, // "Low" | "Medium" | "High" | "Critical"
    int SuggestedDurationDays,
    int ExecutionPhase, // 1 = Immediate/Parallel, 2 = Dependent, 3 = Final/Acceptance
    IReadOnlyList<int> DependsOnBranchIndexes,
    IReadOnlyList<string> Checklist,
    string Reasoning);

public sealed record BranchingPlanResult(
    Guid PlanId,
    string SourceType,
    Guid? SourceId,
    string OverallStrategy,
    int TotalBranches,
    int EstimatedTotalDays,
    IReadOnlyList<BranchPlanNode> Branches,
    decimal Confidence,
    string ConfirmationToken);

public sealed record ExecuteBranchingPlanCommand(
    Guid PlanId,
    string SourceType,
    Guid? SourceId,
    IReadOnlyList<BranchPlanNode> Branches,
    string ConfirmationToken);

public sealed record CreatedBranchRecord(
    int BranchIndex,
    string RecordType, // "Request" | "Task"
    Guid RecordId,
    string Title,
    string Status,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ServiceId,
    Guid? CategoryId);

public sealed record ExecuteBranchingPlanResult(
    Guid PlanId,
    Guid? SourceId,
    int TotalCreated,
    IReadOnlyList<CreatedBranchRecord> CreatedRecords,
    DateTimeOffset ExecutedAt);

public interface IAiBranchingStore
{
    Task<IReadOnlyList<CreatedBranchRecord>> PersistBranchingPlanAsync(
        Guid tenantId,
        Guid userId,
        Guid planId,
        string sourceType,
        Guid? sourceId,
        IReadOnlyList<BranchPlanNode> branches,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IAiBranchingService
{
    Task<BranchingPlanResult> PlanBranchingAsync(
        BranchPlanCommand command,
        CancellationToken cancellationToken = default);

    Task<ExecuteBranchingPlanResult> ExecuteBranchingPlanAsync(
        ExecuteBranchingPlanCommand command,
        CancellationToken cancellationToken = default);
}
