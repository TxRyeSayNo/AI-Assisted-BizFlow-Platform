using System.Text.Json;
using System.Text.Json.Serialization;
using BizFlow.Application.Common;

namespace BizFlow.Application.Workflows;

public sealed record WorkflowAgentPolicy(string Mode, string Instructions, string[] InputFields,
    bool IncludeDescription = false, bool IncludePreviousResults = false);
public sealed record WorkflowAgentEvidence(string Source, string Quote);
public sealed record WorkflowAgentResult(string Summary, string Deliverable, bool NeedsHumanReview,
    string[] Findings, WorkflowAgentEvidence[] Evidence);
public sealed record WorkflowAgentRunView(Guid Id, string Status, int Attempts, DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt, string? ErrorCode, string? Model, WorkflowAgentResult? Result);
public sealed record WorkflowAgentRetryInput(int Revision, string Reason);
public sealed record WorkflowAgentAttemptView(int Attempt, DateTimeOffset StartedAt, DateTimeOffset CompletedAt,
    string? Model, string? ErrorCode, JsonElement Trace);

/// <summary>Published opt-in capability. An AI result cannot approve, reject or modify business data.</summary>
public static class WorkflowAgentContract
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static WorkflowAgentPolicy? Policy(JsonElement node)
    {
        if (!node.TryGetProperty("agent", out var value) || value.ValueKind == JsonValueKind.Null) return null;
        try
        {
            var policy = value.Deserialize<WorkflowAgentPolicy>(Json);
            if (policy is null || policy.Mode is not ("REVIEW" or "AUTO") ||
                string.IsNullOrWhiteSpace(policy.Instructions) || policy.Instructions.Length > 4000 ||
                policy.InputFields is null || policy.InputFields.Length > 20 ||
                policy.InputFields.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 50) ||
                policy.InputFields.Distinct(StringComparer.Ordinal).Count() != policy.InputFields.Length ||
                node.GetProperty("type").GetString() != "DEPARTMENT" ||
                node.TryGetProperty("approvalCountPolicy", out var count) && count.GetString() is not (null or "SINGLE") ||
                policy.Mode == "AUTO" && node.TryGetProperty("requireAttachment", out var attachment) && attachment.ValueKind == JsonValueKind.True)
                throw Invalid();
            return policy;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { throw Invalid(); }
    }

    public static void Validate(JsonElement graph, bool requestWorkflow = true)
    {
        if (!graph.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return;
        var fields = WorkflowForm.Fields(graph);
        foreach (var node in nodes.EnumerateArray())
        {
            if (Policy(node) is not { } policy) continue;
            if (!requestWorkflow || policy.InputFields.Any(key => !fields.Any(f => f.Key == key))) throw Invalid();
        }
    }

    public static WorkflowAgentResult Result(JsonElement value, IReadOnlyDictionary<string, string> sources)
    {
        WorkflowAgentResult? result;
        try { result = value.Deserialize<WorkflowAgentResult>(Json); }
        catch (JsonException) { throw InvalidResult(); }
        if (result is null || string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Length > 2000 ||
            string.IsNullOrWhiteSpace(result.Deliverable) || result.Deliverable.Length > 12000 ||
            result.Findings is null || result.Findings.Length > 20 || result.Findings.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 1000) ||
            result.Evidence is null || result.Evidence.Length is < 1 or > 20 ||
            result.Evidence.Any(e => e is null || string.IsNullOrWhiteSpace(e.Source) || string.IsNullOrWhiteSpace(e.Quote) || e.Quote.Length > 500 ||
                !sources.TryGetValue(e.Source, out var source) || !source.Contains(e.Quote, StringComparison.Ordinal))) throw InvalidResult();
        return result;
    }

    private static ApplicationFault Invalid() => new(FaultKind.Validation, "WORKFLOW.AGENT_POLICY_INVALID",
        "AI chỉ hỗ trợ bước xử lý yêu cầu với chính sách SINGLE, chỉ đọc trường biểu mẫu đã chọn; chế độ tự động không được bỏ qua tệp bắt buộc.");
    private static ApplicationFault InvalidResult() => new(FaultKind.Validation, "WORKFLOW.AGENT_RESULT_INVALID",
        "Kết quả AI thiếu nội dung hoặc bằng chứng không khớp nguồn dữ liệu được phép.");
}
