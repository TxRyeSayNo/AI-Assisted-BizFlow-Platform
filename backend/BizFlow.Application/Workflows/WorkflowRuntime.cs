using System.Text.Json;
using BizFlow.Application.Common;

namespace BizFlow.Application.Workflows;

public sealed record WorkflowDecisionInput(int Revision, string Outcome, string? Note = null);
public sealed record WorkflowDecisionView(Guid ActorId, string ActorName, string Outcome, string? Note, DateTimeOffset DecidedAt,
    Guid PrincipalId, string PrincipalName);
public sealed record WorkflowDelegationInput(int Revision, Guid PrincipalId, Guid DelegateId, DateTimeOffset ExpiresAt, string Reason);
public sealed record WorkflowRevokeDelegationInput(int Revision);
public sealed record WorkflowSlaInput(int Revision, string? Reason = null);
public sealed record WorkflowSlaPauseView(Guid Id, string PausedBy, string Reason, DateTimeOffset PausedAt, DateTimeOffset DueAtBefore,
    DateTimeOffset? ResumedAt, string? ResumedBy, DateTimeOffset? DueAtAfter);
public sealed record WorkflowCandidateView(Guid UserId, string Name, bool Decided);
public sealed record WorkflowDelegationView(Guid Id, Guid PrincipalId, string PrincipalName, Guid DelegateId, string DelegateName,
    string CreatedBy, string Reason, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, bool Active);
public sealed record WorkflowExecutionView(Guid Id, string NodeId, string Name, string NodeType, string Status,
    string Policy, DateTimeOffset StartedAt, DateTimeOffset? DueAt, DateTimeOffset? CompletedAt, string? Outcome,
    bool CanAct, IReadOnlyList<string> Candidates, IReadOnlyList<WorkflowDecisionView> Decisions,
    IReadOnlyList<WorkflowCandidateView> CandidateDetails, IReadOnlyList<WorkflowDelegationView> Delegations,
    DateTimeOffset? PausedAt, bool CanManageSla, IReadOnlyList<WorkflowSlaPauseView> SlaPauses, WorkflowAgentRunView? Agent = null);
public sealed record WorkflowRuntimeView(Guid Id, Guid RequestId, Guid VersionId, string Name, int VersionNo,
    string Status, int Revision, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, JsonElement FormData,
    IReadOnlyList<WorkflowExecutionView> Steps, IReadOnlyList<WorkflowFormField> FormFields, bool CanDelegate);
public sealed record WorkflowInboxItem(Guid RequestId, Guid StepId, string Title, string StepName,
    string NodeType, DateTimeOffset StartedAt, DateTimeOffset? DueAt, int Revision, DateTimeOffset? PausedAt);
public sealed record WorkflowInboxPage(IReadOnlyList<WorkflowInboxItem> Items, int Page, int PageSize, long Total);
public sealed record WorkflowStepMetric(string Workflow, int VersionNo, string Step, int Waiting, int Completed,
    int Breached, double? AverageActualMinutes, double? P90ActualMinutes);
public sealed record WorkflowMetrics(DateTimeOffset From, DateTimeOffset AsOf, IReadOnlyList<WorkflowStepMetric> Steps);
public sealed record WorkflowFormField(string Key, string Label, string Type, bool Required = false,
    decimal? Min = null, decimal? Max = null, string[]? Options = null);

public interface IWorkflowRuntimeStore
{
    Task<IReadOnlyList<WorkflowCalendarOption>> CalendarsAsync(CancellationToken cancellationToken);
    Task<WorkflowRuntimeView?> GetAsync(Guid requestId, CancellationToken cancellationToken);
    Task<WorkflowRuntimeView> StartAsync(Guid requestId, CancellationToken cancellationToken);
    Task<WorkflowRuntimeView> DecideAsync(Guid requestId, Guid stepId, WorkflowDecisionInput input, CancellationToken cancellationToken);
    Task<WorkflowInboxPage> InboxAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowFormField>> FormAsync(Guid serviceId, CancellationToken cancellationToken);
    Task<WorkflowMetrics> MetricsAsync(int days, CancellationToken cancellationToken);
    Task<WorkflowRuntimeView> DelegateAsync(Guid requestId, Guid stepId, WorkflowDelegationInput input, CancellationToken cancellationToken);
    Task<WorkflowRuntimeView> RevokeDelegationAsync(Guid requestId, Guid delegationId, int revision, CancellationToken cancellationToken);
    Task<WorkflowRuntimeView> PauseSlaAsync(Guid requestId, Guid stepId, WorkflowSlaInput input, bool resume, CancellationToken cancellationToken);
    Task<WorkflowRuntimeView> RetryAgentAsync(Guid requestId, Guid stepId, WorkflowAgentRetryInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowAgentAttemptView>> AgentHistoryAsync(Guid requestId, Guid stepId, CancellationToken cancellationToken);
}
public sealed record WorkflowCalendarOption(Guid Id, string Name, string TimeZone);

/// <summary>Validates structured service inputs against the pinned workflow definition.</summary>
public static class WorkflowForm
{
    public static IReadOnlyList<WorkflowFormField> Fields(JsonElement definition)
    {
        if (!definition.TryGetProperty("formFields", out var fields)) return [];
        if (fields.ValueKind != JsonValueKind.Array || fields.GetArrayLength() > 50) throw Invalid();
        var result = new List<WorkflowFormField>();
        foreach (var item in fields.EnumerateArray())
        {
            WorkflowFormField? field;
            try { field = item.Deserialize<WorkflowFormField>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { throw Invalid(); }
            if (field is null || string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > 50 ||
                field.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') ||
                new[] { "priority", "requesterId", "serviceId", "categoryId", "title" }.Contains(field.Key, StringComparer.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 200 ||
                field.Type is not ("text" or "number" or "date" or "select") ||
                field.Min > field.Max || field.Options?.Length > 100 ||
                field.Type == "select" && (field.Options is not { Length: > 0 } || field.Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Length > 200)) ||
                result.Any(f => f.Key == field.Key)) throw Invalid();
            result.Add(field);
        }
        return result;
    }

    public static void Validate(JsonElement definition, JsonElement values)
    {
        if (values.ValueKind != JsonValueKind.Object || values.GetRawText().Length > 64000) throw Invalid();
        var fields = Fields(definition);
        var names = new HashSet<string>();
        foreach (var property in values.EnumerateObject())
            if (!names.Add(property.Name) || !fields.Any(f => f.Key == property.Name)) throw Invalid();
        foreach (var field in fields)
        {
            var present = values.TryGetProperty(field.Key, out var value) && value.ValueKind != JsonValueKind.Null &&
                !(value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));
            if (!present) { if (field.Required) throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.FIELD_REQUIRED", $"Vui lòng nhập {field.Label}."); continue; }
            var valid = field.Type switch
            {
                "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) &&
                    (!field.Min.HasValue || n >= field.Min) && (!field.Max.HasValue || n <= field.Max),
                "date" => value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _),
                "select" => value.ValueKind == JsonValueKind.String && field.Options!.Contains(value.GetString()),
                _ => value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 4000
            };
            if (!valid) throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.FIELD_INVALID", $"Giá trị {field.Label} không hợp lệ.");
        }
    }
    private static ApplicationFault Invalid() => new(FaultKind.Validation, "WORKFLOW.FORM_INVALID", "Biểu mẫu hoặc dữ liệu biểu mẫu không hợp lệ.");
}
