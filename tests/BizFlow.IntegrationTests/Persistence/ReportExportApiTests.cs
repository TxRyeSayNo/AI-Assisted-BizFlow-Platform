using System.Net;
using System.Net.Http.Json;
using System.Text;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for the report downloads (F5): the three formats over the real reporting
/// queries, the tenant boundary, and the ReportExport feature flag. The downloads deliberately
/// carry the same authorization as the matching view endpoints, which they reuse.
/// </summary>
[Collection("Postgres")]
public sealed class ReportExportApiTests(PostgresFixture database)
{
    private const string Password = "Report-export-test!7733";

    [Theory]
    [InlineData("csv", "text/csv")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("pdf", "application/pdf")]
    public async Task Every_report_downloads_in_every_format_with_a_real_payload(string format, string contentType)
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var report in new[] { "company", "workload", "sla" })
        {
            using var response = await client.GetAsync($"/api/v1/reports/{report}/export?format={format}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(bytes);
            // Each container announces itself: BOM for CSV, PK for a zip, %PDF for a document.
            switch (format)
            {
                case "csv":
                    Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
                    break;
                case "xlsx":
                    Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));
                    break;
                default:
                    Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
                    break;
            }
            // A download is a file, not a page.
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        }
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task Xlsx_is_the_default_and_an_unknown_format_is_refused()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        using var defaulted = await client.GetAsync("/api/v1/reports/company/export");
        Assert.Equal(HttpStatusCode.OK, defaulted.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            defaulted.Content.Headers.ContentType?.MediaType);

        foreach (var bad in new[] { "doc", "html", "xls" })
        {
            using var refused = await client.GetAsync($"/api/v1/reports/company/export?format={bad}");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Contains("REPORT.FORMAT", await refused.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task A_download_only_ever_contains_the_callers_own_tenant()
    {
        var a = await SeedAsync();
        var foreign = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        var csv = Encoding.UTF8.GetString(await client.GetByteArrayAsync(
            $"/api/v1/reports/workload/export?format=csv&tenantId={foreign.Tenant}"));

        Assert.Contains(a.DepartmentName, csv);
        Assert.DoesNotContain(foreign.DepartmentName, csv);
        Assert.DoesNotContain("PasswordHash", csv);
    }

    [Fact]
    public async Task A_download_requires_a_session_exactly_like_the_view_endpoint()
    {
        await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        foreach (var report in new[] { "company", "workload", "sla" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.GetAsync($"/api/v1/reports/{report}/export?format=csv")).StatusCode);
            // The matching view endpoint behaves the same way, which is the contract being kept.
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/reports/{report}")).StatusCode);
        }
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_the_downloads_and_leaves_the_views_working()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:ReportExport"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var report in new[] { "company", "workload", "sla" })
        {
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/api/v1/reports/{report}/export?format=csv")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/reports/{report}")).StatusCode);
        }
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private async Task<Account> SeedAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;
        var name = "Phòng " + tenant.TenantKey;

        var department = Department.Create(tenant.Id, "OURS", name, null, now);
        db.Add(department);
        await db.SaveChangesAsync();

        var done = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc đã xong", now, deadline: now.AddDays(1));
        var open = WorkTask.CreateDraft(tenant.Id, user.Id, "Việc đang mở", now, deadline: now.AddDays(2));
        db.AddRange(done, open);
        await db.SaveChangesAsync();
        db.AddRange(
            TaskAssignment.Create(done.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(open.Id, user.Id, null, user.Id, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={department.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            UPDATE "Task" SET "Status"='COMPLETED' WHERE "TaskId"={done.Id};
            UPDATE "Task" SET "Status"='IN_PROGRESS' WHERE "TaskId"={open.Id};
            """);

        var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
        db.UserRoles.Add(new(user.Id, role.Id));
        await db.SaveChangesAsync();

        return new(user.Id, tenant.Id, tenant.TenantKey, name);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, string DepartmentName);
}
