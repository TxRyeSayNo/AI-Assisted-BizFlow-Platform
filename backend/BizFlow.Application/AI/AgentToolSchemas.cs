using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace BizFlow.Application.AI;

// Tool input contracts, not a router based on words in the user's message.
public static class AgentToolSchemas
{
    public static JsonElement? For(string toolName)
    {
        var fields = toolName switch
        {
            "send_meeting_notification" => """
                "topic":{"type":"string","title":"Nội dung cuộc họp"},
                "startsAt":{"type":"string","format":"date-time","title":"Thời gian họp"},
                "location":{"type":"string","title":"Địa điểm hoặc đường dẫn họp"},
                "preparation":{"type":"string","title":"Nội dung cần chuẩn bị"},
                "recipientScope":{"type":"string","enum":["ALL_COMPANY","DEPARTMENTS"],"title":"Phạm vi người nhận"},
                "departmentIds":{"type":"array","items":{"type":"string","format":"uuid"},"title":"Các phòng ban nhận thông báo"}
                """,
            "send_department_notification" => """
                "title":{"type":"string","maxLength":200,"title":"Tiêu đề thông báo"},
                "content":{"type":"string","maxLength":10000,"title":"Nội dung thông báo"},
                "recipientScope":{"type":"string","enum":["ALL_COMPANY","DEPARTMENTS"],"title":"Phạm vi người nhận"},
                "departmentIds":{"type":"array","items":{"type":"string","format":"uuid"},"title":"Các phòng ban nhận thông báo"}
                """,
            "create_task_draft" => """
                "title":{"type":"string","maxLength":300,"title":"Tiêu đề công việc"},"description":{"type":"string"},"priority":{"type":"string","enum":["LOW","MEDIUM","HIGH"]}
                """,
            "assign_task" => """
                "taskId":{"type":"string","format":"uuid","title":"Công việc cần giao"},"userId":{"type":"string","format":"uuid","title":"Người nhận công việc"}
                """,
            "add_comment" => """
                "objectId":{"type":"string","format":"uuid","title":"Công việc hoặc yêu cầu cần bình luận"},"objectType":{"type":"string","enum":["Task","Request"],"title":"Loại đối tượng"},"content":{"type":"string","title":"Nội dung bình luận"}
                """,
            "route_request" => """
                "requestId":{"type":"string","format":"uuid","title":"Yêu cầu cần điều phối"},"toDepartmentId":{"type":"string","format":"uuid"},"toUserId":{"type":"string","format":"uuid"},"reason":{"type":"string"}
                """,
            _ => null
        };
        if (fields == null) return null;
        var required = toolName switch
        {
            "send_meeting_notification" => new[] { "topic", "startsAt", "location", "recipientScope" },
            "send_department_notification" => ["title", "content", "recipientScope"],
            "create_task_draft" => ["title"],
            "assign_task" => ["taskId", "userId"],
            "add_comment" => ["objectId", "objectType", "content"],
            "route_request" => ["requestId"],
            _ => []
        };
        using var doc = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{" + fields + "},\"required\":" + JsonSerializer.Serialize(required) + "}");
        return doc.RootElement.Clone();
    }

    public static Dictionary<string, string> MissingOrInvalid(string toolName, JsonObject args, DateTimeOffset now)
    {
        var issues = new Dictionary<string, string>();
        var schema = For(toolName);
        if (schema == null) return issues;
        var properties = schema.Value.GetProperty("properties");
        foreach (var field in schema.Value.GetProperty("required").EnumerateArray())
        {
            var name = field.GetString()!;
            if (!args.TryGetPropertyValue(name, out var value) || value == null || value.ToJsonString() == "\"\"")
                issues[name] = Label(properties.GetProperty(name), name);
        }
        foreach (var field in properties.EnumerateObject())
        {
            if (!args.TryGetPropertyValue(field.Name, out var value) || value == null) continue;
            var rule = field.Value;
            var valid = true;
            if (rule.GetProperty("type").GetString() == "string")
            {
                valid = value is JsonValue v && v.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text);
                if (valid)
                {
                    var textValue = value.GetValue<string>();
                    if (rule.TryGetProperty("maxLength", out var max)) valid = textValue.Length <= max.GetInt32();
                    if (rule.TryGetProperty("enum", out var values)) valid &= values.EnumerateArray().Any(e => e.GetString() == textValue);
                    if (rule.TryGetProperty("format", out var format))
                    {
                        if (format.GetString() == "uuid") valid &= Guid.TryParse(textValue, out var id) && id != Guid.Empty;
                        if (format.GetString() == "date-time")
                            valid &= textValue.Contains('T') && (textValue.EndsWith('Z') || textValue.LastIndexOf('+') > 9 || textValue.LastIndexOf('-') > 9)
                                && DateTimeOffset.TryParse(textValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant) && instant > now;
                    }
                }
            }
            else if (rule.GetProperty("type").GetString() == "array")
                valid = value is JsonArray array && array.All(item => item is JsonValue v && v.TryGetValue<string>(out var text) && Guid.TryParse(text, out var id) && id != Guid.Empty);
            if (!valid) issues[field.Name] = Label(rule, field.Name);
        }
        if (toolName is "send_meeting_notification" or "send_department_notification" &&
            args["recipientScope"]?.ToString() == "DEPARTMENTS" && (args["departmentIds"] is not JsonArray ids || ids.Count == 0))
            issues["departmentIds"] = "Các phòng ban nhận thông báo (tra danh mục để lấy mã chính xác)";
        if (toolName == "send_meeting_notification" && args["startsAt"] != null && string.IsNullOrWhiteSpace(args["startsAtEvidence"]?.ToString()))
            issues["startsAt"] = "Ngày và giờ bắt đầu họp do bạn xác định";
        if (toolName == "route_request" && args["toDepartmentId"] == null && args["toUserId"] == null)
            issues["toDepartmentId"] = "Phòng ban hoặc người nhận yêu cầu";
        return issues;
    }

    private static string Label(JsonElement rule, string fallback) => rule.TryGetProperty("title", out var title) ? title.GetString()! : fallback;
}
