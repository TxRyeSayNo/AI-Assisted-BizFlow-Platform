using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for task templates with optional recurrence (F8 + F9) and saved list filters
/// (F12).
///
/// The points that matter: managing the catalogue needs the tenant administration permission while
/// reading it only needs membership; creating a task from a template goes through the ordinary task
/// creation command, so it cannot bypass permissions, idempotency or audit; the recurring sweep acts
/// as the template's owner and never queues a backlog; and a saved filter is invisible to everyone
/// but the person who saved it.
/// </summary>
[Collection("Postgres")]
public sealed class TaskTemplateApiTests(PostgresFixture database)
{
    private const string Password = "Template-test!5523";
    private const string Templates = "/api/v1/task-templates";
    private const string Filters = "/api/v1/saved-filters";

    // -------------------------------------------------------------------------------------------
    // F9 — the catalogue and creating a task from a template
    // -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_template_is_created_edited_retired_and_named_once_per_workspace()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var created = (await (await client.PostAsJsonAsync(Templates, new
        {
            name = " Báo cáo tuần ",
            titleTemplate = " Làm báo cáo tuần ",
            description = "Tổng hợp số liệu",
            priority = "HIGH",
            deadlineOffsetDays = 3
        })).Content.ReadFromJsonAsync<TaskTemplateView>())!;

        Assert.Equal("Báo cáo tuần", created.Name);
        Assert.Equal("Làm báo cáo tuần", created.TitleTemplate);
        Assert.Equal("HIGH", created.Priority);
        Assert.Equal(3, created.DeadlineOffsetDays);
        Assert.Null(created.RecurrenceFrequency);
        Assert.Null(created.NextRunAt);
        Assert.False(created.Archived);

        // The name is unique per workspace.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Templates,
            new { name = "báo cáo tuần", titleTemplate = "Khác" })).StatusCode);

        var edited = (await (await client.PutAsJsonAsync($"{Templates}/{created.TemplateId}", new
        {
            name = "Báo cáo tháng",
            titleTemplate = "Làm báo cáo tháng",
            deadlineOffsetDays = 7
        })).Content.ReadFromJsonAsync<TaskTemplateView>())!;
        Assert.Equal("Báo cáo tháng", edited.Name);
        Assert.Null(edited.Description);
        Assert.Equal(7, edited.DeadlineOffsetDays);

        Assert.True((await (await client.PostAsJsonAsync($"{Templates}/{created.TemplateId}/archive",
            new { archived = true })).Content.ReadFromJsonAsync<TaskTemplateView>())!.Archived);
        // A retired template is out of the default listing but still reachable with the flag.
        Assert.Empty((await client.GetFromJsonAsync<TaskTemplateView[]>(Templates))!);
        Assert.Single((await client.GetFromJsonAsync<TaskTemplateView[]>($"{Templates}?includeArchived=true"))!);
        Assert.False((await (await client.PostAsJsonAsync($"{Templates}/{created.TemplateId}/archive",
            new { archived = false })).Content.ReadFromJsonAsync<TaskTemplateView>())!.Archived);
    }

    [Fact]
    public async Task A_task_made_from_a_template_carries_its_fields_and_replays_once()
    {
        // Both roles: COMPANY_ADMIN to manage the catalogue, MANAGER because creating a task needs
        // tasks.create. Using a template is never a way around the permission a task creation needs.
        var a = await SeedAsync("COMPANY_ADMIN,MANAGER");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var template = (await (await client.PostAsJsonAsync(Templates, new
        {
            name = "Kiểm tra định kỳ",
            titleTemplate = "Kiểm tra thiết bị",
            description = "Theo danh mục",
            priority = "HIGH",
            deadlineOffsetDays = 2
        })).Content.ReadFromJsonAsync<TaskTemplateView>())!;

        var key = Guid.NewGuid().ToString();
        var first = await InstantiateAsync(client, template.TemplateId, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var task = (await first.Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal("Kiểm tra thiết bị", task.Title);
        Assert.Equal("DRAFT", task.Status);

        // The same key replays rather than creating a second task.
        var replay = await InstantiateAsync(client, template.TemplateId, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(task.TaskId, (await replay.Content.ReadFromJsonAsync<TaskCreatedView>())!.TaskId);

        await using var db = database.Create(a.User, a.Tenant);
        var stored = await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == task.TaskId);
        Assert.Equal("Theo danh mục", stored.Description);
        Assert.Equal(TaskPriority.High, stored.Priority);
        Assert.NotNull(stored.Deadline);
        Assert.Equal(TaskState.Draft, stored.Status);
        Assert.Equal(a.User, stored.CreatorId);
        // Exactly one task, created through the ordinary command — which also wrote its audit entry.
        Assert.Equal(1, await db.WorkTasks.AsNoTracking().CountAsync(t => t.Title == "Kiểm tra thiết bị"));

        // A retired template cannot be used.
        await client.PostAsJsonAsync($"{Templates}/{template.TemplateId}/archive", new { archived = true });
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await InstantiateAsync(client, template.TemplateId, Guid.NewGuid().ToString())).StatusCode);
    }

    [Fact]
    public async Task Reading_the_catalogue_needs_only_membership_but_managing_it_needs_the_permission()
    {
        var admin = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var adminClient = factory.CreateClient();
        await LoginAsync(adminClient, admin);
        var template = (await (await adminClient.PostAsJsonAsync(Templates,
            new { name = "Việc chung", titleTemplate = "Việc chung" }))
            .Content.ReadFromJsonAsync<TaskTemplateView>())!;

        // A plain employee of the same workspace may look, but not change anything.
        var employee = await SeedAsync("EMPLOYEE", admin);
        using var employeeClient = factory.CreateClient();
        await LoginAsync(employeeClient, employee, "EMP002");
        Assert.Single((await employeeClient.GetFromJsonAsync<TaskTemplateView[]>(Templates))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await employeeClient.PostAsJsonAsync(Templates,
            new { name = "Của tôi", titleTemplate = "Của tôi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employeeClient.PutAsJsonAsync(
            $"{Templates}/{template.TemplateId}", new { name = "Đổi tên", titleTemplate = "Đổi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employeeClient.PostAsJsonAsync(
            $"{Templates}/{template.TemplateId}/archive", new { archived = true })).StatusCode);
        // Reading the catalogue is not authority to use it: creating a task still needs tasks.create.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await InstantiateAsync(employeeClient, template.TemplateId, null)).StatusCode);

        // Another workspace sees none of it.
        var foreign = await SeedAsync("COMPANY_ADMIN");
        using var foreignClient = factory.CreateClient();
        await LoginAsync(foreignClient, foreign);
        Assert.Empty((await foreignClient.GetFromJsonAsync<TaskTemplateView[]>(Templates))!);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignClient.PutAsJsonAsync(
            $"{Templates}/{template.TemplateId}", new { name = "Chiếm", titleTemplate = "Chiếm" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await InstantiateAsync(foreignClient, template.TemplateId, null)).StatusCode);
    }

    [Fact]
    public async Task A_malformed_template_or_recurrence_is_a_refusal_not_a_server_error()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var body in new object[]
        {
            new { name = "   ", titleTemplate = "T" },
            new { name = "N", titleTemplate = "   " },
            new { name = "N", titleTemplate = "T", deadlineOffsetDays = -1 },
            new { name = "N", titleTemplate = "T", deadlineOffsetDays = 400 },
            new { name = "N", titleTemplate = "T", recurrenceFrequency = "YEARLY" },
            new { name = "N", titleTemplate = "T", recurrenceFrequency = "WEEKLY" },
            new { name = "N", titleTemplate = "T", recurrenceFrequency = "WEEKLY", recurrenceDay = 8 },
            new { name = "N", titleTemplate = "T", recurrenceFrequency = "MONTHLY", recurrenceDay = 29 },
            new { name = "N", titleTemplate = "T", recurrenceFrequency = "DAILY", recurrenceHourUtc = 24 },
        })
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                (await client.PostAsJsonAsync(Templates, body)).StatusCode);

        // An unmapped member is refused, like everywhere else on the platform.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Templates,
            new { name = "N", titleTemplate = "T", nope = 1 })).StatusCode);

        await using var db = database.Create(a.User, a.Tenant);
        Assert.False(await db.TaskTemplates.AsNoTracking().AnyAsync());
    }

    // -------------------------------------------------------------------------------------------
    // F8 — the recurring sweep
    // -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_due_schedule_produces_one_draft_per_sweep_and_steps_forward()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var template = (await (await client.PostAsJsonAsync(Templates, new
        {
            name = "Việc hằng ngày",
            titleTemplate = "Dọn hộp thư",
            deadlineOffsetDays = 1,
            recurrenceFrequency = "DAILY",
            recurrenceHourUtc = 0
        })).Content.ReadFromJsonAsync<TaskTemplateView>())!;
        Assert.NotNull(template.NextRunAt);

        // Make it due without touching the clock the application runs on.
        await using var db = database.Create(a.User, a.Tenant);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TaskTemplate\" SET \"NextRunAt\" = now() - interval '1 hour' WHERE \"TaskTemplateId\"={template.TemplateId}");

        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRecurringTaskDispatcher>();

        Assert.Equal(1, await dispatcher.SweepAsync(CancellationToken.None));
        // A second sweep finds nothing: the schedule has already moved on.
        Assert.Equal(0, await dispatcher.SweepAsync(CancellationToken.None));

        var produced = await db.WorkTasks.AsNoTracking().Where(t => t.Title == "Dọn hộp thư").ToListAsync();
        var task = Assert.Single(produced);
        Assert.Equal(TaskState.Draft, task.Status);
        Assert.Equal(a.User, task.CreatorId);
        Assert.NotNull(task.Deadline);
        // Nothing was assigned: the sweep creates work, it does not hand it out.
        Assert.False(await db.TaskAssignments.AsNoTracking().AnyAsync(t => t.TaskId == task.Id));

        var after = await db.TaskTemplates.AsNoTracking().SingleAsync(t => t.Id == template.TemplateId);
        Assert.NotNull(after.LastRunAt);
        Assert.NotNull(after.NextRunAt);
        Assert.True(after.NextRunAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task The_sweep_ignores_a_template_with_no_schedule_or_a_retired_one()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var plain = (await (await client.PostAsJsonAsync(Templates,
            new { name = "Không định kỳ", titleTemplate = "Việc tay" }))
            .Content.ReadFromJsonAsync<TaskTemplateView>())!;
        var retired = (await (await client.PostAsJsonAsync(Templates, new
        {
            name = "Đã nghỉ",
            titleTemplate = "Việc cũ",
            recurrenceFrequency = "DAILY",
            recurrenceHourUtc = 0
        })).Content.ReadFromJsonAsync<TaskTemplateView>())!;

        await using var db = database.Create(a.User, a.Tenant);
        // Try to force both due, then retire the one that has a schedule.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"TaskTemplate\" SET \"NextRunAt\" = now() - interval '1 hour' WHERE \"TaskTemplateId\" IN ({plain.TemplateId},{retired.TemplateId})");
        await client.PostAsJsonAsync($"{Templates}/{retired.TemplateId}/archive", new { archived = true });

        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IRecurringTaskDispatcher>()
            .SweepAsync(CancellationToken.None));
        Assert.False(await db.WorkTasks.AsNoTracking()
            .AnyAsync(t => t.Title == "Việc tay" || t.Title == "Việc cũ"));
    }

    // -------------------------------------------------------------------------------------------
    // F12 — saved filters
    // -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_saved_filter_is_private_to_its_owner_and_unique_per_list()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var saved = (await (await client.PostAsJsonAsync(Filters,
            new { scope = "task", name = " Việc gấp ", query = "?priority=HIGH&status=ASSIGNED" }))
            .Content.ReadFromJsonAsync<SavedFilterView>())!;
        Assert.Equal("TASK", saved.Scope);
        Assert.Equal("Việc gấp", saved.Name);
        Assert.Equal("priority=HIGH&status=ASSIGNED", saved.Query);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Filters,
            new { scope = "TASK", name = "việc gấp", query = "" })).StatusCode);
        // The same name on the other list is a different filter.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Filters,
            new { scope = "REQUEST", name = "Việc gấp", query = "priority=HIGH" })).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<SavedFilterView[]>($"{Filters}?scope=TASK"))!);
        Assert.Single((await client.GetFromJsonAsync<SavedFilterView[]>($"{Filters}?scope=REQUEST"))!);

        var renamed = (await (await client.PutAsJsonAsync($"{Filters}/{saved.SavedFilterId}",
            new { name = "Việc rất gấp", query = "priority=CRITICAL" }))
            .Content.ReadFromJsonAsync<SavedFilterView>())!;
        Assert.Equal("Việc rất gấp", renamed.Name);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Filters}/{saved.SavedFilterId}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<SavedFilterView[]>($"{Filters}?scope=TASK"))!);
        // Deleting is soft, and re-saving the name reuses the row rather than piling up duplicates.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Filters,
            new { scope = "TASK", name = "Việc rất gấp", query = "priority=HIGH" })).StatusCode);
        await using var db = database.Create(a.User, a.Tenant);
        Assert.Equal(1, await db.SavedFilters.AsNoTracking().CountAsync(f => f.Scope == "TASK"));

        // A colleague of the same workspace sees none of it, not even by id.
        var colleague = await SeedAsync("EMPLOYEE", a);
        using var other = factory.CreateClient();
        await LoginAsync(other, colleague, "EMP002");
        Assert.Empty((await other.GetFromJsonAsync<SavedFilterView[]>($"{Filters}?scope=TASK"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"{Filters}/{saved.SavedFilterId}",
            new { name = "Chiếm", query = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"{Filters}/{saved.SavedFilterId}")).StatusCode);
    }

    [Fact]
    public async Task A_saved_filter_refuses_a_bad_scope_name_or_query()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var body in new object[]
        {
            new { scope = "PROJECT", name = "N", query = "" },
            new { scope = "TASK", name = "   ", query = "" },
            new { scope = "TASK", name = "N", query = new string('a', 1001) },
        })
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                (await client.PostAsJsonAsync(Filters, body)).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Filters,
            new { scope = "TASK", name = "N", query = "", nope = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"{Filters}/{Guid.NewGuid()}")).StatusCode);
    }

    // -------------------------------------------------------------------------------------------
    // Flags and authentication
    // -------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_flags_hide_both_features_and_authentication_is_required()
    {
        var a = await SeedAsync("COMPANY_ADMIN,MANAGER");
        await using var disabled = new AuthenticationFactory(database, new Dictionary<string, string?>
        {
            ["Features:TaskTemplates"] = "false",
            ["Features:SavedFilters"] = "false"
        });
        using var off = disabled.CreateClient();
        await LoginAsync(off, a);

        Assert.Equal(HttpStatusCode.NotFound, (await off.GetAsync(Templates)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await off.PostAsJsonAsync(Templates,
            new { name = "N", titleTemplate = "T" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await off.GetAsync($"{Filters}?scope=TASK")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await off.PostAsJsonAsync(Filters,
            new { scope = "TASK", name = "N", query = "" })).StatusCode);
        // With the flags off the task endpoints behave exactly as they did before.
        Assert.Equal(HttpStatusCode.OK, (await off.GetAsync("/api/v1/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await off.PostAsJsonAsync("/api/v1/tasks",
            new { title = "Vẫn tạo được" })).StatusCode);

        await using var enabled = new AuthenticationFactory(database);
        using var anonymous = enabled.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Templates)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Filters)).StatusCode);

        using var client = enabled.CreateClient();
        await LoginAsync(client, a);
        using var response = await client.GetAsync(Filters);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> InstantiateAsync(HttpClient client, Guid templateId, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Templates}/{templateId}/tasks");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    /// <param name="roleNames">
    /// Comma-separated system roles. The seeded grants are narrow — <c>roles.configure</c> belongs
    /// to COMPANY_ADMIN and <c>tasks.create</c> to MANAGER — so a test that both manages the
    /// catalogue and creates a task asks for both rather than assuming one implies the other.
    /// </param>
    /// <param name="shareWith">
    /// When given, the new account joins that existing workspace instead of a new one, which is how
    /// the "a colleague sees nothing of mine" checks get two people in one tenant.
    /// </param>
    private async Task<Account> SeedAsync(string roleNames, Account? shareWith = null)
    {
        var roles = roleNames.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (shareWith is { } host)
        {
            await using var hostDb = database.Create(host.User, host.Tenant);
            var now = DateTimeOffset.UtcNow;
            var department = (await hostDb.Users.SingleAsync(u => u.Id == host.User)).DepartmentId;
            var member = UserAccount.CreateTenantUser(host.Tenant, "EMP002", "emp002@example.test",
                "Đồng nghiệp hai", "fixture-only", now, department);
            hostDb.Add(member);
            await hostDb.SaveChangesAsync();
            foreach (var name in roles)
            {
                var memberRole = await hostDb.Roles.SingleAsync(r => r.IsSystem && r.Name == name);
                hostDb.UserRoles.Add(new(member.Id, memberRole.Id));
                await hostDb.SaveChangesAsync();
            }
            var memberHash = new PasswordHasher<UserAccount>().HashPassword(member, Password);
            await hostDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"User\" SET \"PasswordHash\"={memberHash} WHERE \"UserId\"={member.Id}");
            return host with { User = member.Id };
        }

        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var stamp = DateTimeOffset.UtcNow;

        var dept = Department.Create(tenant.Id, "OURS", "Phòng của chúng tôi", null, stamp);
        db.Add(dept);
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={dept.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        foreach (var name in roles)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == name);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey);
    }

    private static async Task LoginAsync(HttpClient client, Account a, string identifier = "EMP001")
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier, password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key);
}
