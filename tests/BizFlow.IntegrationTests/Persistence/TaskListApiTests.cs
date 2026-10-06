using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed partial class TaskListApiTests(PostgresFixture database)
{
    private const string Password = "Task-list-test!94742";
    private const string Endpoint = "/api/v1/tasks";

    [Fact]
    public async Task Employee_union_is_creator_or_current_target_or_unclaimed_own_department_never_history()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        await LoginAsync(client, a);
        var page = (await client.GetFromJsonAsync<TaskListPage>(Endpoint))!;
        Assert.Equal(new[] { "Direct", "Own", "Queue" }, page.Items.Select(t => t.Title).Order().ToArray());
        Assert.Equal(3, page.Total);
        var queue = Assert.Single(page.Items, t => t.Title == "Queue"); Assert.Null(queue.AssignedUserId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Endpoint}/{queue.TaskId}")).StatusCode);
        Assert.Equal("Our department", queue.AssignedDepartmentName);
        await using var db = database.Create(a.User, a.Tenant);
        // Read scope is current membership, not eligibility to receive new assignments.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Department\" SET \"Status\"='INACTIVE' WHERE \"DepartmentId\"={queue.AssignedDepartmentId}");
        Assert.Contains((await client.GetFromJsonAsync<TaskListPage>(Endpoint))!.Items, t => t.TaskId == queue.TaskId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DepartmentId\"=NULL WHERE \"UserId\"={a.User}");
        Assert.DoesNotContain((await client.GetFromJsonAsync<TaskListPage>(Endpoint))!.Items, t => t.TaskId == queue.TaskId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DepartmentId\"={queue.AssignedDepartmentId} WHERE \"UserId\"={a.User}");
        var claimTime = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='ASSIGNED' WHERE \"TaskId\"={queue.TaskId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"UserId\"={a.Colleague}, \"AcceptedAt\"={claimTime} WHERE \"TaskId\"={queue.TaskId} AND \"EndedAt\" IS NULL");
        var after = (await client.GetFromJsonAsync<TaskListPage>(Endpoint))!;
        Assert.Equal(2, after.Total); Assert.DoesNotContain(after.Items, t => t.TaskId == queue.TaskId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Endpoint}/{queue.TaskId}")).StatusCode);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"DepartmentId\"=NULL WHERE \"UserId\"={a.User}");
        Assert.Equal(2, (await client.GetFromJsonAsync<TaskListPage>(Endpoint))!.Total);
    }

    [Fact]
    public async Task Manager_scope_is_live_and_matches_current_department_or_user_target_department()
    {
        var a = await SeedAsync("MANAGER");
        await using var db = database.Create(a.User, a.Tenant);
        var child = Department.Create(a.Tenant, "CHILD", "Managed descendant", a.OtherDepartment, DateTimeOffset.UtcNow);
        db.Add(child); await db.SaveChangesAsync();
        var descendant = WorkTask.CreateDraft(a.Tenant, a.Colleague, "Managed descendant", DateTimeOffset.UtcNow);
        db.Add(descendant); await db.SaveChangesAsync();
        db.Add(TaskAssignment.Create(descendant.Id, a.Colleague, child.Id, null, DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        var scope = ManagementScope.Create(a.Tenant, a.User, a.OtherDepartment, true, a.User, DateTimeOffset.UtcNow);
        db.Add(scope); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var page = (await client.GetFromJsonAsync<TaskListPage>(Endpoint))!;
        Assert.Equal(new[] { "Direct", "Managed department", "Managed descendant", "Managed user", "Own", "Queue" }, page.Items.Select(t => t.Title).Order().ToArray());
        foreach (var task in page.Items.Where(t => t.Title.StartsWith("Managed")))
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Endpoint}/{task.TaskId}")).StatusCode);
        // Test-only scope-reconfiguration fixture; production scope administration is not exposed yet.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ManagementScope\" WHERE \"ManagementScopeId\"={scope.Id}");
        var after = (await client.GetFromJsonAsync<TaskListPage>(Endpoint))!;
        Assert.Equal(3, after.Total); Assert.DoesNotContain(after.Items, t => t.Title.StartsWith("Managed"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Endpoint}/{descendant.Id}")).StatusCode);
    }

    [Fact]
    public async Task Tenant_grant_filters_foreign_deleted_rows_search_and_counts_despite_spoofed_context()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());
        using var response = await client.GetAsync(Endpoint + $"?tenantId={foreign.Tenant}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<TaskListPage>())!;
        Assert.Equal(8, page.Total); Assert.Equal(8, page.Items.Select(t => t.TaskId).Distinct().Count());
        Assert.All(page.Items, t => Assert.Contains(t.CreatorId, new[] { a.User, a.Colleague }));
        Assert.DoesNotContain(page.Items, t => t.Title == "Deleted");
        Assert.Empty((await client.GetFromJsonAsync<TaskListPage>(Endpoint + "?search=Deleted"))!.Items);
        await using var db = database.Create(foreign.User, foreign.Tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Title\"='Foreign sentinel' WHERE \"TenantId\"={foreign.Tenant} AND \"DeletedAt\" IS NULL");
        Assert.Equal(0, (await client.GetFromJsonAsync<TaskListPage>(Endpoint + "?search=Foreign"))!.Total);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Custom_grants_work_without_role_name_authority_and_revocation_is_immediate_and_audited()
    {
        var a = await SeedAsync(null);
        await using var db = database.Create(a.User, a.Tenant);
        var role = Role.CreateCustom(a.Tenant, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); await db.SaveChangesAsync(); db.UserRoles.Add(new(a.User, role.Id)); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        await LoginAsync(client, a);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        var permission = await db.Permissions.SingleAsync(p => p.Code == "tasks.read.own");
        var grant = new RolePermission(role.Id, permission.Id); db.RolePermissions.Add(grant); await db.SaveChangesAsync();
        Assert.Equal("Own", Assert.Single((await client.GetFromJsonAsync<TaskListPage>(Endpoint))!.Items).Title);
        db.RolePermissions.Remove(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.ActorId == a.User && x.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Fact]
    public async Task Paging_search_priority_and_canonical_status_are_validated_and_combined()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var first = (await client.GetFromJsonAsync<TaskListPage>(Endpoint + "?pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<TaskListPage>(Endpoint + "?pageSize=2&page=2"))!;
        Assert.Equal(8, first.Total); Assert.Equal(2, first.Items.Count);
        Assert.Empty(first.Items.Select(t => t.TaskId).Intersect(second.Items.Select(t => t.TaskId)));
        var filter = (await client.GetFromJsonAsync<TaskListPage>(Endpoint + "?search=%20own%20&priority=CRITICAL&status=DRAFT"))!;
        Assert.Equal("Own", Assert.Single(filter.Items).Title);
        Assert.Empty((await client.GetFromJsonAsync<TaskListPage>(Endpoint + "?search=%25"))!.Items);
        foreach (var status in Enum.GetValues<TaskState>())
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint + "?status=" + TaskListCodes.State(status))).StatusCode);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=2147483647&pageSize=100", "?status=INPROGRESS", "?status=0", "?priority=URGENT", "?search=" + new string('a', 201) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + query)).StatusCode);
    }

    private async Task<Account> SeedAsync(string? roleName)
    {
        var seed = await database.SeedTenantAsync(); await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync(); var now = DateTimeOffset.UtcNow;
        var ours = Department.Create(tenant.Id, "OURS", "Our department", null, now);
        var other = Department.Create(tenant.Id, "OTHER", "Other department", null, now);
        db.AddRange(ours, other); await db.SaveChangesAsync();
        var colleague = UserAccount.CreateTenantUser(tenant.Id, "COLLEAGUE", "colleague@example.test", "Colleague", "fixture-only", now, ours.Id);
        var managed = UserAccount.CreateTenantUser(tenant.Id, "MANAGED", "managed@example.test", "Managed colleague", "fixture-only", now, other.Id);
        db.AddRange(colleague, managed); await db.SaveChangesAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={ours.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);
        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync();
        }
        var names = new[] { "Own", "Direct", "Queue", "Claimed", "Ended", "Managed department", "Managed user", "Unrelated", "Deleted" };
        foreach (var name in names)
        {
            var task = WorkTask.CreateDraft(tenant.Id, name == "Own" ? user.Id : colleague.Id, name, now,
                priority: name == "Own" ? TaskPriority.Critical : TaskPriority.Low);
            db.Add(task); await db.SaveChangesAsync();
            Guid? targetUser = name switch { "Direct" or "Ended" => user.Id, "Claimed" => colleague.Id, "Managed user" => managed.Id, _ => null };
            Guid? department = name switch { "Queue" or "Claimed" => ours.Id, "Managed department" => other.Id, _ => null };
            if (targetUser is not null || department is not null)
            {
                var assignment = TaskAssignment.Create(task.Id, user.Id, department, targetUser, now);
                db.Add(assignment); await db.SaveChangesAsync();
                if (name == "Ended") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TaskAssignment\" SET \"EndedAt\"={DateTimeOffset.UtcNow} WHERE \"TaskAssignmentId\"={assignment.Id}");
            }
            if (name == "Deleted") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"DeletedAt\"={DateTimeOffset.UtcNow} WHERE \"TaskId\"={task.Id}");
        }
        return new(user.Id, tenant.Id, tenant.TenantKey, colleague.Id, other.Id);
    }
    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
    private sealed record Account(Guid User, Guid Tenant, string Key, Guid Colleague, Guid OtherDepartment);
}
