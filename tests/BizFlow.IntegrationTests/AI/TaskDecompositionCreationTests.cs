using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.AI;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.AI;

[Collection("Postgres")]
public sealed class TaskDecompositionCreationTests(PostgresFixture database)
{
    private const string Password = "Decomposition-test!92483";
    private const string Endpoint = "/api/v1/tasks/ai-decomposition/confirm";

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public async Task Selected_department_creates_and_assigns_real_tasks_with_separate_audits_and_checklists(int count)
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var items = Enumerable.Range(1, count).Select(i => new ConfirmTaskDecompositionItem(
            "Chuẩn bị báo cáo " + i, "Kiểm tra dữ liệu", "Assignment", account.Department, "HIGH", ["Đối soát", "Bàn giao"], SuggestedDurationDays: 2)).ToArray();
        using var response = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand(items));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ConfirmTaskDecompositionResponse>())!;
        Assert.Equal(count, result.TotalCreated);
        await using var db = database.Create(account.User, account.Tenant);
        var tasks = await db.WorkTasks.ToListAsync();
        Assert.Equal(count, tasks.Count); Assert.All(tasks, t => { Assert.Equal(TaskState.Assigned, t.Status); Assert.NotNull(t.Deadline); });
        Assert.Equal(count * 2, await db.TaskChecklistItems.CountAsync());
        var assignments = await db.TaskAssignments.ToListAsync();
        Assert.Equal(count, assignments.Count); Assert.All(assignments, a => Assert.Equal(account.Department, a.DepartmentId));
        var audits = await db.AuditLogs.Where(a => a.ObjectType == "Task").ToListAsync();
        Assert.Equal(count, audits.Count(a => a.Action == "TASK.CREATED"));
        Assert.Equal(count, audits.Count(a => a.Action == "TASK.ASSIGNED"));
        foreach (var audit in audits.Where(a => a.Action == "TASK.CREATED"))
        {
            using var snapshot = JsonDocument.Parse(audit.AfterJson!);
            Assert.Equal("DRAFT", snapshot.RootElement.GetProperty("status").GetString());
            Assert.Equal(2, snapshot.RootElement.GetProperty("checklist").GetArrayLength());
        }
        Assert.Equal(count, await db.Notifications.IgnoreQueryFilters().CountAsync(n => n.TenantId == account.Tenant && n.RecipientId == account.Recipient && n.Type == "TaskAssigned"));
        Assert.Equal(count, (await client.GetFromJsonAsync<BizFlow.Application.Tasks.TaskListPage>("/api/v1/tasks"))!.Total);
    }

    [Fact]
    public async Task No_department_creates_a_draft_and_a_later_invalid_item_rolls_back_the_entire_batch()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var draft = new ConfirmTaskDecompositionItem("Chưa phân công", "Mô tả", "Assignment", null, "MEDIUM", []);
        using var success = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand([draft]));
        Assert.True(success.IsSuccessStatusCode, await success.Content.ReadAsStringAsync());
        await using var db = database.Create(account.User, account.Tenant);
        Assert.Equal(TaskState.Draft, (await db.WorkTasks.SingleAsync()).Status);
        var valid = draft with { Title = "Phải rollback", DepartmentId = account.Department };
        var invalid = draft with { DepartmentId = Guid.NewGuid() };
        using var failure = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand([valid, invalid]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failure.StatusCode);
        Assert.Single(await db.WorkTasks.AsNoTracking().ToListAsync());
        Assert.Empty(await db.TaskAssignments.ToListAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.ObjectType == "Task"));
        Assert.Empty(await db.Notifications.IgnoreQueryFilters().Where(n => n.TenantId == account.Tenant).ToListAsync());
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid Department, Guid Recipient);

    [Fact]
    public async Task Invalid_checklist_returns_validation_error_and_missing_assignment_scope_cannot_create_partial_data()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var input = new ConfirmTaskDecompositionItem("Báo cáo", "Mô tả", "Assignment", account.Department, "HIGH", [""]);
        using var invalid = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand([input]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        await using var db = database.Create(account.User, account.Tenant);
        var scopes = await db.ManagementScopes.Where(s => s.UserId == account.User).ToListAsync();
        db.ManagementScopes.RemoveRange(scopes); await db.SaveChangesAsync();
        using var denied = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand([input with { ChecklistItems = ["Đối soát"] }]));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty(await db.WorkTasks.ToListAsync());
        Assert.Empty(await db.TaskAssignments.ToListAsync());
        Assert.Empty(await db.TaskChecklistItems.ToListAsync());
    }
    private async Task<Account> SeedAsync()
    {
        var seed = await database.SeedTenantAsync(); await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var department = Department.Create(tenant.Id, "OPS", "Điều hành", null, DateTimeOffset.UtcNow);
        db.Add(department); await db.SaveChangesAsync();
        var recipient = UserAccount.CreateTenantUser(tenant.Id, "MANAGER", "manager@example.test", "Test manager", "fixture-only", DateTimeOffset.UtcNow, department.Id);
        db.Users.Add(recipient);
        db.UserRoles.Add(new(user.Id, (await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN")).Id));
        db.UserRoles.Add(new(recipient.Id, (await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER")).Id));
        db.UserRoles.Add(new(user.Id, (await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER")).Id));
        db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, user.Id, department.Id, false, user.Id, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Company\" SET \"Status\"='ACTIVE' WHERE \"CompanyId\"={tenant.CompanyId}");
        return new(user.Id, tenant.Id, tenant.TenantKey, department.Id, recipient.Id);
    }

    [Fact]
    public async Task Mixed_task_and_support_request_batch_preserves_each_record_type_and_department_name_resolution()
    {
        var account = await SeedAsync();
        await using var db = database.Create(account.User, account.Tenant);
        var service = BizFlow.Domain.Services.InternalService.Create(account.Tenant, "OPS", "Operations", null, true, DateTimeOffset.UtcNow);
        db.Services.Add(service); await db.SaveChangesAsync();
        db.ServiceCategories.Add(BizFlow.Domain.Services.ServiceCategory.Create(service.Id, "GENERAL", "General"));
        await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var request = new ConfirmTaskDecompositionItem("Hỗ trợ kỹ thuật", "Kiểm tra thiết bị", "SupportRequest", account.Department, "HIGH", []);
        var task = new ConfirmTaskDecompositionItem("Chuẩn bị báo cáo", "Đối soát số liệu", "Assignment", null, "MEDIUM", ["Kiểm tra"], DepartmentName: "Điều hành");
        using var response = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand([request, task]));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ConfirmTaskDecompositionResponse>())!;
        Assert.Equal(2, result.TotalCreated);
        Assert.Equal("SupportRequest", result.Items[0].Type); Assert.Equal("Assignment", result.Items[1].Type);
        Assert.Single(await db.Requests.ToListAsync());
        Assert.Equal(TaskState.Assigned, (await db.WorkTasks.SingleAsync()).Status);
        Assert.Equal(account.Department, (await db.TaskAssignments.SingleAsync()).DepartmentId);
    }

    [Fact]
    public async Task Missing_support_service_returns_clear_validation_error_without_silently_creating_a_task()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var input = new ConfirmTaskDecompositionItem("Hỗ trợ kỹ thuật", "Kiểm tra thiết bị", "SupportRequest", account.Department, "HIGH", []);
        using var response = await client.PostAsJsonAsync(Endpoint, new ConfirmTaskDecompositionCommand([input]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("AI.SERVICE_REQUIRED", await response.Content.ReadAsStringAsync());
        await using var db = database.Create(account.User, account.Tenant);
        Assert.Empty(await db.WorkTasks.ToListAsync()); Assert.Empty(await db.Requests.ToListAsync());
    }
    private static async Task LoginAsync(HttpClient client, Account account)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = account.Key });
        Assert.True(response.IsSuccessStatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
}
