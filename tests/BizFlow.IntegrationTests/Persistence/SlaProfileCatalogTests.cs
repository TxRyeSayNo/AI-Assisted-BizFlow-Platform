using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Sla;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Sla;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class SlaProfileCatalogTests(PostgresFixture database)
{
    private const string Endpoint = "/api/v1/sla-profiles";
    private const string Password = "Sla-test-only!9247";

    [Fact]
    public async Task Create_commits_only_named_draft_and_matching_actor_audit_without_invented_name_uniqueness()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        using var response = await client.PostAsJsonAsync(Endpoint, new { name = "  Support response  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "name", "slaProfileId", "status" }, json.RootElement.EnumerateObject().Select(p => p.Name).Order());
        var row = (await response.Content.ReadFromJsonAsync<SlaProfileRow>())!;
        Assert.Equal("Support response", row.Name); Assert.Equal("DRAFT", row.Status);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(row.SlaProfileId, (await db.SlaProfiles.SingleAsync()).Id);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "SLA.PROFILE_CREATED");
        Assert.Equal(row.SlaProfileId, audit.ObjectId); Assert.Equal("SLAProfile", audit.ObjectType);
        Assert.Equal(account.TenantId, audit.TenantId); Assert.Equal(account.UserId, audit.ActorId); Assert.Null(audit.BeforeJson);
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(row.Name, after.RootElement.GetProperty("name").GetString());
        Assert.Equal("DRAFT", after.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint, new { name = row.Name })).StatusCode);
        Assert.Equal(2, await db.SlaProfiles.CountAsync());
    }

    [Theory]
    [InlineData("MANAGER")]
    [InlineData("EMPLOYEE")]
    public async Task Default_non_admin_roles_do_not_grant_configuration_catalog_access(string role)
    {
        var account = await SeedAsync(role);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/versions",
            new { targetMinutes = 60, warningMinutes = 45, calendarId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Effective_live_permissions_not_role_names_control_reads_and_writes_separately()
    {
        var account = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Endpoint, new { name = "Anonymous" })).StatusCode);
        await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Denied" })).StatusCode);
        var read = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.read")).Id);
        db.RolePermissions.Add(read); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Reader" })).StatusCode);
        var configure = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.configure")).Id);
        db.RolePermissions.Add(configure); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint, new { name = "Authorized" })).StatusCode);
        db.RolePermissions.RemoveRange(read, configure); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Revoked" })).StatusCode);
        Assert.Single(await db.SlaProfiles.ToListAsync());
        Assert.Equal(5, await db.AuditLogs.CountAsync(a => a.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Fact]
    public async Task Tenant_filters_cover_rows_counts_and_creation_despite_forged_context()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync("COMPANY_ADMIN");
        await using var own = database.Create(a.UserId, a.TenantId);
        await using var foreign = database.Create(b.UserId, b.TenantId);
        foreign.SlaProfiles.Add(SlaProfile.CreateDraft(b.TenantId, "Foreign confidential")); await foreign.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint + $"?tenantId={b.TenantId}", new { name = "Own" })).StatusCode);
        var page = (await client.GetFromJsonAsync<SlaProfilePage>(Endpoint + $"?tenantId={b.TenantId}"))!;
        Assert.Equal(1, page.Total); Assert.Equal("Own", Assert.Single(page.Items).Name);
        Assert.Equal("Own", (await own.SlaProfiles.SingleAsync()).Name);
        Assert.Equal("Foreign confidential", (await foreign.SlaProfiles.SingleAsync()).Name);
        await using var anonymous = database.Create(null, a.TenantId);
        await using var platform = database.Create(database.PlatformUserId, null);
        Assert.Empty(await anonymous.SlaProfiles.ToListAsync()); Assert.Empty(await platform.SlaProfiles.ToListAsync());
    }

    [Fact]
    public async Task Literal_search_paging_status_and_input_allowlist_preserve_the_metadata_contract()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient(); await LoginAsync(client, account);
        foreach (var name in new[] { "Alpha", "SLA %_ value", "Zulu" })
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Endpoint, new { name })).StatusCode);
        var second = (await client.GetFromJsonAsync<SlaProfilePage>(Endpoint + "?page=2&pageSize=1&status=DRAFT"))!;
        Assert.Equal(3, second.Total); Assert.Equal("SLA %_ value", Assert.Single(second.Items).Name);
        Assert.Single((await client.GetFromJsonAsync<SlaProfilePage>(Endpoint + "?search=%25_"))!.Items);
        Assert.Single((await client.GetFromJsonAsync<SlaProfilePage>(Endpoint + "?search=aLpHa"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<SlaProfilePage>(Endpoint + "?status=ACTIVE"))!.Items);
        foreach (var query in new[] { "page=0", "pageSize=0", "pageSize=101", "page=2147483647&pageSize=100", "status=PUBLISHED", "search=" + new string('a', 201) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Endpoint + "?" + query)).StatusCode);
        object[] invalid = [new { }, new { name = "  " }, new { name = new string('x', 201) }, new { name = "Injected", tenantId = Guid.NewGuid() },
            new { name = "Injected", status = "ACTIVE" }, new { name = "Injected", targetMinutes = 60 }, new { name = "Injected", calendarId = Guid.NewGuid() }];
        foreach (var body in invalid) Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint, body)).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(3, await db.SlaProfiles.CountAsync()); Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.Action == "SLA.PROFILE_CREATED"));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_profile_without_exposing_database_details()
    {
        var account = await SeedAsync("COMPANY_ADMIN");
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_sla_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action" = 'SLA.PROFILE_CREATED' THEN RAISE EXCEPTION 'test-only audit failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_sla_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_sla_audit_failure();
            """);
        try
        {
            await using var factory = new AuthenticationFactory(database);
            using var client = factory.CreateClient(); await LoginAsync(client, account);
            using var response = await client.PostAsJsonAsync(Endpoint, new { name = "Rollback" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("test-only audit", await response.Content.ReadAsStringAsync());
            Assert.Empty(await db.SlaProfiles.ToListAsync()); Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "SLA.PROFILE_CREATED"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_sla_audit_failure ON \"AuditLog\"; DROP FUNCTION test_sla_audit_failure();"); }
    }

    [Fact]
    public async Task Platform_plane_and_suspended_tenants_have_no_catalog_access()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await using var platform = database.Create(database.PlatformUserId, null);
        var user = await platform.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await platform.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "PLATFORM", password = Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Endpoint, new { name = "Platform" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/versions",
            new { targetMinutes = 60, warningMinutes = 45, calendarId = Guid.NewGuid() })).StatusCode);
        var account = await SeedAsync("COMPANY_ADMIN"); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Tenant\" SET \"Status\"='SUSPENDED' WHERE \"TenantId\"={account.TenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Endpoint, new { name = "Suspended" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Endpoint}/{Guid.NewGuid()}/versions",
            new { targetMinutes = 60, warningMinutes = 45, calendarId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Persistence_denies_forged_ownership_unimplemented_mutations_and_SQL_reparenting()
    {
        var a = await database.SeedTenantAsync(); var b = await database.SeedTenantAsync();
        await using var db = database.Create(a.UserId, a.TenantId);
        var profile = SlaProfile.CreateDraft(a.TenantId, "Own"); db.SlaProfiles.Add(profile); await db.SaveChangesAsync();
        db.SlaProfiles.Add(SlaProfile.CreateDraft(b.TenantId, "Forged"));
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Attach(profile); db.Entry(profile).Property(p => p.Status).CurrentValue = SlaProfileStatus.Active;
        await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.SlaProfiles.Remove(profile); await Assert.ThrowsAsync<ApplicationFault>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "SLAProfile" SET "TenantId"={b.TenantId} WHERE "SLAProfileId"={profile.Id}
            """))).SqlState);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "SLAProfile" SET "Name"=' ' WHERE "SLAProfileId"={profile.Id}
            """));
        Assert.Equal(SlaProfileStatus.Draft, (await db.SlaProfiles.SingleAsync()).Status);
    }

    [Fact]
    public async Task Migration_reapplies_empty_but_refuses_to_destroy_configured_grants_or_profiles()
    {
        var fixture = new PostgresFixture();
        try
        {
            await fixture.InitializeAsync();
            await using (var migrationDb = fixture.Create(null, null))
            {
                await migrationDb.GetService<IMigrator>().MigrateAsync("20261002123538_TenantSettingFoundation");
                await migrationDb.Database.MigrateAsync(); await migrationDb.Database.MigrateAsync();
            }
            var account = await fixture.SeedTenantAsync();
            await using var db = fixture.Create(account.UserId, account.TenantId);
            var role = Role.CreateCustom(account.TenantId, "SLA reader", DateTimeOffset.UtcNow);
            db.Roles.Add(role); await db.SaveChangesAsync();
            var grant = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.read")).Id);
            db.RolePermissions.Add(grant); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261002123538_TenantSettingFoundation"));
            Assert.True(await db.RolePermissions.AnyAsync(r => r.RoleId == role.Id));
            db.RolePermissions.Remove(grant); await db.SaveChangesAsync();
            db.SlaProfiles.Add(SlaProfile.CreateDraft(account.TenantId, "Retained")); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261002123538_TenantSettingFoundation"));
            Assert.Equal("Retained", (await db.SlaProfiles.SingleAsync()).Name);
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Version_creation_preserves_history_and_audits_the_actual_configuration_without_activation()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var escalation = new { levels = new[] { new { level = 1, afterMinutes = 0, recipientUserIds = new[] { account.UserId } } } };
        using var response = await client.PostAsJsonAsync($"{Endpoint}/{config.Profile}/versions", new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar, escalationConfig = escalation });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var first = (await response.Content.ReadFromJsonAsync<SlaVersionView>())!;
        Assert.Equal(1, first.VersionNo); Assert.Equal(config.Profile, first.SlaProfileId); Assert.Equal(config.Calendar, first.CalendarId);
        Assert.Equal(60, first.TargetMinutes); Assert.Equal(45, first.WarningMinutes); Assert.NotNull(first.EscalationConfig);
        using var nextResponse = await client.PostAsJsonAsync($"{Endpoint}/{config.Profile}/versions", new { targetMinutes = 90, warningMinutes = 60, calendarId = config.Calendar });
        Assert.Equal(HttpStatusCode.Created, nextResponse.StatusCode); Assert.Equal(2, (await nextResponse.Content.ReadFromJsonAsync<SlaVersionView>())!.VersionNo);
        await using var db = database.Create(account.UserId, account.TenantId);
        var saved = await db.SlaVersions.SingleAsync(v => v.Id == first.SlaVersionId);
        Assert.Equal(60, saved.TargetMinutes); Assert.Equal(45, saved.WarningMinutes); Assert.Equal(account.UserId, Assert.Single(saved.EscalationRecipientIds()));
        Assert.Equal(SlaProfileStatus.Draft, (await db.SlaProfiles.SingleAsync()).Status);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "SLA.VERSION_CREATED" && a.ObjectId == saved.Id);
        Assert.Equal(account.UserId, audit.ActorId); Assert.Equal(account.TenantId, audit.TenantId); Assert.Equal("SLAVersion", audit.ObjectType); Assert.Null(audit.BeforeJson);
        using var json = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(60, json.RootElement.GetProperty("targetMinutes").GetInt32()); Assert.Equal(config.Calendar, json.RootElement.GetProperty("calendarId").GetGuid());
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("escalationConfig").ValueKind);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action == "SLA.VERSION_CREATED"));
    }

    [Fact]
    public async Task Concurrent_version_posts_are_numbered_under_the_parent_lock()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsJsonAsync($"{Endpoint}/{config.Profile}/versions",
            new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar })));
        var numbers = new List<int>();
        foreach (var response in responses)
            using (response) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); numbers.Add((await response.Content.ReadFromJsonAsync<SlaVersionView>())!.VersionNo); }
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, numbers.Order());
        await using var db = database.Create(account.UserId, account.TenantId);
        Assert.Equal(5, await db.SlaVersions.CountAsync()); Assert.Equal(5, await db.AuditLogs.CountAsync(a => a.Action == "SLA.VERSION_CREATED"));
    }

    [Fact]
    public async Task Version_creation_requires_live_configure_not_read_grants_or_an_admin_role_name()
    {
        var account = await SeedAsync(); var config = await SeedConfigurationAsync(account);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        var uri = $"{Endpoint}/{config.Profile}/versions"; var body = new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(uri, body)).StatusCode);
        await LoginAsync(client, account); await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow); db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id));
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(uri, body)).StatusCode);
        db.RolePermissions.Add(new(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.read")).Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(uri, body)).StatusCode);
        var configure = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.configure")).Id);
        db.RolePermissions.Add(configure); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(uri, body)).StatusCode);
        db.RolePermissions.Remove(configure); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(uri, body)).StatusCode);
        Assert.Single(await db.SlaVersions.ToListAsync());
    }

    [Fact]
    public async Task Version_references_do_not_reveal_foreign_existence_or_accept_forged_tenant_context()
    {
        var a = await SeedAsync("COMPANY_ADMIN"); var b = await SeedAsync("COMPANY_ADMIN");
        var own = await SeedConfigurationAsync(a); var other = await SeedConfigurationAsync(b);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        foreach (var (profile, calendar) in new[] { (other.Profile, own.Calendar), (Guid.NewGuid(), own.Calendar), (own.Profile, other.Calendar), (own.Profile, Guid.NewGuid()) })
        {
            using var response = await client.PostAsJsonAsync($"{Endpoint}/{profile}/versions?tenantId={b.TenantId}", new { targetMinutes = 60, warningMinutes = 45, calendarId = calendar });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Contains("RESOURCE.NOT_FOUND", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"{Endpoint}/{own.Profile}/versions?tenantId={b.TenantId}",
            new { targetMinutes = 60, warningMinutes = 45, calendarId = own.Calendar })).StatusCode);
        await using var db = database.Create(a.UserId, a.TenantId); await using var foreign = database.Create(b.UserId, b.TenantId);
        Assert.Single(await db.SlaVersions.ToListAsync()); Assert.Empty(await foreign.SlaVersions.ToListAsync());
        Assert.Equal(4, await db.AuditLogs.CountAsync(a => a.Action == "SECURITY.ACCESS_DENIED"));
    }

    [Fact]
    public async Task Invalid_thresholds_calendars_escalation_and_overposted_authority_never_create_versions()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        var other = await SeedAsync();
        await using var db = database.Create(account.UserId, account.TenantId);
        var empty = BusinessCalendar.Create(account.TenantId); db.BusinessCalendars.Add(empty); await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var uri = $"{Endpoint}/{config.Profile}/versions";
        object[] invalid = [new { targetMinutes = 0, warningMinutes = 0, calendarId = config.Calendar },
            new { targetMinutes = 60, warningMinutes = 60, calendarId = config.Calendar },
            new { targetMinutes = 60, warningMinutes = -1, calendarId = config.Calendar },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = Guid.Empty },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = empty.Id },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar, versionNo = 100 },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar, tenantId = account.TenantId },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar, status = "ACTIVE" },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar, escalationConfig = new { } },
            new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar, escalationConfig = new { levels = new[] { new { level = 1, afterMinutes = 0, recipientUserIds = new[] { other.UserId } } } } }];
        foreach (var body in invalid) Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(uri, body)).StatusCode);
        Assert.Empty(await db.SlaVersions.ToListAsync()); Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "SLA.VERSION_CREATED"));
        db.SlaVersions.Add(SlaVersion.CreateSnapshot(config.Profile, int.MaxValue, 60, 45, config.Calendar)); await db.SaveChangesAsync();
        using var overflow = await client.PostAsJsonAsync(uri, new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar });
        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode); Assert.Contains("SLA.VERSION_LIMIT", await overflow.Content.ReadAsStringAsync());
        Assert.Single(await db.SlaVersions.ToListAsync());
    }

    [Fact]
    public async Task Failed_version_audit_rolls_back_snapshot_and_preserves_the_next_number()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_sla_version_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."Action"='SLA.VERSION_CREATED' THEN RAISE EXCEPTION 'test-only version audit failure' USING ERRCODE='23514'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER test_sla_version_audit_failure AFTER INSERT ON "AuditLog" FOR EACH ROW EXECUTE FUNCTION test_sla_version_audit_failure();
            """);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var uri = $"{Endpoint}/{config.Profile}/versions"; var body = new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar };
        try
        {
            using var response = await client.PostAsJsonAsync(uri, body); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("test-only", await response.Content.ReadAsStringAsync());
            Assert.Empty(await db.SlaVersions.ToListAsync()); Assert.False(await db.AuditLogs.AnyAsync(a => a.Action == "SLA.VERSION_CREATED"));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_sla_version_audit_failure ON \"AuditLog\"; DROP FUNCTION test_sla_version_audit_failure();"); }
        using var retry = await client.PostAsJsonAsync(uri, body); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, (await retry.Content.ReadFromJsonAsync<SlaVersionView>())!.VersionNo);
    }

    [Fact]
    public async Task Permission_revocation_while_waiting_for_the_profile_lock_is_rechecked_before_commit()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        await using var db = database.Create(account.UserId, account.TenantId);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        await using var blocker = new NpgsqlConnection(database.ConnectionString); await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using var hold = new NpgsqlCommand("SELECT 1 FROM \"SLAProfile\" WHERE \"SLAProfileId\"=@id FOR UPDATE", blocker, transaction);
        hold.Parameters.AddWithValue("id", config.Profile); await hold.ExecuteScalarAsync();
        var pending = client.PostAsJsonAsync($"{Endpoint}/{config.Profile}/versions", new { targetMinutes = 60, warningMinutes = 45, calendarId = config.Calendar });
        var blocked = false; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested && !pending.IsCompleted)
        {
            blocked = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_stat_activity
                WHERE wait_event_type='Lock' AND query LIKE '%SLAProfile%' AND pid<>pg_backend_pid()
                """).SingleAsync() > 0;
            if (blocked) break; await Task.Delay(25);
        }
        var role = await db.UserRoles.SingleAsync(); db.UserRoles.Remove(role); await db.SaveChangesAsync();
        await transaction.CommitAsync(); using var response = await pending;
        Assert.True(blocked); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await db.SlaVersions.ToListAsync()); Assert.Single(await db.AuditLogs.Where(a => a.Action == "SECURITY.ACCESS_DENIED").ToListAsync());
    }

    [Fact]
    public async Task History_returns_descending_pages_and_exact_frozen_configuration_without_mutation()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var revised = BusinessCalendar.Create(account.TenantId, "Asia/Ho_Chi_Minh", """{"tuesday":[{"start":"09:00","end":"18:00"}]}""", """["2026-10-03"]""");
        db.BusinessCalendars.Add(revised); await db.SaveChangesAsync();
        var escalation = JsonSerializer.Serialize(new { levels = new[] { new { level = 1, afterMinutes = 0, recipientUserIds = new[] { account.UserId } } } });
        for (var number = 1; number <= 26; number++)
            db.SlaVersions.Add(SlaVersion.CreateSnapshot(config.Profile, number, 60 + number, 45, number == 26 ? revised.Id : config.Calendar, number == 26 ? escalation : null));
        await db.SaveChangesAsync();
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        var auditCount = await db.AuditLogs.CountAsync();
        var uri = $"{Endpoint}/{config.Profile}/versions";
        using var response = await client.GetAsync(uri); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var first = (await response.Content.ReadFromJsonAsync<SlaVersionHistoryPage>())!;
        Assert.Equal(config.Profile, first.SlaProfileId); Assert.Equal("Version test profile", first.ProfileName);
        Assert.Equal(26, first.Total); Assert.Equal(25, first.PageSize); Assert.Equal(1, first.Page);
        Assert.Equal(Enumerable.Range(2, 25).Reverse(), first.Items.Select(v => v.VersionNo));
        var newest = first.Items[0]; Assert.Equal(86, newest.TargetMinutes); Assert.Equal(45, newest.WarningMinutes);
        Assert.Equal(revised.Id, newest.Calendar.CalendarId); Assert.Equal("Asia/Ho_Chi_Minh", newest.Calendar.TimeZone);
        Assert.Equal("09:00", newest.Calendar.WorkingHours.GetProperty("tuesday")[0].GetProperty("start").GetString());
        Assert.Equal("2026-10-03", newest.Calendar.Holidays[0].GetString());
        Assert.Equal(account.UserId, newest.EscalationConfig!.Value.GetProperty("levels")[0].GetProperty("recipientUserIds")[0].GetGuid());
        var second = (await client.GetFromJsonAsync<SlaVersionHistoryPage>(uri + "?page=2"))!;
        var oldest = Assert.Single(second.Items); Assert.Equal(1, oldest.VersionNo); Assert.Null(oldest.EscalationConfig);
        Assert.Equal(config.Calendar, oldest.Calendar.CalendarId); Assert.Equal("UTC", oldest.Calendar.TimeZone);
        Assert.Equal("08:00", oldest.Calendar.WorkingHours.GetProperty("monday")[0].GetProperty("start").GetString());
        Assert.Equal(0, oldest.Calendar.Holidays.GetArrayLength());
        Assert.Empty((await client.GetFromJsonAsync<SlaVersionHistoryPage>(uri + "?page=3"))!.Items);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "calendar", "escalationConfig", "slaVersionId", "targetMinutes", "versionNo", "warningMinutes" },
            json.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(auditCount, await db.AuditLogs.CountAsync()); Assert.Equal(26, await db.SlaVersions.CountAsync());
    }

    [Fact]
    public async Task History_hides_foreign_and_missing_profiles_and_validates_pagination_for_owned_empty_profiles()
    {
        var account = await SeedAsync("COMPANY_ADMIN"); var config = await SeedConfigurationAsync(account);
        var other = await SeedAsync("COMPANY_ADMIN"); var foreign = await SeedConfigurationAsync(other);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, account);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", other.TenantId.ToString());
        var uri = $"{Endpoint}/{config.Profile}/versions";
        var empty = (await client.GetFromJsonAsync<SlaVersionHistoryPage>(uri + $"?tenantId={other.TenantId}"))!;
        Assert.Empty(empty.Items); Assert.Equal(0, empty.Total); Assert.Equal(config.Profile, empty.SlaProfileId);
        foreach (var id in new[] { foreign.Profile, Guid.NewGuid(), Guid.Empty })
        {
            using var denied = await client.GetAsync($"{Endpoint}/{id}/versions");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.DoesNotContain("Version test profile", await denied.Content.ReadAsStringAsync());
        }
        foreach (var query in new[] { "page=0", "page=-1", "pageSize=0", "pageSize=101", "page=2147483647&pageSize=100" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(uri + "?" + query)).StatusCode);
        await using var db = database.Create(account.UserId, account.TenantId);
        var denials = await db.AuditLogs.Where(a => a.Action == "SECURITY.ACCESS_DENIED").ToListAsync();
        Assert.Equal(3, denials.Count); Assert.All(denials, d => { Assert.Equal(account.TenantId, d.TenantId); Assert.Equal(account.UserId, d.ActorId); });
    }

    [Fact]
    public async Task History_requires_live_read_permission_not_configuration_grant_or_role_name()
    {
        var account = await SeedAsync(); var config = await SeedConfigurationAsync(account);
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient();
        var uri = $"{Endpoint}/{config.Profile}/versions";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(uri)).StatusCode); await LoginAsync(client, account);
        await using var db = database.Create(account.UserId, account.TenantId);
        var role = Role.CreateCustom(account.TenantId, "COMPANY_ADMIN", DateTimeOffset.UtcNow);
        db.Roles.Add(role); db.UserRoles.Add(new(account.UserId, role.Id)); await db.SaveChangesAsync();
        db.RolePermissions.Add(new(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.configure")).Id)); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(uri + "?page=0")).StatusCode);
        var read = new RolePermission(role.Id, (await db.Permissions.SingleAsync(p => p.Code == "sla.read")).Id);
        db.RolePermissions.Add(read); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri)).StatusCode);
        db.RolePermissions.Remove(read); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(uri)).StatusCode);
        Assert.Empty(await db.SlaVersions.ToListAsync());
    }

    private async Task<(Guid Profile, Guid Calendar)> SeedConfigurationAsync(Account account)
    {
        await using var db = database.Create(account.UserId, account.TenantId);
        var profile = SlaProfile.CreateDraft(account.TenantId, "Version test profile");
        var calendar = BusinessCalendar.Create(account.TenantId, "UTC", """{"monday":[{"start":"08:00","end":"17:30"}]}""");
        db.AddRange(profile, calendar); await db.SaveChangesAsync(); return (profile.Id, calendar.Id);
    }

    private async Task<Account> SeedAsync(string? roleName = null)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
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
    private sealed record Account(Guid UserId, Guid TenantId, string Key);
}
