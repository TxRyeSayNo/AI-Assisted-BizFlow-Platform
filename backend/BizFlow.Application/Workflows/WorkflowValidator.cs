using System.Text.Json;
using System.Text.RegularExpressions;
using BizFlow.Domain.Workflows;

namespace BizFlow.Application.Workflows;

public static class WorkflowValidator
{
    private static readonly HashSet<string> ValidTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MANUAL", "REQUEST_CREATED", "TASK_CREATED", "REQUEST_SUBMITTED"
    };

    private static readonly HashSet<string> ValidEndStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMPLETED", "REJECTED", "CANCELLED", "FAILED"
    };

    public static WorkflowValidationResult Validate(
        string definitionJson,
        WorkflowBusinessType businessType,
        IReadOnlySet<Guid> validDepartmentIds,
        IReadOnlySet<Guid> validRoleIds) =>
        Validate(definitionJson, businessType, validDepartmentIds, validRoleIds, null);

    public static WorkflowValidationResult Validate(
        string definitionJson,
        WorkflowBusinessType businessType,
        IReadOnlySet<Guid> validDepartmentIds,
        IReadOnlySet<Guid> validRoleIds,
        IReadOnlySet<Guid>? validUserIds)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(definitionJson) || definitionJson.Trim() == "{}")
        {
            errors.Add("Quy trình chưa có cấu hình sơ đồ các bước xử lý.");
            return new(false, errors, warnings);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(definitionJson);
        }
        catch (JsonException)
        {
            errors.Add("Dữ liệu định nghĩa sơ đồ không phải là JSON hợp lệ.");
            return new(false, errors, warnings);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("Cấu hình sơ đồ quy trình phải là một JSON object.");
                return new(false, errors, warnings);
            }

            if (!root.TryGetProperty("nodes", out var nodesElement) || nodesElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add("Thiếu danh sách các nút (nodes) trong cấu hình quy trình.");
                return new(false, errors, warnings);
            }

            var nodeMap = new Dictionary<string, (string Type, string Name, Guid? DeptId, Guid? RoleId, Guid? UserId, string? AssignmentMethod, JsonElement Raw)>(StringComparer.OrdinalIgnoreCase);
            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int startCount = 0;
            int endCount = 0;

            foreach (var node in nodesElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object) continue;

                var id = node.TryGetProperty("id", out var idProp) ? idProp.GetString()?.Trim() : null;
                var type = node.TryGetProperty("type", out var typeProp) ? typeProp.GetString()?.Trim().ToUpperInvariant() : null;
                var name = node.TryGetProperty("name", out var nameProp) ? nameProp.GetString()?.Trim() : null;

                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add("Phát hiện một nút thiếu mã định danh (id).");
                    continue;
                }

                if (!nodeIds.Add(id))
                {
                    errors.Add($"Trùng lặp mã định danh nút: '{id}'.");
                }

                if (string.IsNullOrWhiteSpace(type))
                {
                    errors.Add($"Nút '{id}' thiếu loại thành phần (type).");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = type;
                    warnings.Add($"Nút '{id}' chưa được đặt tên hiển thị rõ ràng.");
                }

                Guid? deptId = null;
                if (node.TryGetProperty("departmentId", out var deptProp) &&
                    Guid.TryParse(deptProp.GetString(), out var parsedDeptId))
                {
                    deptId = parsedDeptId;
                }

                Guid? roleId = null;
                if (node.TryGetProperty("roleId", out var roleProp) &&
                    Guid.TryParse(roleProp.GetString(), out var parsedRoleId))
                {
                    roleId = parsedRoleId;
                }

                Guid? userId = null;
                if (node.TryGetProperty("specificUserId", out var userProp) &&
                    Guid.TryParse(userProp.GetString(), out var parsedUserId))
                {
                    userId = parsedUserId;
                }

                string? assignmentMethod = null;
                if (node.TryGetProperty("assignmentMethod", out var amProp) && amProp.ValueKind == JsonValueKind.String)
                {
                    assignmentMethod = amProp.GetString()?.Trim().ToUpperInvariant();
                }
                else if (node.TryGetProperty("approverType", out var atProp) && atProp.ValueKind == JsonValueKind.String)
                {
                    assignmentMethod = atProp.GetString()?.Trim().ToUpperInvariant();
                }

                nodeMap[id] = (type, name, deptId, roleId, userId, assignmentMethod, node);

                if (type == "START") startCount++;
                else if (type == "END") endCount++;
            }

            // 1. Kiểm tra số lượng START / END
            if (startCount == 0)
                errors.Add("Quy trình phải có ít nhất một sự kiện Bắt đầu (Start Event).");
            else if (startCount > 1)
                errors.Add("Quy trình chỉ được phép có duy nhất một sự kiện Bắt đầu (Start Event).");

            if (endCount == 0)
                errors.Add("Quy trình phải có ít nhất một sự kiện Kết thúc (End Event).");

            // 2. Phân tích các liên kết (edges / transitions)
            var edges = new List<(string Id, string Source, string Target, string Label, string? OutcomeCode, string? ConditionId, bool IsDefault)>();
            if (root.TryGetProperty("edges", out var edgesElement) && edgesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var edge in edgesElement.EnumerateArray())
                {
                    if (edge.ValueKind != JsonValueKind.Object) continue;

                    var edgeId = edge.TryGetProperty("id", out var eid) ? eid.GetString()?.Trim() ?? "" : "";
                    var source = edge.TryGetProperty("source", out var s) ? s.GetString()?.Trim() ?? "" : "";
                    var target = edge.TryGetProperty("target", out var t) ? t.GetString()?.Trim() ?? "" : "";
                    var label = edge.TryGetProperty("label", out var l) ? l.GetString()?.Trim() ?? "" : "";
                    var outcomeCode = edge.TryGetProperty("outcomeCode", out var oc) ? oc.GetString()?.Trim().ToUpperInvariant() : null;
                    var conditionId = edge.TryGetProperty("conditionId", out var cid) ? cid.GetString()?.Trim() : null;
                    bool isDefault = edge.TryGetProperty("isDefault", out var df) && df.ValueKind == JsonValueKind.True;

                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                    {
                        errors.Add($"Liên kết '{edgeId}' thiếu điểm xuất phát hoặc điểm đến.");
                        continue;
                    }

                    if (!nodeMap.ContainsKey(source))
                    {
                        errors.Add($"Liên kết '{edgeId}' xuất phát từ nút không tồn tại: '{source}'.");
                        continue;
                    }

                    if (!nodeMap.ContainsKey(target))
                    {
                        errors.Add($"Liên kết '{edgeId}' đi tới nút không tồn tại: '{target}'.");
                        continue;
                    }

                    if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Liên kết không hợp lệ: Nút '{nodeMap[source].Name}' không thể tự kết nối với chính nó.");
                        continue;
                    }

                    edges.Add((edgeId, source, target, label, outcomeCode, conditionId, isDefault));
                }
            }

            // Bản đồ kề cho việc duyệt đồ thị
            var outgoing = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var incoming = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var nid in nodeMap.Keys)
            {
                outgoing[nid] = new();
                incoming[nid] = new();
            }

            foreach (var edge in edges)
            {
                outgoing[edge.Source].Add(edge.Target);
                incoming[edge.Target].Add(edge.Source);
            }

            // 3. Kiểm tra từng loại nút cụ thể
            foreach (var (nid, nodeInfo) in nodeMap)
            {
                var (type, name, deptId, roleId, userId, assignmentMethod, raw) = nodeInfo;

                switch (type)
                {
                    case "START":
                        if (incoming[nid].Count > 0)
                            errors.Add("Sự kiện Bắt đầu không được phép có liên kết đi vào.");
                        if (outgoing[nid].Count == 0)
                            errors.Add("Sự kiện Bắt đầu chưa được kết nối tới bước xử lý nào tiếp theo.");

                        // Kiểm tra trigger
                        if (raw.TryGetProperty("trigger", out var trigProp) && trigProp.ValueKind == JsonValueKind.String)
                        {
                            var trigger = trigProp.GetString()?.Trim().ToUpperInvariant();
                            if (!string.IsNullOrEmpty(trigger))
                            {
                                if (businessType == WorkflowBusinessType.Task && trigger == "REQUEST_CREATED")
                                    errors.Add($"Sự kiện Bắt đầu '{name}': Quy trình công việc (TASK) không thể kích hoạt bởi sự kiện Tạo yêu cầu (REQUEST_CREATED).");
                                else if (businessType == WorkflowBusinessType.Request && trigger == "TASK_CREATED")
                                    errors.Add($"Sự kiện Bắt đầu '{name}': Quy trình yêu cầu (REQUEST) không thể kích hoạt bởi sự kiện Tạo nhiệm vụ (TASK_CREATED).");
                            }
                        }
                        break;

                    case "END":
                        if (outgoing[nid].Count > 0)
                            errors.Add($"Sự kiện Kết thúc '{name}' không được phép có liên kết đi tiếp.");
                        if (incoming[nid].Count == 0)
                            errors.Add($"Sự kiện Kết thúc '{name}' chưa có liên kết dẫn tới.");

                        if (raw.TryGetProperty("completionState", out var compProp) && compProp.ValueKind == JsonValueKind.String)
                        {
                            var state = compProp.GetString()?.Trim().ToUpperInvariant();
                            if (!string.IsNullOrEmpty(state) && !ValidEndStates.Contains(state))
                                errors.Add($"Sự kiện Kết thúc '{name}' có trạng thái hoàn tất '{state}' không hợp lệ.");
                        }
                        break;

                    case "DEPARTMENT":
                        if (incoming[nid].Count == 0)
                            errors.Add($"Bước xử lý '{name}' không có liên kết dẫn tới (bị cô lập).");
                        if (outgoing[nid].Count == 0)
                            errors.Add($"Bước xử lý '{name}' chưa kết nối tới bước tiếp theo.");

                        // Phân công phòng ban
                        var deptMethod = assignmentMethod ?? "DEPARTMENT_QUEUE";
                        if (deptMethod is "DEPARTMENT_QUEUE" or "DEPARTMENT_MANAGER" or "BUSINESS_ROLE" || deptId.HasValue)
                        {
                            if (!deptId.HasValue || deptId.Value == Guid.Empty)
                            {
                                errors.Add($"Bước xử lý '{name}' chưa được chọn phòng ban phụ trách.");
                            }
                            else if (!validDepartmentIds.Contains(deptId.Value))
                            {
                                errors.Add($"Phòng ban tại bước '{name}' không tồn tại hoặc không thuộc công ty hiện tại.");
                            }
                        }

                        if (deptMethod == "BUSINESS_ROLE" && (!roleId.HasValue || roleId.Value == Guid.Empty))
                        {
                            errors.Add($"Bước xử lý '{name}' chọn phân công theo vai trò nhưng chưa chỉ định vai trò nghiệp vụ.");
                        }

                        if (deptMethod == "SPECIFIC_USER")
                        {
                            if (!userId.HasValue || userId.Value == Guid.Empty)
                            {
                                errors.Add($"Bước xử lý '{name}' được cấu hình giao cho nhân viên cụ thể nhưng chưa chọn nhân viên.");
                            }
                            else if (validUserIds != null && !validUserIds.Contains(userId.Value))
                            {
                                errors.Add($"Nhân viên được chỉ định tại bước '{name}' không tồn tại hoặc không còn hoạt động trong công ty.");
                            }
                        }

                        if (roleId.HasValue && roleId.Value != Guid.Empty)
                        {
                            if (!validRoleIds.Contains(roleId.Value))
                            {
                                errors.Add($"Vai trò phân công tại bước '{name}' không thuộc công ty hiện tại hoặc là vai trò hệ thống cấp nền tảng không hợp lệ.");
                            }
                        }
                        break;

                    case "APPROVAL":
                        if (incoming[nid].Count == 0)
                            errors.Add($"Bước phê duyệt '{name}' không có liên kết dẫn tới.");
                        if (outgoing[nid].Count == 0)
                            errors.Add($"Bước phê duyệt '{name}' chưa có nhánh chuyển tiếp.");

                        var apprMethod = assignmentMethod ?? "ROLE";
                        if (apprMethod is "ROLE" or "BUSINESS_ROLE")
                        {
                            if (!roleId.HasValue || roleId.Value == Guid.Empty)
                            {
                                errors.Add($"Bước phê duyệt '{name}' chưa được cấu hình vai trò người phê duyệt.");
                            }
                            else if (!validRoleIds.Contains(roleId.Value))
                            {
                                errors.Add($"Vai trò phê duyệt tại bước '{name}' không thuộc công ty hiện tại hoặc là vai trò quản trị hệ thống cấp nền tảng không hợp lệ.");
                            }
                        }
                        else if (apprMethod is "DEPARTMENT_LEAD" or "DEPARTMENT_MANAGER" or "DIRECT_MANAGER")
                        {
                            if (deptId.HasValue && deptId.Value != Guid.Empty && !validDepartmentIds.Contains(deptId.Value))
                            {
                                errors.Add($"Phòng ban tại bước phê duyệt '{name}' không thuộc công ty hiện tại.");
                            }
                        }
                        else if (apprMethod == "SPECIFIC_USER")
                        {
                            if (!userId.HasValue || userId.Value == Guid.Empty)
                            {
                                errors.Add($"Bước phê duyệt '{name}' chọn người duyệt cụ thể nhưng chưa chỉ định nhân sự.");
                            }
                            else if (validUserIds != null && !validUserIds.Contains(userId.Value))
                            {
                                errors.Add($"Người duyệt chỉ định tại bước '{name}' không tồn tại hoặc không thuộc công ty hiện tại.");
                            }
                        }

                        if (roleId.HasValue && roleId.Value != Guid.Empty && !validRoleIds.Contains(roleId.Value))
                        {
                            errors.Add($"Vai trò phê duyệt tại bước '{name}' không thuộc công ty hiện tại hoặc là vai trò quản trị hệ thống cấp nền tảng không hợp lệ.");
                        }
                        break;

                    case "DECISION":
                        if (incoming[nid].Count == 0)
                            errors.Add($"Cổng rẽ nhánh '{name}' không có liên kết dẫn tới.");
                        if (outgoing[nid].Count < 2)
                            errors.Add($"Cổng rẽ nhánh '{name}' phải có ít nhất 2 nhánh rẽ điều kiện.");

                        // Kiểm tra danh sách conditions nếu có
                        if (raw.TryGetProperty("conditions", out var condsElement) && condsElement.ValueKind == JsonValueKind.Array)
                        {
                            int defaultBranchCount = 0;
                            var nodeOutEdges = edges.Where(e => string.Equals(e.Source, nid, StringComparison.OrdinalIgnoreCase)).ToList();

                            foreach (var cond in condsElement.EnumerateArray())
                            {
                                if (cond.ValueKind != JsonValueKind.Object) continue;

                                var f = cond.TryGetProperty("field", out var fp) ? fp.GetString()?.Trim() :
                                        cond.TryGetProperty("fieldKey", out var fkp) ? fkp.GetString()?.Trim() : null;
                                var op = cond.TryGetProperty("operator", out var opProp) ? opProp.GetString()?.Trim() : null;
                                var val = cond.TryGetProperty("value", out var vp) ? vp.GetString()?.Trim() : null;
                                bool isDef = (cond.TryGetProperty("isDefault", out var dp) && dp.ValueKind == JsonValueKind.True) ||
                                             string.Equals(op, "default", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(f, "default", StringComparison.OrdinalIgnoreCase);

                                if (isDef)
                                {
                                    defaultBranchCount++;
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(f))
                                {
                                    errors.Add($"Cổng rẽ nhánh '{name}': Có điều kiện rẽ nhánh thiếu tên trường dữ liệu.");
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(op) || !WorkflowConditionEvaluator.ValidOperators.Contains(op))
                                {
                                    errors.Add($"Cổng rẽ nhánh '{name}': Toán tử so sánh '{op}' không hợp lệ.");
                                }

                                if (op is not ("IS_EMPTY" or "IS_NOT_EMPTY") && string.IsNullOrEmpty(val))
                                {
                                    warnings.Add($"Cổng rẽ nhánh '{name}': Điều kiện trường '{f}' chưa nhập giá trị so sánh.");
                                }
                            }

                            if (defaultBranchCount > 1)
                            {
                                errors.Add($"Cổng rẽ nhánh '{name}' chỉ được phép có duy nhất một nhánh mặc định (default branch).");
                            }

                            // Phase 12.C5: Hai outgoing edges nhưng không có điều kiện/default phù hợp -> Validate không đạt
                            if (nodeOutEdges.Count >= 2 && condsElement.GetArrayLength() == 0)
                            {
                                errors.Add($"Cổng rẽ nhánh '{name}' có {nodeOutEdges.Count} nhánh rẽ nhưng chưa được cấu hình logic điều kiện hoặc nhánh mặc định.");
                            }
                        }
                        break;

                    case "CONFIRMATION":
                        if (incoming[nid].Count == 0)
                            errors.Add($"Bước xác nhận '{name}' không có liên kết dẫn tới.");
                        if (outgoing[nid].Count == 0)
                            errors.Add($"Bước xác nhận '{name}' chưa kết nối tới bước tiếp theo.");

                        if (assignmentMethod == "SPECIFIC_USER")
                        {
                            if (!userId.HasValue || userId.Value == Guid.Empty)
                            {
                                errors.Add($"Bước xác nhận '{name}' cấu hình giao cho nhân sự cụ thể nhưng chưa chọn nhân viên.");
                            }
                            else if (validUserIds != null && !validUserIds.Contains(userId.Value))
                            {
                                errors.Add($"Nhân viên xác nhận tại bước '{name}' không tồn tại hoặc không thuộc công ty hiện tại.");
                            }
                        }
                        break;

                    default:
                        warnings.Add($"Phát hiện loại nút tùy biến: '{type}' tại '{name}'.");
                        break;
                }
            }

            // 4. Kiểm tra tính liên thông toàn đồ thị (Reachability từ START)
            var startNode = nodeMap.FirstOrDefault(kvp => kvp.Value.Type == "START").Key;
            if (!string.IsNullOrEmpty(startNode))
            {
                var visitedFromStart = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>();
                queue.Enqueue(startNode);
                visitedFromStart.Add(startNode);

                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    foreach (var next in outgoing[curr])
                    {
                        if (visitedFromStart.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                foreach (var nid in nodeMap.Keys)
                {
                    if (!visitedFromStart.Contains(nid))
                    {
                        errors.Add($"Nút '{nodeMap[nid].Name}' không thể tiếp cận được từ sự kiện Bắt đầu (nhánh bị cô lập).");
                    }
                }
            }

            // 5. Kiểm tra mọi nút phải dẫn được tới ít nhất 1 sự kiện END
            var endNodes = nodeMap.Where(kvp => kvp.Value.Type == "END").Select(kvp => kvp.Key).ToList();
            if (endNodes.Count > 0)
            {
                var canReachEnd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var reverseQueue = new Queue<string>();
                foreach (var endId in endNodes)
                {
                    canReachEnd.Add(endId);
                    reverseQueue.Enqueue(endId);
                }

                while (reverseQueue.Count > 0)
                {
                    var curr = reverseQueue.Dequeue();
                    foreach (var prev in incoming[curr])
                    {
                        if (canReachEnd.Add(prev))
                        {
                            reverseQueue.Enqueue(prev);
                        }
                    }
                }

                foreach (var nid in nodeMap.Keys)
                {
                    if (!canReachEnd.Contains(nid))
                    {
                        errors.Add($"Từ bước '{nodeMap[nid].Name}' không có đường dẫn nào tới sự kiện Kết thúc (nhánh cụt).");
                    }
                }
            }

            // 6. Kiểm tra chu trình lặp vô tận (Infinite loop without human step)
            // Chu trình có điểm xử lý con người (APPROVAL / CONFIRMATION / DEPARTMENT) được chấp nhận là rework loop hợp lệ
            var inDegree = incoming.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count, StringComparer.OrdinalIgnoreCase);
            var zeroInQueue = new Queue<string>();
            foreach (var (k, v) in inDegree)
            {
                if (v == 0) zeroInQueue.Enqueue(k);
            }

            int topoCount = 0;
            var nonCycleNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (zeroInQueue.Count > 0)
            {
                var u = zeroInQueue.Dequeue();
                topoCount++;
                nonCycleNodes.Add(u);
                foreach (var v in outgoing[u])
                {
                    inDegree[v]--;
                    if (inDegree[v] == 0) zeroInQueue.Enqueue(v);
                }
            }

            if (topoCount < nodeMap.Count && startCount == 1 && endCount >= 1 && errors.Count == 0)
            {
                var cycleNodes = nodeMap.Keys.Where(k => !nonCycleNodes.Contains(k)).ToList();
                bool hasHumanStepInCycle = cycleNodes.Any(nid => nodeMap[nid].Type is "DEPARTMENT" or "APPROVAL" or "CONFIRMATION");

                if (!hasHumanStepInCycle)
                {
                    errors.Add("Sơ đồ chứa vòng lặp rẽ nhánh tự động kín không có điểm dừng xử lý của con người.");
                }
            }
        }

        return new(errors.Count == 0, errors, warnings);
    }
}
