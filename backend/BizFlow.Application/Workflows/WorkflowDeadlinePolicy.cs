using System.Text.Json;
using BizFlow.Application.Common;

namespace BizFlow.Application.Workflows;

public static class WorkflowDeadlinePolicy
{
    public static int EscalationHours(JsonElement definition)
    {
        if (!definition.TryGetProperty("escalationAfterHours",out var value)) return 24;
        if (value.ValueKind!=JsonValueKind.Number || !value.TryGetInt32(out var hours) || hours is <1 or >720)
            throw new ApplicationFault(FaultKind.Validation,"WORKFLOW.ESCALATION_INVALID","Ngưỡng báo quản lý phải từ 1 đến 720 giờ quá hạn.");
        return hours;
    }
}
