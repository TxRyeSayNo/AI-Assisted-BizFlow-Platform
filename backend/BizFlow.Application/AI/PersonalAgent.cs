using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Tasks;

namespace BizFlow.Application.AI;

public sealed partial class RobotChatService
{
    // This JSON is a model prompt, never HTML. Keep Vietnamese readable instead of turning
    // every accented character into a \u escape that obscures short follow-up messages.
    private static readonly JsonSerializerOptions AgentJson = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private async Task<RobotChatResponse> RunPersonalAgentAsync(RobotChatCommand command, CancellationToken cancellationToken)
    {
        if (modelClient is not { IsConfigured: true })
            return new("AI Agent chưa được cấu hình mô hình. Vui lòng liên hệ quản trị viên để bật dịch vụ AI.", "AGENT_UNAVAILABLE");
        var tenantId = tenantContext.TenantId!.Value;
        var userId = tenantContext.UserId!.Value;
        var conversationId = string.IsNullOrWhiteSpace(command.ConversationId) ? "default" : command.ConversationId.Trim();
        if (conversationId.Length > 100) throw new ApplicationFault(FaultKind.Validation, "AI.CONVERSATION_INVALID", "Mã hội thoại quá dài.");
        var state = conversationStateManager == null ? null : await conversationStateManager.GetStateAsync(tenantId, userId, conversationId, cancellationToken);
        // Client history may contain newer events, such as a confirmation through the action endpoint.
        var history = (command.History is { Count: > 0 } ? command.History : state?.MessageHistory ?? [])
            .TakeLast(20).Select(h => new RobotChatMessageDto(h.Sender == "user" ? "user" : "robot", Truncate(h.Text, 4000))).ToList();
        var plan = state?.WorkPlan;
        var pendingAtStart = plan?.ConfirmationToken;
        var preferences = learnedChoiceStore == null ? [] : (await learnedChoiceStore.ListChoicesAsync(tenantId, userId, cancellationToken: cancellationToken))
            .Where(p => p.UserId == userId && p.Scope == "PERSONAL").Take(20).Select(p => new { p.SemanticKey, p.ChoiceValue }).ToArray();
        AiTenantContextData? workspace = null;
        var results = new List<object>();
        const string instructions = AgentInstructions.System;
        try
        {
            if (contextReader != null) workspace = await contextReader.GetTenantContextAsync(tenantId, cancellationToken);
            for (var step = 0; step < 5; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = new
                {
                    currentUserId = userId, currentTime = clock.GetUtcNow(), locale = command.Locale ?? "vi", message = command.Message, history, preferences,
                    workPlan = plan == null ? null : new { plan.Goal, plan.ToolName, arguments = ParseArgs(plan.ArgumentsJson), plan.Completed,
                        awaitingConfirmation = plan.ConfirmationToken != null, missingFields = AgentToolSchemas.MissingOrInvalid(plan.ToolName, ParseArgs(plan.ArgumentsJson), clock.GetUtcNow()) },
                    workspace = workspace == null ? null : new { workspace.TimeZone, workspace.Departments, user = CurrentAgentUser(workspace, userId) },
                    actionTools = toolRegistry?.GetRegisteredTools() ?? [], readTools = AvailableReadTools(), toolResults = results
                };
                var model = await modelClient.CompleteConversationAsync(JsonSerializer.Serialize(input, AgentJson), instructions,
                    history, command.Message, cancellationToken);
                using var document = JsonDocument.Parse(model.Content);
                var root = document.RootElement;
                if (root.TryGetProperty("cancel", out var cancel) && cancel.ValueKind == JsonValueKind.True)
                {
                    plan = plan is null ? null : plan with { Completed = true, ConfirmationToken = null, ConfirmationArgsJson = null };
                    return await FinishAsync(new("Đã hủy công việc đang chuẩn bị.", "AGENT_CANCELLED"));
                }
                if (root.TryGetProperty("confirmAction", out var confirm) && confirm.ValueKind == JsonValueKind.True)
                {
                    if (root.TryGetProperty("plan", out _))
                    {
                        results.Add(new { error = "REVIEW_CHANGES_FIRST", message = "Không được vừa sửa tham số vừa duyệt bản cũ. Hãy cập nhật plan để tạo bản xem trước mới." });
                        continue;
                    }
                    if (plan?.ConfirmationToken == null || plan.ConfirmationToken != pendingAtStart || assistanceService == null)
                    {
                        results.Add(new { error = "NO_PENDING_CONFIRMATION", message = "Chưa có bản xem trước được duyệt trong cuộc hội thoại này." });
                        continue;
                    }
                    var done = await assistanceService.ExecuteActionAsync(new(plan.ToolName, plan.ConfirmationArgsJson!, true, plan.ConfirmationToken), cancellationToken);
                    plan = plan with { Completed = true, ConfirmationToken = null, ConfirmationArgsJson = null };
                    var text = done.Output is AgentNotificationResult sent ? $"Đã gửi thông báo trong ứng dụng tới {sent.DeliveredCount} người nhận. {sent.AlreadyDeliveredCount} người đã nhận trước đó nên không gửi trùng." : "Đã thực hiện hành động được xác nhận.";
                    return await FinishAsync(new(text, "EXECUTE_ACTION"));
                }
                if (root.TryGetProperty("plan", out var proposed))
                {
                    var name = RequiredString(proposed, "toolName");
                    if (toolRegistry?.GetTool(name) == null) { results.Add(new { error = "TOOL_NOT_ALLOWED", name }); continue; }
                    var reset = root.TryGetProperty("newGoal", out var newGoal) && newGoal.ValueKind == JsonValueKind.True;
                    var prior = !reset && plan?.ToolName == name ? plan : null;
                    var merged = prior == null ? new JsonObject() : ParseArgs(prior.ArgumentsJson);
                    foreach (var field in proposed.GetProperty("arguments").EnumerateObject())
                    {
                        if (field.Name is "operationId" or "recipientIds" or "startsAtEvidence") continue;
                        var fieldName = field.Name;
                        if (field.Value.ValueKind == JsonValueKind.Null) merged.Remove(fieldName);
                        else merged[fieldName] = JsonNode.Parse(field.Value.GetRawText());
                    }
                    if (name == "send_meeting_notification" && merged["startsAt"] is { } startsAt)
                    {
                        var evidence = AgentTimeEvidence.Find(startsAt.ToString(), workspace?.TimeZone ?? "UTC",
                            new[] { command.Message }.Concat(history.Where(h => h.Sender == "user").Reverse().Select(h => h.Text))
                                .Concat(prior == null ? [] : new[] { ParseArgs(prior.ArgumentsJson)["startsAtEvidence"]?.ToString() ?? "" }));
                        if (evidence == null)
                        {
                            merged.Remove("startsAt");
                            merged.Remove("startsAtEvidence");
                        }
                        else merged["startsAtEvidence"] = evidence;
                    }
                    var json = merged.ToJsonString();
                    var unchanged = json == prior?.ArgumentsJson;
                    plan = new(Text(proposed, "goal") ?? prior?.Goal ?? command.Message, name, json, prior is { Completed: false } ? prior.OperationId : Guid.NewGuid(),
                        unchanged ? prior?.ConfirmationToken : null, unchanged ? prior?.ConfirmationArgsJson : null, unchanged && prior!.Completed);
                }
                if (root.TryGetProperty("answerOnly", out var answerOnly) && answerOnly.ValueKind == JsonValueKind.True)
                    return await FinishAsync(new(RequiredString(root, "reply"), "AGENT_REPLY"));
                if (plan is { Completed: false } && !root.TryGetProperty("plan", out _) && !root.TryGetProperty("toolCall", out _))
                {
                    results.Add(new { error = "WORK_PLAN_UPDATE_REQUIRED", message = "Phải phân tích message mới và trả plan cập nhật trước khi hỏi tiếp; không được bỏ qua dữ kiện vừa nhận. Nếu chỉ trả lời câu hỏi xen kẽ, dùng answerOnly=true." });
                    continue;
                }
                if (root.TryGetProperty("toolCall", out var call))
                {
                    var name = RequiredString(call, "name");
                    var args = call.GetProperty("arguments");
                    if (args.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid arguments.");
                    object? result;
                    try
                    {
                        switch (name)
                        {
                            case "list_tasks": result = await taskListQuery.ListAsync(new TaskListFilter(Page: ReadPage(args), PageSize: 50,
                                Search: Text(args, "search"), Status: Text(args, "status"), Priority: Text(args, "priority")), cancellationToken); break;
                            case "list_people" when userDirectoryQuery != null:
                                var people = await userDirectoryQuery.ListAsync(new(Page: ReadPage(args), PageSize: 25, Search: Text(args, "search"), Status: "ACTIVE", DepartmentId: ReadId(args, "departmentId", false)), cancellationToken);
                                result = new { people.Page, people.PageSize, people.Total, items = people.Items.Select(u => new { u.UserId, u.FullName, u.DepartmentId, u.DepartmentName }) }; break;
                            case "list_requests" when requestListQuery != null:
                                result = await requestListQuery.ListAsync(new(Page: ReadPage(args), PageSize: 25, Search: Text(args, "search"), Status: Text(args, "status"), Priority: Text(args, "priority")), cancellationToken); break;
                            case "task_details" when taskDetailQuery != null:
                                result = await taskDetailQuery.GetAsync(ReadId(args, "taskId")!.Value, new(), cancellationToken); break;
                            case "request_details" when requestDetailQuery != null:
                                result = await requestDetailQuery.GetAsync(ReadId(args, "requestId")!.Value, cancellationToken); break;
                            case "workflow_status" when workflowRuntime != null:
                                result = await workflowRuntime.GetAsync(ReadId(args, "requestId")!.Value, cancellationToken); break;
                            case "workflow_inbox" when workflowRuntime != null:
                                result = await workflowRuntime.InboxAsync(ReadPage(args), 25, cancellationToken); break;
                            case "notifications": result = await notificationInbox.ListAsync(new NotificationFilter(1, 10), cancellationToken); break;
                            case "profile": result = workspace == null ? null : new { user = CurrentAgentUser(workspace, userId), workspace.Departments, workspace.TimeZone }; break;
                            case "search_memory" when vectorMemoryStore != null: result = await vectorMemoryStore.SearchMemoriesAsync(tenantId, RequiredString(args, "query"), maxResults: 5, cancellationToken: cancellationToken); break;
                            case "remember_preference" when learnedChoiceStore != null:
                                var content = RequiredString(args, "content");
                                return await FinishAsync(new("Bạn có muốn lưu sở thích này vào hồ sơ cá nhân?\n" + content, "AGENT_MEMORY_PROPOSAL",
                                    [new RobotActionCard("PREFILL_PROMPT", "Lưu sở thích", null, new { template = "/remember " + content })]));
                            case "decompose":
                                var preview = await decompositionService.PreviewAsync(new(RequiredString(args, "prompt"), RequireModel: true), cancellationToken);
                                return await FinishAsync(new("Đây là bản đề xuất theo mục tiêu của bạn. Hãy kiểm tra các nhiệm vụ trước khi tạo.", "AGENT_PLAN",
                                    [new RobotActionCard("OPEN_DECOMPOSITION", "Xem đề xuất nhiệm vụ", "/tasks/ai-decomposition", new { proposals = preview.Proposals })]));
                            default:
                                result = toolRegistry?.GetTool(name) != null
                                    ? new { error = "PLAN_REQUIRED", message = "Công cụ ghi phải đi qua plan để kiểm tra dữ kiện và chuẩn bị xác nhận." }
                                    : (object)new { error = "TOOL_NOT_ALLOWED", name, reason = "Capability unavailable; this is not a network error." };
                                break;
                        }
                    }
                    catch (ApplicationFault fault) { result = new { error = fault.Code, kind = fault.Kind.ToString(), message = fault.Message }; }
                    results.Add(new { name, arguments = args.Clone(), result });
                    continue;
                }
                if (plan != null && !plan.Completed)
                {
                    var args = ParseArgs(plan.ArgumentsJson);
                    var missing = AgentToolSchemas.MissingOrInvalid(plan.ToolName, args, clock.GetUtcNow());
                    if (missing.Count > 0)
                    {
                        var questions = new Dictionary<string, string>();
                        if (root.TryGetProperty("questions", out var requested) && requested.ValueKind == JsonValueKind.Array)
                            foreach (var q in requested.EnumerateArray())
                            {
                                var field = Text(q, "field"); var question = Text(q, "question");
                                if (field != null && question != null && missing.ContainsKey(field)) questions.TryAdd(field, question);
                            }
                        foreach (var field in missing.Take(3)) questions.TryAdd(field.Key, "Bạn cho biết " + field.Value.ToLowerInvariant() + "?");
                        return await FinishAsync(new(DescribeKnown(plan, workspace) + "\n\n### Cần bổ sung\n" + string.Join("\n", questions.Take(3).Select(q => "- " + q.Value)), "AGENT_CLARIFICATION_REQUIRED"));
                    }
                    if (assistanceService == null) throw new ApplicationFault(FaultKind.Unavailable, "AI.TOOL_UNAVAILABLE", "Dịch vụ thực thi chưa sẵn sàng.");
                    args["operationId"] = plan.OperationId.ToString();
                    AgentNotificationPreview? notice = null;
                    if (plan.ToolName is "send_meeting_notification" or "send_department_notification")
                    {
                        if (agentNotificationService == null) throw new ApplicationFault(FaultKind.Unavailable, "AI.TOOL_UNAVAILABLE", "Dịch vụ gửi thông báo chưa sẵn sàng.");
                        notice = await agentNotificationService.PreviewAsync(tenantId, plan.ToolName, args.ToJsonString(), cancellationToken);
                        args["recipientIds"] = JsonSerializer.SerializeToNode(notice.RecipientIds);
                    }
                    var confirmationArgs = args.ToJsonString();
                    var action = await assistanceService.ExecuteActionAsync(new(plan.ToolName, confirmationArgs, false), cancellationToken);
                    plan = plan with { ConfirmationToken = action.ConfirmationToken, ConfirmationArgsJson = confirmationArgs };
                    var review = notice == null ? DescribeKnown(plan, workspace) : $"### Xem trước thông báo\n**Người nhận:** {notice.RecipientCount} người ({string.Join(", ", notice.Departments)}).\n\n**{notice.Title}**\n{notice.Content}";
                    return await FinishAsync(new(review + "\n\nBạn có thể bấm xác nhận hoặc trả lời đồng ý thực hiện.", "PROPOSE_ACTION",
                        [new RobotActionCard("CONFIRM_ACTION", "Xác nhận thực hiện", "/api/v1/ai/actions/execute",
                            new { toolName = plan.ToolName, argsJson = confirmationArgs, confirmationToken = action.ConfirmationToken })]));
                }
                var reply = Text(root, "reply");
                if (string.IsNullOrWhiteSpace(reply)) throw new JsonException("Empty reply.");
                return await FinishAsync(new(reply, "AGENT_REPLY"));
            }
            return await FinishAsync(new("Agent đã đạt giới hạn xử lý lượt này. Dữ kiện đã được giữ lại để bạn tiếp tục.", "AGENT_STEP_LIMIT"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ApplicationFault fault) { return await FinishAsync(new(fault.Message, fault.Kind == FaultKind.Forbidden ? "ACTION_REJECTED" : "AGENT_UNAVAILABLE")); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or System.Net.Http.HttpRequestException or OperationCanceledException)
        { return await FinishAsync(new("Agent chưa xử lý được lượt này. Dữ kiện đã được giữ lại; vui lòng thử lại.", "AGENT_UNAVAILABLE")); }

        async Task<RobotChatResponse> FinishAsync(RobotChatResponse response)
        {
            history.Add(new("user", command.Message)); history.Add(new("robot", response.Response));
            if (conversationStateManager != null) await conversationStateManager.SaveStateAsync(new(conversationId, tenantId, userId, plan?.Goal, new(), [], null,
                history.TakeLast(20).ToList(), clock.GetUtcNow(), plan), cancellationToken);
            return response;
        }
    }

    private static JsonObject ParseArgs(string value) => JsonNode.Parse(value) as JsonObject ?? throw new JsonException("Arguments must be an object.");
    private static string? Text(JsonElement args, string name) => args.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
    private static string RequiredString(JsonElement args, string name) => !string.IsNullOrWhiteSpace(Text(args, name)) ? Text(args, name)! : throw new JsonException("Missing " + name);
    private static string DescribeKnown(AgentWorkPlan plan, AiTenantContextData? workspace)
    {
        var args = ParseArgs(plan.ArgumentsJson); var fields = new List<string>();
        var properties = AgentToolSchemas.For(plan.ToolName)?.GetProperty("properties");
        foreach (var field in args)
        {
            if (field.Value == null || field.Key is "operationId" or "recipientIds" or "departmentIds" or "startsAtEvidence") continue;
            var title = properties is { } props && props.TryGetProperty(field.Key, out var rule) && rule.TryGetProperty("title", out var label) ? label.GetString()! : field.Key;
            var value = field.Value.ToString();
            if (field.Key == "recipientScope") value = value == "ALL_COMPANY" ? "Toàn thể công ty" : "Các phòng ban đã chọn";
            fields.Add("**" + title + ":** " + value);
        }
        if (args["departmentIds"] is JsonArray ids && workspace != null)
            fields.Add("**Phòng ban:** " + string.Join(", ", workspace.Departments.Where(d => ids.Any(id => id?.ToString() == d.DepartmentId.ToString())).Select(d => d.Name)));
        return fields.Count == 0 ? "**Mục tiêu:** " + plan.Goal : "### Đã ghi nhận\n" + string.Join("\n", fields.Select(f => "- " + f));
    }
}
