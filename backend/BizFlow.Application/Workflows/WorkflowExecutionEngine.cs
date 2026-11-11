using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Domain.Workflows;

namespace BizFlow.Application.Workflows;

public sealed record WorkflowStepResolution(
    string NextNodeId,
    string NodeType,
    string Name,
    int? SlaHours,
    string AssignmentMethod,
    Guid? DepartmentId,
    Guid? RoleId,
    Guid? AssignedUserId,
    string? CompletionState,
    bool IsCompleted,
    bool RequireNotes,
    bool RequireAttachment,
    bool RequireDeliverable,
    string? ApprovalCountPolicy,
    bool RejectionReasonRequired,
    string? Description
);

public interface IWorkflowRuntimeEngine
{
    Task<WorkflowStepResolution> StartWorkflowAsync(
        Guid tenantId,
        Guid workflowVersionId,
        JsonElement contextPayload,
        Guid? requesterUserId = null,
        Guid? requesterDepartmentId = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowStepResolution> ResolveNextStepAsync(
        Guid tenantId,
        Guid workflowVersionId,
        string currentNodeId,
        string actionOutcome,
        JsonElement contextPayload,
        Guid? requesterUserId = null,
        Guid? requesterDepartmentId = null,
        CancellationToken cancellationToken = default);

    WorkflowStepResolution ResolveStepFromGraph(
        JsonElement definitionJson,
        string? currentNodeId,
        string? actionOutcome,
        JsonElement contextPayload,
        Guid? requesterUserId = null,
        Guid? requesterDepartmentId = null);
}

public sealed class WorkflowExecutionEngine(IWorkflowLibraryStore store) : IWorkflowRuntimeEngine
{
    public async Task<WorkflowStepResolution> StartWorkflowAsync(
        Guid tenantId,
        Guid workflowVersionId,
        JsonElement contextPayload,
        Guid? requesterUserId = null,
        Guid? requesterDepartmentId = null,
        CancellationToken cancellationToken = default)
    {
        var version = await store.ReadVersionAsync(tenantId, workflowVersionId, cancellationToken);
        if (version is null)
            throw ApplicationFault.NotFound();

        return ResolveStepFromGraph(
            version.Definition,
            currentNodeId: null,
            actionOutcome: null,
            contextPayload: contextPayload,
            requesterUserId: requesterUserId,
            requesterDepartmentId: requesterDepartmentId
        );
    }

    public async Task<WorkflowStepResolution> ResolveNextStepAsync(
        Guid tenantId,
        Guid workflowVersionId,
        string currentNodeId,
        string actionOutcome,
        JsonElement contextPayload,
        Guid? requesterUserId = null,
        Guid? requesterDepartmentId = null,
        CancellationToken cancellationToken = default)
    {
        var version = await store.ReadVersionAsync(tenantId, workflowVersionId, cancellationToken);
        if (version is null)
            throw ApplicationFault.NotFound();

        return ResolveStepFromGraph(
            version.Definition,
            currentNodeId: currentNodeId,
            actionOutcome: actionOutcome,
            contextPayload: contextPayload,
            requesterUserId: requesterUserId,
            requesterDepartmentId: requesterDepartmentId
        );
    }

    public WorkflowStepResolution ResolveStepFromGraph(
        JsonElement definitionJson,
        string? currentNodeId,
        string? actionOutcome,
        JsonElement contextPayload,
        Guid? requesterUserId = null,
        Guid? requesterDepartmentId = null)
    {
        if (definitionJson.ValueKind != JsonValueKind.Object)
            throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.INVALID_DEFINITION", "Sơ đồ quy trình không đúng định dạng JSON.");

        if (!definitionJson.TryGetProperty("nodes", out var nodesProp) || nodesProp.ValueKind != JsonValueKind.Array)
            throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.INVALID_NODES", "Sơ đồ quy trình thiếu danh sách nút (nodes).");

        var edgesProp = definitionJson.TryGetProperty("edges", out var ep) && ep.ValueKind == JsonValueKind.Array
            ? ep
            : default;

        var nodes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        string? startNodeId = null;

        foreach (var node in nodesProp.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object) continue;
            var id = node.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;
            nodes[id] = node;

            var type = node.TryGetProperty("type", out var tProp) ? tProp.GetString() : null;
            if (string.Equals(type, "START", StringComparison.OrdinalIgnoreCase))
            {
                startNodeId = id;
            }
        }

        var edges = new List<RuntimeEdge>();
        if (edgesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var edge in edgesProp.EnumerateArray())
            {
                if (edge.ValueKind != JsonValueKind.Object) continue;
                var source = edge.TryGetProperty("source", out var sProp) ? sProp.GetString() : null;
                var target = edge.TryGetProperty("target", out var tgProp) ? tgProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;

                var label = edge.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
                var outcomeCode = edge.TryGetProperty("outcomeCode", out var ocProp) ? ocProp.GetString() : null;
                var conditionId = edge.TryGetProperty("conditionId", out var cIdProp) ? cIdProp.GetString() : null;
                var isDefault = edge.TryGetProperty("isDefault", out var dProp) && dProp.GetBoolean();

                edges.Add(new RuntimeEdge(source, target, label, outcomeCode, conditionId, isDefault));
            }
        }

        // Determine starting point
        string currentId;
        if (string.IsNullOrWhiteSpace(currentNodeId))
        {
            if (string.IsNullOrWhiteSpace(startNodeId))
                throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.NO_START_NODE", "Sơ đồ quy trình không có sự kiện bắt đầu (START).");
            currentId = startNodeId;
        }
        else
        {
            if (!nodes.ContainsKey(currentNodeId))
                throw new ApplicationFault(FaultKind.Validation, "WORKFLOW.NODE_NOT_FOUND", $"Không tìm thấy bước hiện tại '{currentNodeId}' trong sơ đồ.");
            currentId = currentNodeId;
        }

        // Traverse graph to next operational node or END
        int maxHops = 20;
        int hopCount = 0;

        while (hopCount++ < maxHops)
        {
            var currentNode = nodes[currentId];
            var currentType = currentNode.TryGetProperty("type", out var ctProp) ? ctProp.GetString()?.ToUpperInvariant() : "";

            // Find next target id
            string? nextTargetId = null;

            if (currentType == "DECISION")
            {
                nextTargetId = EvaluateDecisionNode(currentNode, edges, currentId, contextPayload);
            }
            else
            {
                var outgoing = edges.Where(e => string.Equals(e.Source, currentId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (outgoing.Count == 0)
                {
                    if (currentType == "END")
                    {
                        // Terminal step
                        return BuildResolution(currentNode, requesterUserId, requesterDepartmentId);
                    }
                    throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.NO_OUTGOING_PATH", $"Bước '{currentId}' không có đường dẫn chuyển tiếp tiếp theo.");
                }

                // If specific action outcome provided
                if (!string.IsNullOrWhiteSpace(actionOutcome))
                {
                    // 1. Exact match on outcomeCode
                    var matched = outgoing.FirstOrDefault(e =>
                        string.Equals(e.OutcomeCode, actionOutcome, StringComparison.OrdinalIgnoreCase));

                    // 2. Exact match on Label if outcomeCode not matched
                    matched ??= outgoing.FirstOrDefault(e =>
                        string.Equals(e.Label, actionOutcome, StringComparison.OrdinalIgnoreCase));

                    // 3. Fallback to default edge
                    matched ??= outgoing.FirstOrDefault(e => e.IsDefault);

                    // 4. Fallback to DEFAULT outcomeCode or empty
                    matched ??= outgoing.FirstOrDefault(e =>
                        string.IsNullOrWhiteSpace(e.OutcomeCode) || string.Equals(e.OutcomeCode, "DEFAULT", StringComparison.OrdinalIgnoreCase));

                    nextTargetId = (matched ?? outgoing[0]).Target;
                }
                else
                {
                    // No outcome specified (e.g. from START or regular sequential)
                    var matched = outgoing.FirstOrDefault(e => e.IsDefault)
                        ?? outgoing.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.OutcomeCode) || string.Equals(e.OutcomeCode, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                        ?? outgoing[0];

                    nextTargetId = matched.Target;
                }
            }

            if (string.IsNullOrWhiteSpace(nextTargetId) || !nodes.TryGetValue(nextTargetId, out var targetNode))
            {
                throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.INVALID_TARGET", $"Không thể xác định bước kế tiếp từ bước '{currentId}'.");
            }

            var targetType = targetNode.TryGetProperty("type", out var ttProp) ? ttProp.GetString()?.ToUpperInvariant() : "";
            if (targetType == "DECISION")
            {
                // Must evaluate decision next
                currentId = nextTargetId;
                actionOutcome = null; // Outcome has been consumed to enter the decision gate
                continue;
            }

            // Arrived at operational node or END
            return BuildResolution(targetNode, requesterUserId, requesterDepartmentId);
        }

        throw new ApplicationFault(FaultKind.Conflict, "WORKFLOW.MAX_HOPS_EXCEEDED", "Vượt quá số lượt chuyển tiếp tối đa cho phép trên quy trình.");
    }

    private static string? EvaluateDecisionNode(JsonElement decisionNode, List<RuntimeEdge> edges, string decisionNodeId, JsonElement contextPayload)
    {
        var outgoing = edges.Where(e => string.Equals(e.Source, decisionNodeId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (decisionNode.TryGetProperty("conditions", out var condsProp) && condsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var cond in condsProp.EnumerateArray())
            {
                if (cond.ValueKind != JsonValueKind.Object) continue;

                var isDefault = cond.TryGetProperty("isDefault", out var dProp) && dProp.GetBoolean();
                if (isDefault) continue;

                var field = cond.TryGetProperty("field", out var fProp) ? fProp.GetString() : null;
                var op = cond.TryGetProperty("operator", out var opProp) ? opProp.GetString() : null;
                var value = cond.TryGetProperty("value", out var vProp) ? vProp.GetString() : null;
                var targetNodeId = cond.TryGetProperty("targetNodeId", out var tnProp) ? tnProp.GetString() : null;
                var condId = cond.TryGetProperty("id", out var cIdProp) ? cIdProp.GetString() : null;

                if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(op)) continue;

                if (WorkflowConditionEvaluator.Evaluate(field, op, value, contextPayload))
                {
                    if (!string.IsNullOrWhiteSpace(targetNodeId))
                        return targetNodeId;

                    // Match outgoing edge by conditionId
                    if (!string.IsNullOrWhiteSpace(condId))
                    {
                        var edgeMatch = outgoing.FirstOrDefault(e => string.Equals(e.ConditionId, condId, StringComparison.OrdinalIgnoreCase));
                        if (edgeMatch is not null) return edgeMatch.Target;
                    }
                }
            }

            // Check default condition
            foreach (var cond in condsProp.EnumerateArray())
            {
                if (cond.ValueKind != JsonValueKind.Object) continue;
                var isDefault = cond.TryGetProperty("isDefault", out var dProp) && dProp.GetBoolean();
                if (isDefault)
                {
                    var targetNodeId = cond.TryGetProperty("targetNodeId", out var tnProp) ? tnProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(targetNodeId))
                        return targetNodeId;

                    var condId = cond.TryGetProperty("id", out var cIdProp) ? cIdProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(condId))
                    {
                        var edgeMatch = outgoing.FirstOrDefault(e => string.Equals(e.ConditionId, condId, StringComparison.OrdinalIgnoreCase));
                        if (edgeMatch is not null) return edgeMatch.Target;
                    }
                }
            }
        }

        // Fallback to default edge or first outgoing edge
        var defaultEdge = outgoing.FirstOrDefault(e => e.IsDefault)
            ?? outgoing.FirstOrDefault(e => string.Equals(e.OutcomeCode, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            ?? outgoing.FirstOrDefault();

        return defaultEdge?.Target;
    }

    private static string? GetContextFieldValue(JsonElement context, string fieldKey)
    {
        if (context.ValueKind != JsonValueKind.Object) return null;

        foreach (var prop in context.EnumerateObject())
        {
            if (string.Equals(prop.Name, fieldKey, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }

        return null;
    }

    private static WorkflowStepResolution BuildResolution(JsonElement node, Guid? requesterUserId, Guid? requesterDepartmentId)
    {
        var id = node.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        var type = node.TryGetProperty("type", out var tProp) ? tProp.GetString()?.ToUpperInvariant() ?? "" : "";
        var name = node.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";

        int? slaHours = node.TryGetProperty("slaHours", out var slaProp) && slaProp.TryGetInt32(out var sla) ? sla : null;

        var assignmentMethod = node.TryGetProperty("assignmentMethod", out var amProp)
            ? amProp.GetString()?.ToUpperInvariant()
            : null;

        if (string.IsNullOrWhiteSpace(assignmentMethod))
        {
            assignmentMethod = type switch
            {
                "DEPARTMENT" => "DEPARTMENT_QUEUE",
                "APPROVAL" => "BUSINESS_ROLE",
                "CONFIRMATION" => "REQUESTER",
                _ => "DEPARTMENT_QUEUE"
            };
        }

        Guid? deptId = null;
        if (node.TryGetProperty("departmentId", out var dIdProp) && Guid.TryParse(dIdProp.GetString(), out var parsedDept))
            deptId = parsedDept;

        Guid? roleId = null;
        if (node.TryGetProperty("roleId", out var rIdProp) && Guid.TryParse(rIdProp.GetString(), out var parsedRole))
            roleId = parsedRole;

        Guid? specificUserId = null;
        if (node.TryGetProperty("specificUserId", out var uIdProp) && Guid.TryParse(uIdProp.GetString(), out var parsedUser))
            specificUserId = parsedUser;

        // Resolve Requester assignment
        if (assignmentMethod == "REQUESTER" && requesterUserId.HasValue)
        {
            specificUserId = requesterUserId.Value;
            deptId ??= requesterDepartmentId;
        }

        var completionState = node.TryGetProperty("completionState", out var csProp) ? csProp.GetString() : null;
        var isCompleted = type == "END";

        var requireNotes = node.TryGetProperty("requireNotes", out var rnProp) && rnProp.GetBoolean();
        var requireAttachment = node.TryGetProperty("requireAttachment", out var raProp) && raProp.GetBoolean();
        var requireDeliverable = node.TryGetProperty("requireDeliverable", out var rdProp) && rdProp.GetBoolean();

        var approvalPolicy = node.TryGetProperty("approvalCountPolicy", out var apProp) ? apProp.GetString() : null;
        var rejectionReasonRequired = !node.TryGetProperty("rejectionReasonRequired", out var rrProp) || rrProp.GetBoolean();

        var description = node.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

        return new WorkflowStepResolution(
            NextNodeId: id,
            NodeType: type,
            Name: name,
            SlaHours: slaHours,
            AssignmentMethod: assignmentMethod,
            DepartmentId: deptId,
            RoleId: roleId,
            AssignedUserId: specificUserId,
            CompletionState: completionState,
            IsCompleted: isCompleted,
            RequireNotes: requireNotes,
            RequireAttachment: requireAttachment,
            RequireDeliverable: requireDeliverable,
            ApprovalCountPolicy: approvalPolicy,
            RejectionReasonRequired: rejectionReasonRequired,
            Description: description
        );
    }

    private sealed record RuntimeEdge(
        string Source,
        string Target,
        string? Label,
        string? OutcomeCode,
        string? ConditionId,
        bool IsDefault
    );
}
