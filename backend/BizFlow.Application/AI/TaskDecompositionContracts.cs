using System.ComponentModel.DataAnnotations;

namespace BizFlow.Application.AI;

public sealed record TaskDecompositionPreviewCommand(
    [Required, StringLength(4000, MinimumLength = 3)] string Prompt);

public sealed record TaskDecompositionProposalDto(
    string Title,
    string Description,
    string Type, // "SupportRequest" | "Assignment"
    Guid? DepartmentId,
    string? DepartmentName,
    string Priority, // "LOW" | "MEDIUM" | "HIGH"
    IReadOnlyList<string> ChecklistItems,
    string? Reasoning);

public sealed record TaskDecompositionPreviewResult(
    string OriginalPrompt,
    IReadOnlyList<TaskDecompositionProposalDto> Proposals);

public sealed record ConfirmTaskDecompositionItem(
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    string? Description,
    [Required] string Type, // "SupportRequest" | "Assignment"
    Guid? DepartmentId,
    string? Priority,
    IReadOnlyList<string>? ChecklistItems);

public sealed record ConfirmTaskDecompositionCommand(
    [Required] IReadOnlyList<ConfirmTaskDecompositionItem> Items);

public sealed record CreatedDecompositionItemDto(
    Guid Id,
    string Type,
    string Title,
    string Status,
    Guid? DepartmentId);

public sealed record ConfirmTaskDecompositionResponse(
    int TotalCreated,
    IReadOnlyList<CreatedDecompositionItemDto> Items);

public interface IAiTaskDecompositionService
{
    Task<TaskDecompositionPreviewResult> PreviewAsync(
        TaskDecompositionPreviewCommand command,
        CancellationToken cancellationToken = default);

    Task<ConfirmTaskDecompositionResponse> ConfirmAsync(
        ConfirmTaskDecompositionCommand command,
        CancellationToken cancellationToken = default);
}

public interface ITaskDecompositionStore
{
    Task<IReadOnlyList<CreatedDecompositionItemDto>> PersistDecompositionBatchAsync(
        Guid tenantId,
        Guid actorId,
        IReadOnlyList<ConfirmTaskDecompositionItem> items,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
