using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Services;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Services;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Workflows;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class ServiceCatalogApiTests(PostgresFixture database)
{
    private const string Endpoint = "/api/v1/services";
    private const string Password = "Service-test-only!9247";
    [Fact]
    public async Task Creation_commits_service_initial_categories_and_actual_actor_audit_together()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        using var response = await client.PostAsJsonAsync(Endpoint, new { code = " IT ", name = " Help ", description = "<script>plain text</script>",
            categories = new[] { new { code = " DESKTOP ", name = " Devices ", active = true }, new { code = "MOBILE", name = "Phones", active = false } } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var row = (await response.Content.ReadFromJsonAsync<ServiceRow>())!;
        Assert.Equal("IT", row.Code); Assert.Equal("Help", row.Name); Assert.Equal("ACTIVE", row.Status);
        Assert.Equal("<script>plain text</script>", row.Description); Assert.Equal(2, row.Categories.Count);
        Assert.Equal("DESKTOP", row.Categories[0].Code); Assert.Equal("INACTIVE", row.Categories[1].Status);
        Assert.Null(row.ActiveWorkflowVersionId); Assert.Null(row.ActiveSlaVersionId);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Single(await db.Services.ToListAsync()); Assert.Equal(2, await db.ServiceCategories.CountAsync());
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "SERVICE.CREATED");
        Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(account.TenantId, audit.TenantId); Assert.Equal(row.ServiceId, audit.ObjectId);
        using var details = JsonDocument.Parse(audit.AfterJson!); Assert.Equal(2, details.RootElement.GetProperty("categories").GetArrayLength());
        var listed = (await client.GetFromJsonAsync<ServicePage>(Endpoint))!; Assert.Equal(row.ServiceId, Assert.Single(listed.Items).ServiceId);
    }
    [Fact]
    public async Task Concurrent_duplicate_codes_return_one_safe_conflict_without_partial_categories_or_audit()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        var body = new { code = "DUP", name = "Duplicate", categories = new[] { new { code = "CATEGORY", name = "Category" } } };
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Endpoint, body), client.PostAsJsonAsync(Endpoint, body));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains("SERVICE.DUPLICATE_CODE", await conflict.Content.ReadAsStringAsync()); Assert.DoesNotContain("IX_Service", await conflict.Content.ReadAsStringAsync());
        foreach (var response in responses) response.Dispose();
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Single(await db.Services.ToListAsync()); Assert.Single(await db.ServiceCategories.ToListAsync());
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "SERVICE.CREATED").ToListAsync());
    }
    [Theory]
    [InlineData("MANAGER")]
    [InlineData("EMPLOYEE")]
    public async Task Tenant_members_can_read_but_default_non_admin_roles_cannot_create(string roleName)
    {
        var account = await SeedAsync(roleName); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { code = "NO", name = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostCategoryAsync(client, Guid.NewGuid(), "\"1\"", new { code = "NO", name = "Denied" })).StatusCode);
    }
    [Fact]
    public async Task Role_names_do_not_grant_creation_and_live_custom_permission_changes_take_effect()
    {
        var account = await SeedAsync(); await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Endpoint, new { code = "NO", name = "Anonymous" })).StatusCode);
        await LoginAsync(client, account); await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { code = "NO", name = "Denied" })).StatusCode);
        var grant = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "service.create")).Id);
        db.RolePermissions.Add(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint, new { code = "YES", name = "Allowed" })).StatusCode);
        db.RolePermissions.Remove(grant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { code = "NO", name = "Revoked" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
    }
    [Fact]
    public async Task Listing_filters_paging_and_nested_categories_cannot_escape_the_session_tenant()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync();
        await using var own = database.Create(a.UserId, a.TenantId); await using var other = database.Create(b.UserId, b.TenantId);
        foreach (var code in new[] { "A", "B%_", "C" })
        { var service = InternalService.Create(a.TenantId, code, "Support " + code, null, code != "C", DateTimeOffset.UtcNow); own.AddRange(service, ServiceCategory.Create(service.Id, "CAT", "Own category")); }
        var foreign = InternalService.Create(b.TenantId, "A", "Foreign confidential", null, true, DateTimeOffset.UtcNow);
        other.AddRange(foreign, ServiceCategory.Create(foreign.Id, "CAT", "Foreign category")); await other.SaveChangesAsync(); await own.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        var page = (await client.GetFromJsonAsync<ServicePage>(Endpoint + $"?tenantId={b.TenantId}&page=2&pageSize=1"))!;
        Assert.Equal(3, page.Total); Assert.Equal("B%_", Assert.Single(page.Items).Code); Assert.Equal("Own category", Assert.Single(page.Items[0].Categories).Name);
        Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint + "?search=%25_"))!.Items);
        Assert.Equal(2, (await client.GetFromJsonAsync<ServicePage>(Endpoint + "?status=ACTIVE"))!.Total);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint + $"?tenantId={b.TenantId}", new { code = "NEW", name = "Own" })).StatusCode);
        Assert.Single(await other.Services.ToListAsync()); Assert.Equal(4, await own.Services.CountAsync());
        foreach (var query in new[] { "page=0", "pageSize=0", "pageSize=101", "page=2147483647&pageSize=100", "status=PUBLISHED" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + "?" + query)).StatusCode);
    }
    [Fact]
    public async Task Invalid_metadata_duplicate_categories_and_unsupported_fields_are_rejected_atomically()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        object[] invalid = [new { code = " ", name = "Help" }, new { code = "IT", name = new string('x', 201) },
            new { code = "IT", name = "Help", status = "DRAFT" }, new { code = "IT", name = "Help", tenantId = account.TenantId },
            new { code = "IT", name = "Help", activeWorkflowVersionId = Guid.NewGuid() },
            new { code = "IT", name = "Help", categories = new[] { new { code = "C", name = "One" }, new { code = " C ", name = "Two" } } },
            new { code = "IT", name = "Help", categories = new object?[] { null } },
            new { code = "IT", name = "Help", categories = new[] { new { code = "C", name = "One", serviceId = Guid.NewGuid() } } },
            new { code = "IT", name = "Help", description = "bad\0text" }];
        foreach (var body in invalid) Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint, body)).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Empty(await db.Services.ToListAsync()); Assert.Empty(await db.ServiceCategories.ToListAsync());
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "SERVICE.CREATED"));
    }
    [Fact]
    public async Task Audit_failure_rolls_back_service_and_all_initial_categories()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_service_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action"='SERVICE.CREATED' THEN RAISE EXCEPTION 'test-only audit failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_service_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_service_audit_failure();
            """);
        try
        {
            await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
            using var response = await client.PostAsJsonAsync(Endpoint, new { code = "IT", name = "Help", categories = new[] { new { code = "C", name = "Category" } } });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.DoesNotContain("test-only", await response.Content.ReadAsStringAsync());
            Assert.Empty(await db.Services.ToListAsync()); Assert.Empty(await db.ServiceCategories.ToListAsync());
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_service_audit_failure ON \"AuditLog\"; DROP FUNCTION test_service_audit_failure();"); }
    }
    [Fact]
    public async Task Platform_accounts_and_suspended_workspaces_cannot_use_the_tenant_catalog()
    {
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        await using var platform = database.Create(database.PlatformUserId, null);
        var user = await platform.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await platform.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { code = "NO", name = "Platform" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutServiceAsync(client, Guid.NewGuid(), "\"1\"", new {code="NO",name="Platform",active=true})).StatusCode);
        var account = await SeedAsync("COMPANY_ADMIN"); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\"='SUSPENDED' WHERE \"TenantId\"={account.TenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Endpoint, new { code = "NO", name = "Suspended" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutServiceAsync(client, Guid.NewGuid(), "\"1\"", new {code="NO",name="Suspended",active=true})).StatusCode);
    }

    [Fact]
    public async Task Adding_category_preserves_existing_configuration_and_advances_ETag_even_with_an_unchanged_clock()
    {
        var now = new DateTimeOffset(DateTime.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database, configureServices: services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FrozenClock(now)); });
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        using var created = await client.PostAsJsonAsync(Endpoint, new { code = "IT", name = "Help", description = "Kept", active = false,
            categories = new[] { new { code = "OLD", name = "Original" } } });
        var original = (await created.Content.ReadFromJsonAsync<ServiceRow>())!; Assert.Equal(original.ETag, created.Headers.ETag!.ToString());
        await using var db = database.Create(account.UserId, account.TenantId);
        var workflow = WorkflowDefinition.CreateDraft(account.TenantId, "Fixture workflow", WorkflowBusinessType.Request, now);
        var workflowVersion = WorkflowVersion.CreateDraft(workflow.Id, 1);
        var profile = SlaProfile.CreateDraft(account.TenantId, "Fixture SLA");
        var calendar = BusinessCalendar.Create(account.TenantId, "UTC", """{"monday":[{"start":"08:00","end":"17:00"}]}""");
        var sla = SlaVersion.CreateSnapshot(profile.Id, 1, 60, 45, calendar.Id);
        db.AddRange(workflow, workflowVersion, profile, calendar, sla); await db.SaveChangesAsync();
        // Test-only binding fixture. No production publication/binding bypass is exposed.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkflowVersion" SET "Status"='PUBLISHED', "PublishedAt"={now} WHERE "WorkflowVersionId"={workflowVersion.Id};
            UPDATE "Service" SET "ActiveWorkflowVersionId"={workflowVersion.Id}, "ActiveSLAVersionId"={sla.Id} WHERE "ServiceId"={original.ServiceId};
            """);
        var before = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items);
        using var response = await PostCategoryAsync(client, before.ServiceId, before.ETag, new { code = " NEW ", name = " New category ", active = false });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var after = (await response.Content.ReadFromJsonAsync<ServiceRow>())!;
        Assert.NotEqual(before.ETag, after.ETag); Assert.Equal(after.ETag, response.Headers.ETag!.ToString());
        Assert.Equal(before.ServiceId, after.ServiceId); Assert.Equal(before.Code, after.Code); Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Description, after.Description); Assert.Equal(before.Status, after.Status); Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(workflowVersion.Id, after.ActiveWorkflowVersionId); Assert.Equal(sla.Id, after.ActiveSlaVersionId);
        Assert.Equal(2, after.Categories.Count); Assert.Contains(after.Categories, c => c.ServiceCategoryId == original.Categories[0].ServiceCategoryId);
        var added = Assert.Single(after.Categories, c => c.Code == "NEW"); Assert.Equal("INACTIVE", added.Status);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "SERVICE.CATEGORY_CREATED");
        Assert.Equal(added.ServiceCategoryId, audit.ObjectId); Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(account.TenantId, audit.TenantId);
        using var auditJson = JsonDocument.Parse(audit.AfterJson!); Assert.Equal(before.ServiceId, auditJson.RootElement.GetProperty("serviceId").GetGuid());
        Assert.Equal(now, (await db.Services.AsNoTracking().SingleAsync()).UpdatedAt);
        Assert.Equal(after.ETag, Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items).ETag);
    }
    [Fact]
    public async Task Category_creation_requires_a_strong_current_tag_and_rejects_duplicates_without_mutation()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        var body = new { code = "CATEGORY", name = "Category" };
        foreach (var tag in new string?[] { null, "*", "1", "W/\"1\"", "\"1\", \"2\"" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostCategoryAsync(client, row.ServiceId, tag, body)).StatusCode);
        using var success = await PostCategoryAsync(client, row.ServiceId, row.ETag, body); Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        var saved = (await success.Content.ReadFromJsonAsync<ServiceRow>())!;
        using var stale = await PostCategoryAsync(client, row.ServiceId, row.ETag, new { code = "SECOND", name = "Second" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("SERVICE.VERSION_CONFLICT", await stale.Content.ReadAsStringAsync());
        using var duplicate = await PostCategoryAsync(client, row.ServiceId, saved.ETag, new { code = " CATEGORY ", name = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.Contains("SERVICE.CATEGORY_CODE_EXISTS", await duplicate.Content.ReadAsStringAsync());
        object[] invalid = [new { code = "", name = "Blank" }, new { code = "NEW", name = "Valid", serviceId = Guid.NewGuid() },
            new { code = "NEW", name = "Valid", tenantId = account.TenantId }, new { code = "NEW", name = "Valid", status = "ACTIVE" }];
        foreach (var input in invalid) Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostCategoryAsync(client, row.ServiceId, saved.ETag, input)).StatusCode);
        var final = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items);
        Assert.Single(final.Categories); Assert.Equal(saved.ETag, final.ETag);
        await using var db = database.Create(account.UserId, account.TenantId); Assert.Single(await db.AuditLogs.Where(a => a.Action == "SERVICE.CATEGORY_CREATED").ToListAsync());
    }
    [Fact]
    public async Task Concurrent_category_creates_with_the_same_tag_have_one_winner()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        var responses = await Task.WhenAll(PostCategoryAsync(client, row.ServiceId, row.ETag, new { code = "A", name = "First" }),
            PostCategoryAsync(client, row.ServiceId, row.ETag, new { code = "B", name = "Second" }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await using var db = database.Create(account.UserId, account.TenantId); Assert.Single(await db.ServiceCategories.ToListAsync());
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "SERVICE.CATEGORY_CREATED").ToListAsync());
    }
    [Fact]
    public async Task Foreign_and_missing_services_share_safe_category_creation_denials()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        await LoginAsync(client, b); var foreign = await CreateServiceAsync(client); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        foreach (var id in new[] { foreign.ServiceId, Guid.NewGuid() })
        {
            using var response = await PostCategoryAsync(client, id, foreign.ETag, new { code = "BAD", name = "Foreign write" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.DoesNotContain("IT", await response.Content.ReadAsStringAsync());
        }
        await using var db = database.Create(a.UserId, a.TenantId); var denials = await db.AuditLogs.Where(a => a.Action == "SECURITY.ACCESS_DENIED").ToListAsync();
        Assert.Equal(2, denials.Count); Assert.All(denials, d => Assert.Equal(a.TenantId, d.TenantId));
        await using var other = database.Create(b.UserId, b.TenantId); Assert.Empty(await other.ServiceCategories.ToListAsync());
    }
    [Fact]
    public async Task Category_audit_failure_rolls_back_both_the_child_and_parent_ETag()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_category_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action"='SERVICE.CATEGORY_CREATED' THEN RAISE EXCEPTION 'test-only category failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_category_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_category_audit_failure();
            """);
        try
        {
            using var response = await PostCategoryAsync(client, row.ServiceId, row.ETag, new { code = "ROLLBACK", name = "Rollback" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.DoesNotContain("test-only", await response.Content.ReadAsStringAsync());
            var current = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items);
            Assert.Equal(row.ETag, current.ETag); Assert.Empty(current.Categories);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_category_audit_failure ON \"AuditLog\"; DROP FUNCTION test_category_audit_failure();"); }
    }
    [Fact]
    public async Task Category_creation_rechecks_grants_after_waiting_for_the_service_lock()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        await using var db = database.Create(account.UserId, account.TenantId);
        await using var blocker = new NpgsqlConnection(database.ConnectionString); await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using var hold = new NpgsqlCommand("SELECT 1 FROM \"Service\" WHERE \"ServiceId\"=@id FOR UPDATE", blocker, transaction);
        hold.Parameters.AddWithValue("id", row.ServiceId); await hold.ExecuteScalarAsync();
        var pending = PostCategoryAsync(client, row.ServiceId, row.ETag, new { code = "NO", name = "Revoked" });
        var blocked = false; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE wait_event_type='Lock' AND query LIKE '%Service%' AND pid<>pg_backend_pid()
                """).SingleAsync() > 0;
            if (blocked) break; await Task.Delay(25);
        }
        db.UserRoles.Remove(await db.UserRoles.SingleAsync()); await db.SaveChangesAsync(); await transaction.CommitAsync();
        using var response = await pending; Assert.True(blocked); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await db.ServiceCategories.ToListAsync()); Assert.Single(await db.AuditLogs.Where(a => a.Action == "SECURITY.ACCESS_DENIED").ToListAsync());
    }

    private static async Task<ServiceRow> CreateServiceAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(Endpoint, new { code = "IT", name = "Help" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ServiceRow>())!;
    }
    private static async Task<HttpResponseMessage> PostCategoryAsync(HttpClient client, Guid id, string? tag, object input)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{id}/categories") { Content = JsonContent.Create(input) };
        if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
        return await client.SendAsync(request);
    }
    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    [Fact]
    public async Task Metadata_update_preserves_categories_bindings_and_identity_with_before_after_audit()
    {
        var now = new DateTimeOffset(DateTime.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database, configureServices: services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FrozenClock(now)); });
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        using var create = await client.PostAsJsonAsync(Endpoint, new { code = "IT", name = "Original", description = "Before", categories = new[] {new {code = "DEVICE", name = "Devices"}} });
        var original = (await create.Content.ReadFromJsonAsync<ServiceRow>())!;
        await using var db = database.Create(account.UserId, account.TenantId);
        var workflow = WorkflowDefinition.CreateDraft(account.TenantId, "Workflow", WorkflowBusinessType.Request, now);
        var workflowVersion = WorkflowVersion.CreateDraft(workflow.Id, 1);
        var profile = SlaProfile.CreateDraft(account.TenantId, "SLA"); var calendar = BusinessCalendar.Create(account.TenantId, "UTC", """{"monday":[{"start":"08:00","end":"17:00"}]}""");
        var sla = SlaVersion.CreateSnapshot(profile.Id, 1, 60, 45, calendar.Id);
        db.AddRange(workflow, workflowVersion, profile, calendar, sla); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkflowVersion" SET "Status"='PUBLISHED', "PublishedAt"={now} WHERE "WorkflowVersionId"={workflowVersion.Id};
            UPDATE "Service" SET "ActiveWorkflowVersionId"={workflowVersion.Id}, "ActiveSLAVersionId"={sla.Id} WHERE "ServiceId"={original.ServiceId};
            """);
        var before = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items);
        using var response = await PutServiceAsync(client, before.ServiceId, before.ETag, new {code = " NEW ", name = " New name ", description = " <script>literal</script> ", active = false});
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var after = (await response.Content.ReadFromJsonAsync<ServiceRow>())!;
        Assert.Equal("NEW", after.Code); Assert.Equal("New name", after.Name); Assert.Equal("<script>literal</script>", after.Description); Assert.Equal("INACTIVE", after.Status);
        Assert.Equal(before.ServiceId, after.ServiceId); Assert.Equal(before.CreatedAt, after.CreatedAt); Assert.Equal(before.Categories, after.Categories);
        Assert.Equal(workflowVersion.Id, after.ActiveWorkflowVersionId); Assert.Equal(sla.Id, after.ActiveSlaVersionId);
        Assert.NotEqual(before.ETag, after.ETag); Assert.Equal(after.ETag, response.Headers.ETag!.ToString());
        Assert.Equal(now, (await db.Services.AsNoTracking().SingleAsync()).UpdatedAt);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "SERVICE.UPDATED");
        Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(account.TenantId, audit.TenantId); Assert.Equal(after.ServiceId, audit.ObjectId);
        using var oldJson = JsonDocument.Parse(audit.BeforeJson!); using var newJson = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal("IT", oldJson.RootElement.GetProperty("code").GetString()); Assert.Equal("ACTIVE", oldJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("NEW", newJson.RootElement.GetProperty("code").GetString()); Assert.Equal("INACTIVE", newJson.RootElement.GetProperty("status").GetString());
    }
    [Fact]
    public async Task Metadata_update_validates_strong_ETag_required_fields_and_does_not_mutate_on_rejection()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        var body = new {code = "NEW", name = "New name", active = false};
        foreach (var tag in new string?[] {null, "*", "1", "W/\"1\"", "\"1\", \"2\""})
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutServiceAsync(client, row.ServiceId, tag, body)).StatusCode);
        foreach (var invalid in new object[] {new {code="IT",name="Help"}, new {code=" ",name="Help",active=true}, new {code="IT",name="Help",active=true,categories=Array.Empty<object>()},
            new {code="IT",name="Help",active=true,tenantId=account.TenantId},new {code="IT",name="Help",active=true,activeWorkflowVersionId=Guid.NewGuid()},new {code="IT",name="Help",active=true,status="DRAFT"}})
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutServiceAsync(client, row.ServiceId, row.ETag, invalid)).StatusCode);
        using var success = await PutServiceAsync(client, row.ServiceId, row.ETag, body); Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        var updated = (await success.Content.ReadFromJsonAsync<ServiceRow>())!;
        using var stale = await PutServiceAsync(client, row.ServiceId, row.ETag, body);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("SERVICE.VERSION_CONFLICT", await stale.Content.ReadAsStringAsync());
        using var duplicate = await client.PostAsJsonAsync(Endpoint, new {code="EXISTS",name="Another"}); Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
        using var rejected = await PutServiceAsync(client, row.ServiceId, updated.ETag, new {code="EXISTS",name="Changed",active=true});
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode); Assert.Contains("SERVICE.DUPLICATE_CODE", await rejected.Content.ReadAsStringAsync());
        var persisted = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items, s => s.ServiceId == row.ServiceId);
        Assert.Equal(updated.ETag, persisted.ETag); Assert.Equal("NEW", persisted.Code); Assert.Equal("INACTIVE", persisted.Status);
    }
    [Fact]
    public async Task Concurrent_metadata_and_category_mutations_share_one_aggregate_ETag()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        var results = await Task.WhenAll(PutServiceAsync(client, row.ServiceId, row.ETag, new {code="NEW",name="Updated",active=false}),
            PostCategoryAsync(client, row.ServiceId, row.ETag, new {code="NEW",name="New category"}));
        Assert.Single(results, r => r.IsSuccessStatusCode); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "SERVICE.UPDATED" || a.Action == "SERVICE.CATEGORY_CREATED").ToListAsync());
        var current = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items); Assert.NotEqual(row.ETag, current.ETag);
        foreach (var result in results) result.Dispose();
    }
    [Fact]
    public async Task Service_create_does_not_grant_update_and_update_authority_is_live_not_role_name_based()
    {
        var account = await SeedAsync(); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow); db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id)); await db.SaveChangesAsync();
        var createGrant = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "service.create")).Id);
        db.RolePermissions.Add(createGrant); await db.SaveChangesAsync(); var row = await CreateServiceAsync(client);
        var body = new {code="IT",name="Updated",active=false};
        Assert.Equal(HttpStatusCode.Forbidden, (await PutServiceAsync(client, row.ServiceId, row.ETag, body)).StatusCode);
        var updateGrant = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "service.update")).Id);
        db.RolePermissions.Remove(createGrant); db.RolePermissions.Add(updateGrant); await db.SaveChangesAsync();
        using var allowed = await PutServiceAsync(client, row.ServiceId, row.ETag, body); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        row = (await allowed.Content.ReadFromJsonAsync<ServiceRow>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new {code="NO",name="No"})).StatusCode);
        db.RolePermissions.Remove(updateGrant); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await PutServiceAsync(client, row.ServiceId, row.ETag, body)).StatusCode);
    }
    private static async Task<HttpResponseMessage> PutServiceAsync(HttpClient client, Guid id, string? tag, object input)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Endpoint}/{id}") {Content = JsonContent.Create(input)};
        if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
        return await client.SendAsync(request);
    }
    [Fact]
    public async Task Metadata_update_cannot_cross_tenants_or_use_employee_authority()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database); using var own = factory.CreateClient(); using var other = factory.CreateClient();
        await LoginAsync(own, a); await LoginAsync(other, b); var row = await CreateServiceAsync(other);
        var body = new {code="CHANGED",name="Changed",active=false};
        own.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        using var denied = await PutServiceAsync(own, row.ServiceId, row.ETag, body);
        using var missing = await PutServiceAsync(own, Guid.NewGuid(), row.ETag, body);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); Assert.Equal(missing.StatusCode, denied.StatusCode);
        await using var db = database.Create(a.UserId, a.TenantId);
        Assert.Equal(2, await db.AuditLogs.CountAsync(log => log.Action == "SECURITY.ACCESS_DENIED"));
        var employee = await SeedAsync("EMPLOYEE"); await LoginAsync(own, employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutServiceAsync(own, row.ServiceId, row.ETag, body)).StatusCode);
        Assert.Equal(row.ETag, Assert.Single((await other.GetFromJsonAsync<ServicePage>(Endpoint))!.Items).ETag);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutServiceAsync(anonymous, row.ServiceId, row.ETag, body)).StatusCode);
    }
    [Fact]
    public async Task Metadata_audit_failure_rolls_back_all_fields_and_the_ETag()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_service_update_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW."Action"='SERVICE.UPDATED' THEN RAISE EXCEPTION 'test audit failure' USING ERRCODE='23514'; END IF; RETURN NEW;
            END $$;
            CREATE TRIGGER test_service_update_audit_failure BEFORE INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_service_update_audit_failure();
            """);
        try
        {
            using var response = await PutServiceAsync(client, row.ServiceId, row.ETag, new {code="CHANGED",name="Changed",description="Changed",active=false});
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.DoesNotContain("test audit failure", await response.Content.ReadAsStringAsync());
            var unchanged = Assert.Single((await client.GetFromJsonAsync<ServicePage>(Endpoint))!.Items);
            Assert.Equal(row.ETag, unchanged.ETag); Assert.Equal(row.Code, unchanged.Code); Assert.Equal(row.Name, unchanged.Name);
            Assert.Equal(row.Description, unchanged.Description); Assert.Equal(row.Status, unchanged.Status);
            Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "SERVICE.UPDATED"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_service_update_audit_failure ON \"AuditLog\"; DROP FUNCTION test_service_update_audit_failure();"); }
    }
    [Fact]
    public async Task Metadata_update_rechecks_authority_after_waiting_for_the_aggregate_lock()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account); var row = await CreateServiceAsync(client);
        await using var connection = new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT 1 FROM \"Service\" WHERE \"ServiceId\"=@id FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("id", row.ServiceId); await command.ExecuteScalarAsync();
        var pending = PutServiceAsync(client, row.ServiceId, row.ETag, new {code="CHANGED",name="Changed",active=false});
        await using var db = database.Create(account.UserId, account.TenantId); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var blocked = false;
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE wait_event_type='Lock' AND query LIKE '%Service%' AND pid<>pg_backend_pid()
                """).SingleAsync() > 0;
            if (blocked) break; await Task.Delay(25);
        }
        db.UserRoles.Remove(await db.UserRoles.SingleAsync()); await db.SaveChangesAsync(); await transaction.CommitAsync();
        using var response = await pending; Assert.True(blocked); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("IT", (await db.Services.AsNoTracking().SingleAsync()).Code);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "SERVICE.UPDATED"));
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "SECURITY.ACCESS_DENIED").ToListAsync());
    }

    private async Task<Account> SeedAsync(string? roleName = null)
    {
        var seed = await database.SeedTenantAsync(); await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);
        if (roleName is not null) { var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName); db.UserRoles.Add(new(user.Id, role.Id)); await db.SaveChangesAsync(); }
        return new(user.Id, tenant.Id, tenant.TenantKey);
    }
    private static async Task LoginAsync(HttpClient client, Account account)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = account.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }
    private sealed record Account(Guid UserId, Guid TenantId, string Key);
}
