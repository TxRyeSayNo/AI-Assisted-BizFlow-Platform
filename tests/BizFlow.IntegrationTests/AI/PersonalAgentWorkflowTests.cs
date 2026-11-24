using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BizFlow.Application.AI;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BizFlow.IntegrationTests.AI;

[Collection("Postgres")]
public sealed class PersonalAgentWorkflowTests(PostgresFixture database)
{
    private const string Password = "Agent-workflow-test!59127";
    private sealed class ScriptedModel : IAiModelClient
    {
        public bool IsConfigured => true;
        public Queue<string> Replies { get; } = new();
        public List<JsonElement> Inputs { get; } = [];
        public Task<AiModelResponse> CompleteAsync(string prompt, string systemInstructions, CancellationToken cancellationToken = default)
        {
            Inputs.Add(JsonDocument.Parse(prompt).RootElement.Clone());
            return Task.FromResult(new AiModelResponse(Replies.Dequeue(), "test-model", 1, 1, 1));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Multi_turn_meeting_collects_only_missing_facts_and_really_delivers_after_approval(bool naturalLanguageApproval)
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        var model = new ScriptedModel();
        model.Replies.Enqueue("""{"plan":{"goal":"Gửi thông báo chuẩn bị cuộc họp ngày mai","toolName":"send_meeting_notification","arguments":{}}}""");
        model.Replies.Enqueue("""{"plan":{"toolName":"send_meeting_notification","arguments":{"topic":"Báo cáo tổng kết công ty Quý 4","recipientScope":"ALL_COMPANY"}},"questions":[{"field":"topic","question":"Nội dung là gì?"},{"field":"recipientScope","question":"Phòng ban nào?"},{"field":"startsAt","question":"Họp lúc mấy giờ?"}]}""");
        var startsAt = new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1).AddHours(9), TimeSpan.FromHours(7)).ToString("O");
        model.Replies.Enqueue(JsonSerializer.Serialize(new { plan = new { toolName = "send_meeting_notification", arguments = new { startsAt, location = "Phòng họp tầng 3" } } }));
        if (naturalLanguageApproval) model.Replies.Enqueue("""{"confirmAction":true}""");
        await using var factory = Factory(model);
        using var client = factory.CreateClient();
        await LoginAsync(client, account);
        var conversation = Guid.NewGuid().ToString("N");
        var first = await ChatAsync(client, new("Gửi thông báo tới các phòng ban yêu cầu chuẩn bị cho cuộc họp ngày mai", ConversationId: conversation));
        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", first.RecognizedCommand);
        var second = await ChatAsync(client, new("Nội dung về báo cáo tổng kết công ty Quý 4, toàn thể công ty", ConversationId: conversation));
        Assert.Contains("Quý 4", second.Response);
        Assert.Contains("Toàn thể công ty", second.Response);
        Assert.DoesNotContain("Nội dung là gì?", second.Response);
        Assert.DoesNotContain("Phòng ban nào?", second.Response);
        Assert.Contains("Họp lúc mấy giờ?", second.Response);
        var third = await ChatAsync(client, new("9h sáng mai, phòng họp tầng 3", ConversationId: conversation));
        Assert.Equal("PROPOSE_ACTION", third.RecognizedCommand);
        Assert.Contains("2 người", third.Response);
        await using var db = database.Create(account.User, account.Tenant);
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
        var card = Assert.Single(third.ActionCards!);
        var payload = JsonSerializer.SerializeToElement(card.Payload);
        var command = new ExecuteAiActionCommand(payload.GetProperty("toolName").GetString()!, payload.GetProperty("argsJson").GetString()!, true,
            payload.GetProperty("confirmationToken").GetString(), conversation);
        if (naturalLanguageApproval)
        {
            var result = await ChatAsync(client, new("Đồng ý, gửi đi", ConversationId: conversation));
            Assert.Equal("EXECUTE_ACTION", result.RecognizedCommand);
            Assert.Contains("2 người nhận", result.Response);
        }
        else
        {
            using var result = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", command);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var executed = await result.Content.ReadFromJsonAsync<ExecuteAiActionResult>();
            Assert.Equal("SUCCEEDED", executed!.ExecutionStatus);
        }
        var delivered = await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync();
        Assert.Equal(2, delivered.Count);
        Assert.All(delivered, n => { Assert.Equal("MeetingInvitation", n.Type); Assert.Contains("Quý 4", n.Content); Assert.Contains("Phòng họp tầng 3", n.Content); });
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == foreign.Tenant).ToListAsync());
        using var replay = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", command);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(2, await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.TenantId == account.Tenant));
        using var scope = factory.Services.CreateScope();
        var state = await scope.ServiceProvider.GetRequiredService<IConversationStateManager>().GetStateAsync(account.Tenant, account.User, conversation);
        Assert.True(state!.WorkPlan!.Completed);
    }

    [Fact]
    public async Task Unprivileged_user_cannot_send_even_with_a_model_proposed_action()
    {
        var account = await SeedAsync("EMPLOYEE");
        var model = new ScriptedModel();
        model.Replies.Enqueue("""{"plan":{"goal":"Thông báo Q4","toolName":"send_department_notification","arguments":{"title":"Q4","content":"Chuẩn bị báo cáo","recipientScope":"ALL_COMPANY"}}}""");
        await using var factory = Factory(model);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var response = await ChatAsync(client, new("Gửi thông báo Q4 tới toàn công ty", ConversationId: "denied"));
        Assert.Equal("ACTION_REJECTED", response.RecognizedCommand);
        Assert.Null(response.ActionCards);
        await using var db = database.Create(account.User, account.Tenant);
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
    }

    [Fact]
    public async Task Invalid_department_never_falls_back_to_everyone_and_tampered_arguments_cannot_execute()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var foreign = await SeedAsync("COMPANY_ADMIN");
        var model = new ScriptedModel();
        model.Replies.Enqueue(JsonSerializer.Serialize(new { plan = new { toolName = "send_department_notification", arguments = new {
            title = "Q4", content = "Chuẩn bị báo cáo", recipientScope = "DEPARTMENTS", departmentIds = new[] { foreign.Department } } } }));
        model.Replies.Enqueue("""{"plan":{"toolName":"send_department_notification","arguments":{"title":"Q4","content":"Chuẩn bị báo cáo","recipientScope":"ALL_COMPANY"}} ,"newGoal":true}""");
        await using var factory = Factory(model); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var invalid = await ChatAsync(client, new("Gửi thông báo", ConversationId: "invalid-dept"));
        Assert.Null(invalid.ActionCards);
        Assert.Contains("không tồn tại", invalid.Response);
        var ready = await ChatAsync(client, new("Gửi toàn công ty", ConversationId: "valid-dept"));
        var payload = JsonSerializer.SerializeToElement(Assert.Single(ready.ActionCards!).Payload);
        var tamperedArgs = JsonNode.Parse(payload.GetProperty("argsJson").GetString()!)!.AsObject();
        tamperedArgs["content"] = "Nội dung bị thay đổi";
        var tampered = tamperedArgs.ToJsonString();
        using var rejected = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", new ExecuteAiActionCommand("send_department_notification", tampered, true, payload.GetProperty("confirmationToken").GetString(), "valid-dept"));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await using var db = database.Create(account.User, account.Tenant);
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
    }

    [Fact]
    public async Task Editing_or_cancelling_a_plan_invalidates_previous_confirmation_cards()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var model = new ScriptedModel();
        model.Replies.Enqueue("""{"plan":{"goal":"Thông báo Q4","toolName":"send_department_notification","arguments":{"title":"Q4","content":"Bản đầu","recipientScope":"ALL_COMPANY"}}}""");
        model.Replies.Enqueue("""{"plan":{"toolName":"send_department_notification","arguments":{"content":"Bản đã sửa"}}}""");
        model.Replies.Enqueue("""{"cancel":true}""");
        await using var factory = Factory(model); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var first = await ChatAsync(client, new("Gửi thông báo Q4 toàn công ty", ConversationId: "edit"));
        var edited = await ChatAsync(client, new("Sửa lại nội dung", ConversationId: "edit"));
        var old = Confirmation(first, "edit");
        using var stale = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", old);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await ChatAsync(client, new("Hủy yêu cầu này", ConversationId: "edit"));
        using var cancelled = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", Confirmation(edited, "edit"));
        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        await using var db = database.Create(account.User, account.Tenant);
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
    }

    [Fact]
    public async Task Specific_department_delivery_and_recipient_changes_are_checked_again_at_execution()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var model = new ScriptedModel();
        var proposal = JsonSerializer.Serialize(new { plan = new { toolName = "send_department_notification", arguments = new {
            title = "Q4", content = "Chuẩn bị báo cáo", recipientScope = "DEPARTMENTS", departmentIds = new[] { account.Department } } } });
        model.Replies.Enqueue(proposal); model.Replies.Enqueue(proposal);
        await using var factory = Factory(model); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var ready = await ChatAsync(client, new("Gửi phòng điều hành", ConversationId: "department"));
        Assert.Contains("1 người", ready.Response);
        using var sent = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", Confirmation(ready, "department"));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        await using var db = database.Create(account.User, account.Tenant);
        var notifications = await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync();
        var notification = Assert.Single(notifications);
        Assert.NotEqual(account.User, notification.RecipientId);
        Assert.Equal("Announcement", notification.Type);
        var next = await ChatAsync(client, new("Gửi một thông báo mới", ConversationId: "roster-change"));
        db.Users.Add(UserAccount.CreateTenantUser(account.Tenant, "NEW", "new@example.test", "New recipient", "fixture-only", DateTimeOffset.UtcNow, account.Department));
        await db.SaveChangesAsync();
        using var changed = await client.PostAsJsonAsync("/api/v1/ai/actions/execute", Confirmation(next, "roster-change"));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(1, await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.TenantId == account.Tenant));
    }

    [Fact]
    public async Task Business_lookup_chain_uses_real_scoped_queries_and_returns_ids_without_directory_emails()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(account.User, account.Tenant);
        var task = BizFlow.Domain.Tasks.WorkTask.CreateDraft(account.Tenant, account.User, "Budget reconciliation", DateTimeOffset.UtcNow, "Review vouchers");
        db.WorkTasks.Add(task); await db.SaveChangesAsync();
        var model = new ScriptedModel();
        model.Replies.Enqueue("""{"toolCall":{"name":"list_tasks","arguments":{"search":"Budget"}}}""");
        model.Replies.Enqueue(JsonSerializer.Serialize(new { toolCall = new { name = "task_details", arguments = new { taskId = task.Id } } }));
        model.Replies.Enqueue("""{"toolCall":{"name":"list_people","arguments":{"search":"Second"}}}""");
        model.Replies.Enqueue("""{"toolCall":{"name":"list_requests","arguments":{}}}""");
        model.Replies.Enqueue("""{"answerOnly":true,"reply":"Checked authorized records."}""");
        await using var factory = Factory(model); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var response = await ChatAsync(client, new("Review budget work and related people and requests", ConversationId: "lookups"));
        Assert.Equal("AGENT_REPLY", response.RecognizedCommand);
        Assert.Equal(5, model.Inputs.Count);
        var results = model.Inputs.Last().GetProperty("toolResults");
        Assert.Equal(task.Id, results[0].GetProperty("result").GetProperty("items")[0].GetProperty("taskId").GetGuid());
        Assert.Equal("Review vouchers", results[1].GetProperty("result").GetProperty("description").GetString());
        Assert.Equal("Second recipient", results[2].GetProperty("result").GetProperty("items")[0].GetProperty("fullName").GetString());
        Assert.False(results[2].GetProperty("result").GetProperty("items")[0].TryGetProperty("email", out _));
        Assert.Equal(0, results[3].GetProperty("result").GetProperty("total").GetInt32());
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
    }

    [Fact]
    public async Task Read_tool_cannot_disclose_another_tenants_task_even_if_model_supplies_its_id()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(foreign.User, foreign.Tenant);
        var task = BizFlow.Domain.Tasks.WorkTask.CreateDraft(foreign.Tenant, foreign.User, "FOREIGN_SECRET", DateTimeOffset.UtcNow);
        db.WorkTasks.Add(task); await db.SaveChangesAsync();
        var model = new ScriptedModel();
        model.Replies.Enqueue(JsonSerializer.Serialize(new { toolCall = new { name = "task_details", arguments = new { taskId = task.Id } } }));
        model.Replies.Enqueue("""{"answerOnly":true,"reply":"No authorized records."}""");
        await using var factory = Factory(model); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var response = await ChatAsync(client, new("Read this task", ConversationId: "foreign"));
        Assert.Equal("AGENT_REPLY", response.RecognizedCommand);
        var input = model.Inputs.Last().GetProperty("toolResults");
        Assert.True(input[0].GetProperty("result").TryGetProperty("error", out _));
        Assert.DoesNotContain("FOREIGN_SECRET", model.Inputs.Last().GetRawText());
    }

    private static ExecuteAiActionCommand Confirmation(RobotChatResponse response, string conversation)
    {
        var payload = JsonSerializer.SerializeToElement(Assert.Single(response.ActionCards!).Payload);
        return new(payload.GetProperty("toolName").GetString()!, payload.GetProperty("argsJson").GetString()!, true,
            payload.GetProperty("confirmationToken").GetString(), conversation);
    }

    private AuthenticationFactory Factory(ScriptedModel model) => new(database,
        new Dictionary<string, string?> { ["Redis:ConnectionString"] = "none" }, configureServices: services =>
        { services.RemoveAll<IAiModelClient>(); services.AddSingleton<IAiModelClient>(model); });

    private static async Task<RobotChatResponse> ChatAsync(HttpClient client, RobotChatCommand command)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", command);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RobotChatResponse>())!;
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid Department);
    private async Task<Account> SeedAsync(string roleName)
    {
        var seed = await database.SeedTenantAsync(); await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var department = Department.Create(tenant.Id, "OPS", "Điều hành", null, DateTimeOffset.UtcNow);
        db.Add(department); await db.SaveChangesAsync();
        db.Users.Add(UserAccount.CreateTenantUser(tenant.Id, "SECOND", "second@example.test", "Second recipient", "fixture-only", DateTimeOffset.UtcNow, department.Id));
        db.UserRoles.Add(new(user.Id, (await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName)).Id));
        await db.SaveChangesAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Company\" SET \"Status\"='ACTIVE' WHERE \"CompanyId\"={tenant.CompanyId}");
        return new(user.Id, tenant.Id, tenant.TenantKey, department.Id);
    }
    private static async Task LoginAsync(HttpClient client, Account account)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
}
