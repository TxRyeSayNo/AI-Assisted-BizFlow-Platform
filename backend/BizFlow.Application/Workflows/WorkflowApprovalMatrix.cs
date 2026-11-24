using System.Text.Json;
using System.Text.Json.Nodes;
using BizFlow.Application.Common;

namespace BizFlow.Application.Workflows;

public sealed record WorkflowApprovalTier(decimal? UpperLimit, Guid RoleId, string Policy = "SINGLE");
public sealed record WorkflowApprovalMatrixConfig(string AmountField, WorkflowApprovalTier[] Tiers);

/// <summary>Inclusive upper limits, ordered ascending, with an explicit unlimited final tier.</summary>
public static class WorkflowApprovalMatrix
{
    public static void Validate(JsonElement definition, IReadOnlySet<Guid>? validRoles = null)
    {
        if (!definition.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array) return;
        var fields = WorkflowForm.Fields(definition);
        foreach (var node in nodes.EnumerateArray())
        {
            var configured = node.TryGetProperty("assignmentMethod", out var method) && method.ValueKind == JsonValueKind.String && method.GetString() == "MATRIX";
            if (!node.TryGetProperty("approvalMatrix", out var matrix) || matrix.ValueKind == JsonValueKind.Null)
            { if (configured) throw Invalid("Bước duyệt chưa có ma trận hạn mức."); continue; }
            if (!configured || !node.TryGetProperty("type", out var type) || type.GetString() != "APPROVAL") throw Invalid("Ma trận hạn mức chỉ dùng cho bước phê duyệt chọn phương thức MATRIX.");
            var config = Parse(matrix);
            if (!fields.Any(f => f.Key == config.AmountField && f.Type == "number" && f.Required))
                throw Invalid("Trường hạn mức phải là một trường số bắt buộc trong biểu mẫu.");
            decimal previous = -1;
            for (var i = 0; i < config.Tiers.Length; i++)
            {
                var tier = config.Tiers[i];
                if (tier.RoleId == Guid.Empty || validRoles is not null && !validRoles.Contains(tier.RoleId) || tier.Policy is not ("SINGLE" or "ANY" or "ALL"))
                    throw Invalid("Mỗi mức cần vai trò đang hoạt động trong doanh nghiệp và chính sách duyệt hợp lệ.");
                if (i == config.Tiers.Length - 1)
                { if (tier.UpperLimit is not null) throw Invalid("Mức cuối phải không giới hạn để không bỏ sót yêu cầu."); }
                else
                {
                    if (tier.UpperLimit is not { } upper || upper < 0 || upper <= previous) throw Invalid("Các hạn mức phải không âm, tăng dần và không trùng nhau.");
                    previous = upper;
                }
            }
        }
    }
    private static WorkflowApprovalMatrixConfig Parse(JsonElement matrix)
    {
        WorkflowApprovalMatrixConfig? config;
        try { config = matrix.Deserialize<WorkflowApprovalMatrixConfig>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { throw Invalid("Cấu hình ma trận không hợp lệ."); }
        if (config is null || string.IsNullOrWhiteSpace(config.AmountField) || config.Tiers is not {Length: > 0 and <= 20} || config.Tiers.Any(t => t is null))
            throw Invalid("Ma trận cần từ 1 đến 20 mức và trường giá trị.");
        return config;
    }
    public static JsonElement Resolve(JsonElement node, JsonElement payload)
    {
        if (!node.TryGetProperty("assignmentMethod", out var method) || method.ValueKind != JsonValueKind.String || method.GetString() != "MATRIX") return node;
        if (!node.TryGetProperty("approvalMatrix", out var matrix)) throw Invalid("Thiếu ma trận hạn mức.");
        var config = Parse(matrix);
        if (!payload.TryGetProperty(config.AmountField, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var amount) || amount < 0)
            throw Invalid("Giá trị dùng để xác định hạn mức phải là số không âm.");
        var tier = config.Tiers.FirstOrDefault(t => t.UpperLimit is null || amount <= t.UpperLimit) ?? throw Invalid("Không tìm được mức phê duyệt.");
        var result = JsonNode.Parse(node.GetRawText())!.AsObject();
        result["assignmentMethod"] = "BUSINESS_ROLE"; result["roleId"] = tier.RoleId.ToString(); result["approvalCountPolicy"] = tier.Policy;
        return JsonSerializer.SerializeToElement(result);
    }
    private static ApplicationFault Invalid(string message) => new(FaultKind.Validation,"WORKFLOW.MATRIX_INVALID",message);
}
