using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.AI;

public sealed record DocumentSummarizeCommand(
    string? RawText,
    Guid? AttachmentId,
    string Mode = "COMPREHENSIVE", // "EXECUTIVE" | "ACTION_ITEMS" | "RISKS_COMPLIANCE" | "COMPREHENSIVE"
    string Language = "vi",        // "vi" | "en" | "ja"
    int MaxKeyPoints = 5);

public sealed record ExtractedActionItem(
    int OrderIndex,
    string Title,
    string Description,
    string? SuggestedDepartment,
    string? SuggestedPriority, // "Low" | "Medium" | "High" | "Critical"
    int? SuggestedDurationDays);

public sealed record DocumentSummarizeResult(
    string SourceTitle,
    string SourceType, // "RawText" | "Attachment"
    string Mode,
    string ExecutiveSummary,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<ExtractedActionItem> ActionItems,
    IReadOnlyList<string> RisksAndCompliance,
    int WordCount,
    decimal Confidence,
    int? TokensUsed,
    DateTimeOffset SummarizedAt);

public sealed record CreateTasksFromSummaryCommand(
    string SourceTitle,
    string? SourceReference,
    IReadOnlyList<ExtractedActionItem> Items);

public sealed record CreatedSummaryTaskResult(
    Guid TaskId,
    string Title,
    string Priority,
    Guid? DepartmentId,
    string Status);

public sealed record CreateTasksFromSummaryResponse(
    int TotalCreated,
    IReadOnlyList<CreatedSummaryTaskResult> CreatedTasks,
    DateTimeOffset CreatedAt);

public interface IAiDocumentSummarizerStore
{
    Task<(string FileName, string? Content)?> GetAttachmentTextAsync(
        Guid tenantId,
        Guid attachmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CreatedSummaryTaskResult>> PersistSummaryTasksAsync(
        Guid tenantId,
        Guid userId,
        string sourceTitle,
        IReadOnlyList<ExtractedActionItem> items,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IAiDocumentSummarizerService
{
    Task<DocumentSummarizeResult> SummarizeDocumentAsync(
        DocumentSummarizeCommand command,
        CancellationToken cancellationToken = default);

    Task<CreateTasksFromSummaryResponse> CreateTasksFromSummaryAsync(
        CreateTasksFromSummaryCommand command,
        CancellationToken cancellationToken = default);
}
