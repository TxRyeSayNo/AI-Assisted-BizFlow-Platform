using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Workflows;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Workflows;

[Collection("Postgres")]
public sealed class WorkflowLibraryTests(PostgresFixture database)
{
    private const string Password = "Workflow-test-only!9247";
    private const string Endpoint = "/api/v1/workflows";

    [Fact]
    public async Task Next_version_is_empty_and_audited_without_mutating_published_history()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(account.UserId, account.TenantId);
        var original = await WorkflowPersistenceTests.SeedAsync(db, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkflowVersion" SET "Status"='PUBLISHED', "PublishedAt"=now() WHERE "WorkflowVersionId"={original.Version};
            UPDATE "Workflow" SET "Status"='ACTIVE' WHERE "WorkflowId"={original.Workflow};
            """);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var before = await client.GetStringAsync($"/api/v1/workflow-versions/{original.Version}");
        using var response = await client.PostAsJsonAsync($"{Endpoint}/{original.Workflow}/versions", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var row = (await response.Content.ReadFromJsonAsync<WorkflowRow>())!;
        Assert.Equal(2, row.LatestVersionNo); Assert.Equal("DRAFT", row.LatestVersionStatus); Assert.Equal("ACTIVE", row.Status);
        Assert.Equal($"/api/v1/workflow-versions/{row.LatestVersionId}", response.Headers.Location!.OriginalString);
        var created = (await client.GetFromJsonAsync<WorkflowVersionView>(response.Headers.Location))!;
        Assert.Empty(created.Steps); Assert.Empty(created.Transitions); Assert.Empty(created.Definition.EnumerateObject()); Assert.Null(created.PublishedAt);
        Assert.Equal(before, await client.GetStringAsync($"/api/v1/workflow-versions/{original.Version}"));
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "WORKFLOW.VERSION_DRAFT_CREATED");
        Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(account.TenantId, audit.TenantId);
        Assert.Equal("WorkflowVersion", audit.ObjectType); Assert.Equal(row.LatestVersionId, audit.ObjectId); Assert.Null(audit.BeforeJson);
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(original.Workflow, after.RootElement.GetProperty("workflowId").GetGuid());
        Assert.Equal(2, after.RootElement.GetProperty("versionNo").GetInt32());
    }

    [Fact]
    public async Task Concurrent_next_versions_receive_distinct_increasing_numbers_and_matching_audits()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var root = await CreateAsync(client, "Concurrent versions");
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync($"{Endpoint}/{root.WorkflowId}/versions", new { })));
        var numbers = new List<int>();
        foreach (var response in responses)
        {
            using (response) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); numbers.Add((await response.Content.ReadFromJsonAsync<WorkflowRow>())!.LatestVersionNo!.Value); }
        }
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, numbers.Order());
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(6, await db.WorkflowVersions.CountAsync());
        Assert.Equal(5, await db.AuditLogs.CountAsync(a => a.Action == "WORKFLOW.VERSION_DRAFT_CREATED"));
    }

    [Fact]
    public async Task Next_version_rejects_client_authority_graphs_and_number_overflow()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var root = await CreateAsync(client, "Version validation");
        var endpoint = $"{Endpoint}/{root.WorkflowId}/versions";
        object[] invalid = [new { tenantId = account.TenantId }, new { versionNo = 100 }, new { status = "PUBLISHED" },
            new { definition = new { } }, new { steps = Array.Empty<object>() }, new { sourceVersionId = root.LatestVersionId }];
        foreach (var body in invalid) Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(endpoint, body)).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Single(await db.WorkflowVersions.ToListAsync());
        db.WorkflowVersions.Add(WorkflowVersion.CreateDraft(root.WorkflowId, int.MaxValue));
        await db.SaveChangesAsync();
        using var overflow = await client.PostAsJsonAsync(endpoint, new { });
        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode); Assert.Contains("WORKFLOW.VERSION_LIMIT", await overflow.Content.ReadAsStringAsync());
        Assert.Equal(2, await db.WorkflowVersions.CountAsync());
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "WORKFLOW.VERSION_DRAFT_CREATED"));
    }

    [Fact]
    public async Task Failed_next_version_audit_rolls_back_insert_and_does_not_consume_the_number()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var root = await CreateAsync(client, "Rollback next version");
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_next_version_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action" = 'WORKFLOW.VERSION_DRAFT_CREATED' THEN RAISE EXCEPTION 'test-only audit failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_next_version_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_next_version_audit_failure();
            """);
        try
        {
            using var failed = await client.PostAsJsonAsync($"{Endpoint}/{root.WorkflowId}/versions", new { });
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.DoesNotContain("test-only audit", await failed.Content.ReadAsStringAsync());
            Assert.Single(await db.WorkflowVersions.ToListAsync());
            Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "WORKFLOW.VERSION_DRAFT_CREATED"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_next_version_audit_failure ON \"AuditLog\"; DROP FUNCTION test_next_version_audit_failure();"); }
        using var retry = await client.PostAsJsonAsync($"{Endpoint}/{root.WorkflowId}/versions", new { });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(2, (await retry.Content.ReadFromJsonAsync<WorkflowRow>())!.LatestVersionNo);
    }

    [Fact]
    public async Task Creation_commits_first_draft_and_actor_audit_without_publishing_or_seeding_steps()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        using var response = await client.PostAsJsonAsync(Endpoint, new { name = "  Access requests  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var row = (await response.Content.ReadFromJsonAsync<WorkflowRow>())!;
        Assert.Equal("Access requests", row.Name); Assert.Equal("REQUEST", row.BusinessType);
        Assert.Equal("DRAFT", row.Status); Assert.Equal("DRAFT", row.LatestVersionStatus); Assert.Equal(1, row.LatestVersionNo);
        Assert.Equal($"/api/v1/workflow-versions/{row.LatestVersionId}", response.Headers.Location?.OriginalString);
        var version = (await client.GetFromJsonAsync<WorkflowVersionView>(response.Headers.Location))!;
        Assert.Equal(row.WorkflowId, version.WorkflowId); Assert.Null(version.PublishedAt);
        Assert.Empty(version.Steps); Assert.Empty(version.Transitions); Assert.Equal(JsonValueKind.Object, version.Definition.ValueKind);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Single(await db.Workflows.ToListAsync()); Assert.Single(await db.WorkflowVersions.ToListAsync());
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "WORKFLOW.DRAFT_CREATED");
        Assert.Equal(row.WorkflowId, audit.ObjectId); Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(account.TenantId, audit.TenantId);
        Assert.Null(audit.BeforeJson); Assert.Equal("Workflow", audit.ObjectType);
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(row.LatestVersionId, after.RootElement.GetProperty("versionId").GetGuid());
        Assert.Equal("DRAFT", after.RootElement.GetProperty("status").GetString());
        // Name is deliberately not unique in Appendix C; do not invent that restriction.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint, new { name = row.Name, businessType = "TASK" })).StatusCode);
    }

    [Theory]
    [InlineData("MANAGER")]
    [InlineData("EMPLOYEE")]
    public async Task Tenant_system_read_grants_do_not_authorize_configuration(string role)
    {
        var account = await SeedAsync(role);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Denied" })).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/versions", new { })).StatusCode);
        Assert.Empty(await db.Workflows.ToListAsync());
    }

    [Fact]
    public async Task Live_permissions_not_role_names_control_both_reads_and_creation()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Denied" })).StatusCode);
        var catalog = await db.Permissions.Where(p => p.Code == "workflows.read" || p.Code == "workflows.configure").ToListAsync();
        var grants = catalog.Select(p => new RolePermission(role.Id, p.Id)).ToArray();
        db.RolePermissions.AddRange(grants); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        var created = await CreateAsync(client, "Allowed");
        db.RolePermissions.RemoveRange(grants); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/workflow-versions/{created.LatestVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Denied after revoke" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Endpoint}/{created.WorkflowId}/versions", new { })).StatusCode);
        Assert.Single(await db.Workflows.ToListAsync());
        Assert.Equal(6, await db.AuditLogs.CountAsync(a => a.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Fact]
    public async Task Tenant_filters_cover_rows_counts_and_complete_version_graph_and_ignore_forged_query_context()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync("COMPANY_ADMIN");
        await using var ownDb = database.Create(a.UserId, a.TenantId);
        var own = await WorkflowPersistenceTests.SeedAsync(ownDb, a.TenantId);
        await using var foreignDb = database.Create(b.UserId, b.TenantId);
        var foreign = await WorkflowPersistenceTests.SeedAsync(foreignDb, b.TenantId);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        var page = (await client.GetFromJsonAsync<WorkflowPage>(Endpoint + $"?tenantId={b.TenantId}"))!;
        Assert.Equal(1, page.Total); Assert.Equal(own.Workflow, Assert.Single(page.Items).WorkflowId);
        var version = (await client.GetFromJsonAsync<WorkflowVersionView>($"/api/v1/workflow-versions/{own.Version}"))!;
        Assert.Equal(own.Step, Assert.Single(version.Steps).StepId); Assert.Equal(own.Transition, Assert.Single(version.Transitions).TransitionId);
        Assert.Equal(JsonValueKind.Object, version.Steps[0].Config.ValueKind); Assert.Null(version.Transitions[0].Guard);
        foreach (var id in new[] { foreign.Version, Guid.NewGuid() })
        {
            using var response = await client.GetAsync($"/api/v1/workflow-versions/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Test workflow", await response.Content.ReadAsStringAsync());
        }
        foreach (var id in new[] { foreign.Workflow, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Endpoint}/{id}/versions", new { })).StatusCode);
        Assert.Single(await foreignDb.WorkflowVersions.ToListAsync());
        Assert.Equal(4, await ownDb.AuditLogs.CountAsync(audit => audit.Action == "SECURITY.ACCESS_DENIED"));
        Assert.All(await ownDb.AuditLogs.Where(audit => audit.Action == "SECURITY.ACCESS_DENIED").ToListAsync(),
            audit => Assert.DoesNotContain(foreign.Version.ToString(), audit.MetadataJson!));
    }

    [Fact]
    public async Task Paging_literal_search_and_validation_do_not_invent_workflow_states_or_accept_overposted_graphs()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var first = await CreateAsync(client, "A literal % draft"); await CreateAsync(client, "B request", "REQUEST");
        var page = (await client.GetFromJsonAsync<WorkflowPage>(Endpoint + "?pageSize=1"))!;
        Assert.Equal(2, page.Total); Assert.Equal(first.WorkflowId, Assert.Single(page.Items).WorkflowId);
        Assert.NotEqual(first.WorkflowId, Assert.Single((await client.GetFromJsonAsync<WorkflowPage>(Endpoint + "?pageSize=1&page=2"))!.Items).WorkflowId);
        Assert.Equal(first.WorkflowId, Assert.Single((await client.GetFromJsonAsync<WorkflowPage>(Endpoint + "?search=%25&businessType=TASK&status=DRAFT"))!.Items).WorkflowId);
        Assert.Empty((await client.GetFromJsonAsync<WorkflowPage>(Endpoint + "?status=ACTIVE"))!.Items);
        foreach (var query in new[] { "?page=0", "?pageSize=101", "?page=2147483647&pageSize=100", "?status=PUBLISHED", "?businessType=OTHER", "?search=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + query)).StatusCode);
        object[] invalid = [new { name = " " }, new { name = new string('x', 201) }, new { name = "Bad type", businessType = "task" },
            new { name = "Injected", status = "ACTIVE" }, new { name = "Injected", tenantId = Guid.NewGuid() }, new { name = "Unimplemented graph", steps = Array.Empty<object>() }];
        foreach (var body in invalid)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint, body)).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(2, await db.Workflows.CountAsync()); Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "WORKFLOW.DRAFT_CREATED"));
    }

    [Fact]
    public async Task Database_failure_rolls_back_workflow_version_and_audit_together()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(account.UserId, account.TenantId);
        // Fault injection in the disposable database only; no production failure switch.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_workflow_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF EXISTS (SELECT 1 FROM "Workflow" WHERE "WorkflowId" = NEW."WorkflowId" AND "Name" = 'Rollback sentinel')
                THEN RAISE EXCEPTION 'test-only storage failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_workflow_failure AFTER INSERT ON "WorkflowVersion" FOR EACH ROW EXECUTE FUNCTION test_workflow_failure();
            """);
        try
        {
            await using var factory = new AuthenticationFactory(database);
            using var client = factory.CreateClient(); await LoginAsync(client, account);
            using var response = await client.PostAsJsonAsync(Endpoint, new { name = "Rollback sentinel" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("test-only storage", await response.Content.ReadAsStringAsync());
            Assert.Empty(await db.Workflows.ToListAsync()); Assert.Empty(await db.WorkflowVersions.ToListAsync());
            Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "WORKFLOW.DRAFT_CREATED"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_workflow_failure ON \"WorkflowVersion\"; DROP FUNCTION test_workflow_failure();"); }
    }

    [Fact]
    public async Task Platform_plane_and_suspended_tenant_cannot_access_tenant_library()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await using var platformDb = database.Create(database.PlatformUserId, null);
        var user = await platformDb.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await platformDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\" = {hash} WHERE \"UserId\" = {user.Id}");
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Platform bypass" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/versions", new { })).StatusCode);
        var account = await SeedAsync("COMPANY_ADMIN"); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\" = 'SUSPENDED' WHERE \"TenantId\" = {account.TenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Endpoint, new { name = "Suspended" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/versions", new { })).StatusCode);
    }

    private async Task<Account> SeedAsync(string? roleName = null)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
            UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
            """);
        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync();
        }
        return new(user.Id, tenant.Id, tenant.TenantKey);
    }

    private static async Task LoginAsync(HttpClient client, Account account)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private static async Task<WorkflowRow> CreateAsync(HttpClient client, string name, string type = "TASK")
    {
        using var response = await client.PostAsJsonAsync(Endpoint, new { name, businessType = type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkflowRow>())!;
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key);
}
