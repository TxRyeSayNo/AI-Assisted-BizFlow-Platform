using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for labels (F6): the per-tenant catalogue, the administration boundary, the
/// read policy that gates tagging, tenant isolation, the board filter and the Labels flag.
/// </summary>
[Collection("Postgres")]
public sealed class LabelApiTests(PostgresFixture database)
{
    private const string Password = "Label-test!5190";
    private const string Catalogue = "/api/v1/labels";

    private static string On(Guid taskId) => $"/api/v1/work-items/TASK/{taskId}/labels";

    [Fact]
    public async Task An_administrator_creates_renames_retires_and_restores_a_label()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        using var created = await client.PostAsJsonAsync(Catalogue, new { name = " Gấp ", colour = "b3261e" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var label = (await created.Content.ReadFromJsonAsync<LabelView>())!;
        // The name is trimmed and the colour normalised to #RRGGBB upper case.
        Assert.Equal("Gấp", label.Name);
        Assert.Equal("#B3261E", label.Colour);
        Assert.False(label.Archived);

        // A second label with the same name, in any casing, is a conflict rather than a duplicate.
        using var clash = await client.PostAsJsonAsync(Catalogue, new { name = "gấp", colour = "#000000" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Contains("LABEL.DUPLICATE_NAME", await clash.Content.ReadAsStringAsync());

        var renamed = (await (await client.PutAsJsonAsync($"{Catalogue}/{label.LabelId}",
            new { name = "Ưu tiên cao", colour = "#E8710A" })).Content.ReadFromJsonAsync<LabelView>())!;
        Assert.Equal("Ưu tiên cao", renamed.Name);
        Assert.Equal("#E8710A", renamed.Colour);

        var retired = (await (await client.PutAsJsonAsync($"{Catalogue}/{label.LabelId}/archived",
            new { archived = true })).Content.ReadFromJsonAsync<LabelView>())!;
        Assert.True(retired.Archived);
        // A retired label leaves the default listing but is still there when asked for.
        Assert.DoesNotContain((await client.GetFromJsonAsync<LabelView[]>(Catalogue))!, l => l.LabelId == label.LabelId);
        Assert.Contains((await client.GetFromJsonAsync<LabelView[]>($"{Catalogue}?includeArchived=true"))!,
            l => l.LabelId == label.LabelId);

        var restored = (await (await client.PutAsJsonAsync($"{Catalogue}/{label.LabelId}/archived",
            new { archived = false })).Content.ReadFromJsonAsync<LabelView>())!;
        Assert.False(restored.Archived);
    }

    [Fact]
    public async Task Invalid_names_and_colours_are_refused_before_storage()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var body in new object[]
        {
            new { name = "", colour = "#000000" },
            new { name = "   ", colour = "#000000" },
            new { name = new string('x', 61), colour = "#000000" },
            new { name = "Hợp lệ", colour = "" },
            new { name = "Hợp lệ", colour = "red" },
            new { name = "Hợp lệ", colour = "#12345" },
            new { name = "Hợp lệ", colour = "#1234567" }
        })
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                (await client.PostAsJsonAsync(Catalogue, body)).StatusCode);
    }

    [Fact]
    public async Task A_member_without_the_administration_capability_can_read_but_not_manage()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // Reading the catalogue is fine: labels appear on items they can already open.
        using var listed = await client.GetAsync(Catalogue);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(Catalogue, new { name = "Của tôi", colour = "#000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"{Catalogue}/{Guid.NewGuid()}", new { name = "X", colour = "#000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"{Catalogue}/{Guid.NewGuid()}/archived", new { archived = true })).StatusCode);
    }

    [Fact]
    public async Task Tagging_an_item_is_idempotent_reversible_and_counted()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        var label = await CreateLabelAsync(client, "Khẩn", "#B3261E");

        var attached = await PutLabelAsync(client, a.TaskId, label.LabelId, factory);
        Assert.Equal("TASK", attached.ObjectType);
        Assert.Equal(label.LabelId, Assert.Single(attached.Labels).LabelId);

        // Applying it twice changes nothing.
        Assert.Single((await PutLabelAsync(client, a.TaskId, label.LabelId, factory)).Labels);
        Assert.Equal(1, (await client.GetFromJsonAsync<LabelView[]>(Catalogue))!
            .Single(l => l.LabelId == label.LabelId).UsageCount);

        var detached = (await (await client.DeleteAsync($"{On(a.TaskId)}/{label.LabelId}"))
            .Content.ReadFromJsonAsync<WorkItemLabelsView>())!;
        Assert.Empty(detached.Labels);
        // Detaching twice is not an error either.
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{On(a.TaskId)}/{label.LabelId}")).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<LabelView[]>(Catalogue))!
            .Single(l => l.LabelId == label.LabelId).UsageCount);

        // Re-applying revives the same row rather than creating a second one.
        Assert.Single((await PutLabelAsync(client, a.TaskId, label.LabelId, factory)).Labels);
        await using var db = database.Create(a.User, a.Tenant);
        Assert.Single(await db.WorkItemLabels.AsNoTracking().Where(l => l.ObjectId == a.TaskId).ToListAsync());
    }

    [Fact]
    public async Task A_retired_label_cannot_be_applied_and_an_unknown_one_is_not_found()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        var label = await CreateLabelAsync(client, "Hết dùng", "#5F6368");
        await client.PutAsJsonAsync($"{Catalogue}/{label.LabelId}/archived", new { archived = true });

        using var refused = await client.PutAsJsonAsync($"{On(a.TaskId)}/{label.LabelId}", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains("LABEL.ARCHIVED", await refused.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{On(a.TaskId)}/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.GetAsync($"/api/v1/work-items/PROJECT/{a.TaskId}/labels")).StatusCode);
    }

    [Fact]
    public async Task An_item_outside_the_callers_read_scope_cannot_be_tagged()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var admin = new AuthenticationFactory(database);
        using var adminClient = admin.CreateClient();
        await LoginAsync(adminClient, a);

        // The employee can read the catalogue but the colleague-only task is outside their scope.
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync(On(a.ForeignScopeTaskId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await adminClient.PutAsJsonAsync($"{On(a.ForeignScopeTaskId)}/{Guid.NewGuid()}", new { })).StatusCode);
    }

    [Fact]
    public async Task One_tenants_labels_are_invisible_to_another()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        var mine = await CreateLabelAsync(client, "Của tôi", "#188038");

        using var foreignFactory = new AuthenticationFactory(database);
        using var foreignClient = foreignFactory.CreateClient();
        await LoginAsync(foreignClient, foreign);
        var theirs = await CreateLabelAsync(foreignClient, "Của họ", "#9334E6");

        // Each workspace sees only its own catalogue, even though both names are unique globally.
        var ours = (await client.GetFromJsonAsync<LabelView[]>(Catalogue))!;
        Assert.Contains(ours, l => l.LabelId == mine.LabelId);
        Assert.DoesNotContain(ours, l => l.LabelId == theirs.LabelId);
        // A foreign label cannot be applied to our item.
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"{On(a.TaskId)}/{theirs.LabelId}", new { })).StatusCode);
        // Nor can ours reach their task.
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/v1/work-items/TASK/{foreign.TaskId}/labels/{mine.LabelId}", new { })).StatusCode);
    }

    [Fact]
    public async Task The_board_can_be_narrowed_to_one_label()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        var label = await CreateLabelAsync(client, "Chiến dịch", "#1A73E8");
        await PutLabelAsync(client, a.TaskId, label.LabelId, factory);

        var filtered = (await client.GetFromJsonAsync<TaskBoardView>(
            $"/api/v1/tasks/board?labelId={label.LabelId}"))!;
        var titles = filtered.Columns.SelectMany(c => c.Items).Select(i => i.Title).ToArray();
        Assert.Equal("Việc có nhãn", Assert.Single(titles));

        // Removing the label removes the task from that view.
        await client.DeleteAsync($"{On(a.TaskId)}/{label.LabelId}");
        Assert.Empty((await client.GetFromJsonAsync<TaskBoardView>(
            $"/api/v1/tasks/board?labelId={label.LabelId}"))!.Columns.SelectMany(c => c.Items));

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.GetAsync($"/api/v1/tasks/board?labelId={Guid.Empty}")).StatusCode);
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_labels_and_leaves_tasks_and_the_board_working()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:Labels"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Catalogue)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(Catalogue, new { name = "X", colour = "#000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(On(a.TaskId))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tasks/board")).StatusCode);
    }

    [Fact]
    public async Task Label_endpoints_require_authentication_and_are_never_cached()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Catalogue)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(On(a.TaskId))).StatusCode);

        await LoginAsync(client, a);
        using var response = await client.GetAsync(Catalogue);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private static async Task<LabelView> CreateLabelAsync(HttpClient client, string name, string colour)
    {
        using var response = await client.PostAsJsonAsync(Catalogue, new { name, colour });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LabelView>())!;
    }

    private static async Task<WorkItemLabelsView> PutLabelAsync(HttpClient client, Guid taskId, Guid labelId,
        AuthenticationFactory factory)
    {
        using var response = await client.PutAsJsonAsync($"{On(taskId)}/{labelId}", new { });
        Assert.Null(factory.LastServerError);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkItemLabelsView>())!;
    }

    private async Task<Account> SeedAsync(string? roleName)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var department = Department.Create(tenant.Id, "OURS", "Phòng của chúng tôi", null, now);
        db.Add(department);
        await db.SaveChangesAsync();
        var colleague = UserAccount.CreateTenantUser(tenant.Id, "COLLEAGUE", "colleague@example.test",
            "Đồng nghiệp", "fixture-only", now, department.Id);
        db.Add(colleague);
        await db.SaveChangesAsync();

        var task = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc có nhãn", now);
        var foreignScope = WorkTask.CreateDraft(tenant.Id, colleague.Id, "Việc của người khác", now);
        db.AddRange(task, foreignScope);
        await db.SaveChangesAsync();
        db.AddRange(
            TaskAssignment.Create(task.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(foreignScope.Id, colleague.Id, null, colleague.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={department.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, task.Id, foreignScope.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid TaskId, Guid ForeignScopeTaskId);
}
