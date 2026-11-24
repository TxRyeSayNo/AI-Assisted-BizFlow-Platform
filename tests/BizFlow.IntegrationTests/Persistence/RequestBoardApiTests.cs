using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Requests;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for the read-only request board (F1): lane grouping against PostgreSQL, the
/// tenant and scope boundaries inherited from RequestReadPolicy, and the shared Kanban flag.
/// </summary>
[Collection("Postgres")]
public sealed class RequestBoardApiTests(PostgresFixture database)
{
    private const string Password = "Request-board-test!9312";
    private const string Board = "/api/v1/requests/board";

    [Fact]
    public async Task Every_lifecycle_state_becomes_a_lane_and_each_lane_reports_its_real_size()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<RequestBoardView>(Board))!;

        Assert.Equal(Enum.GetValues<RequestState>().Length, view.Columns.Count);
        Assert.Equal(Enum.GetValues<RequestState>().Select(RequestListCodes.State).ToArray(),
            view.Columns.Select(column => column.Status).ToArray());
        Assert.Equal(50, view.ColumnSize);

        var draft = Lane(view, RequestState.Draft);
        Assert.Equal(2, draft.Total);
        Assert.Equal(2, draft.Items.Count);
        Assert.All(draft.Items, item => Assert.Equal("Dịch vụ hạ tầng", item.ServiceName));
        Assert.All(draft.Items, item => Assert.Equal("Máy chủ", item.CategoryName));
        Assert.All(draft.Items, item => Assert.Equal("Test Employee", item.RequesterName));

        var submitted = Assert.Single(Lane(view, RequestState.Submitted).Items);
        Assert.Equal("Hồ sơ đã gửi", submitted.Title);
        Assert.Equal("HIGH", submitted.Priority);

        Assert.Empty(Lane(view, RequestState.Cancelled).Items);
        Assert.Equal(0, Lane(view, RequestState.Cancelled).Total);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task A_capped_lane_still_reports_the_untruncated_total()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var view = (await client.GetFromJsonAsync<RequestBoardView>($"{Board}?columnSize=1"))!;

        Assert.Single(Lane(view, RequestState.Draft).Items);
        Assert.Equal(2, Lane(view, RequestState.Draft).Total);
    }

    [Fact]
    public async Task Search_priority_service_category_and_requester_filters_translate_correctly()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        async Task<string[]> TitlesAsync(string query)
        {
            var view = (await client.GetFromJsonAsync<RequestBoardView>(Board + query))!;
            return [.. view.Columns.SelectMany(column => column.Items).Select(item => item.Title)
                .OrderBy(title => title, StringComparer.Ordinal)];
        }

        Assert.Equal("Hồ sơ đã gửi", Assert.Single(await TitlesAsync("?priority=HIGH")));
        Assert.Equal("Hồ sơ nháp A", Assert.Single(await TitlesAsync("?search=%20nh%C3%A1p%20A%20")));
        Assert.Empty(await TitlesAsync("?search=%25"));
        Assert.Equal(3, (await TitlesAsync($"?serviceId={a.ServiceId}")).Length);
        Assert.Equal(3, (await TitlesAsync($"?categoryId={a.CategoryId}")).Length);
        Assert.Equal(3, (await TitlesAsync($"?requesterId={a.User}")).Length);
        Assert.Empty(await TitlesAsync($"?requesterId={Guid.NewGuid()}"));
    }

    [Fact]
    public async Task One_tenants_board_never_reaches_another_tenants_requests()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        var view = (await client.GetFromJsonAsync<RequestBoardView>($"{Board}?tenantId={foreign.Tenant}"))!;
        var rows = view.Columns.SelectMany(column => column.Items).ToArray();

        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.NotEqual(foreign.User, row.RequesterId));
        Assert.All(rows, row => Assert.NotEqual(foreign.ServiceId, row.ServiceId));
        Assert.DoesNotContain(rows, row => row.RequestId == foreign.SubmittedId);
        Assert.Empty((await client.GetFromJsonAsync<RequestBoardView>($"{Board}?requesterId={foreign.User}"))!
            .Columns.SelectMany(column => column.Items));
        Assert.Empty((await client.GetFromJsonAsync<RequestBoardView>($"{Board}?serviceId={foreign.ServiceId}"))!
            .Columns.SelectMany(column => column.Items));
    }

    [Fact]
    public async Task A_caller_without_any_request_read_grant_is_refused_and_an_anonymous_one_is_challenged()
    {
        var a = await SeedAsync(roleName: null);
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Board)).StatusCode);

        await LoginAsync(client, a);
        using var refused = await client.GetAsync(Board);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("ACCESS.DENIED", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_parameters_are_refused_and_the_board_is_never_cached()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        foreach (var query in new[]
        {
            "?columnSize=0", "?columnSize=101", "?priority=Highest",
            "?search=" + new string('a', RequestBoardQuery.MaxSearchLength + 1),
            $"?serviceId={Guid.Empty}", $"?categoryId={Guid.Empty}", $"?requesterId={Guid.Empty}"
        })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync(Board + query)).StatusCode);

        using var response = await client.GetAsync(Board);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_the_board_without_disturbing_the_request_list()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:Kanban"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Board)).StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<RequestListPage>("/api/v1/requests"))!.Total);
    }

    private static RequestBoardColumn Lane(RequestBoardView view, RequestState state) =>
        Assert.Single(view.Columns, column => column.Status == RequestListCodes.State(state));

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

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

        var service = InternalService.Create(tenant.Id, "INFRA", "Dịch vụ hạ tầng", null, true, now);
        db.Add(service);
        await db.SaveChangesAsync();
        var category = ServiceCategory.Create(service.Id, "SERVER", "Máy chủ");
        db.Add(category);
        await db.SaveChangesAsync();

        var draftA = WorkRequest.CreateDraft(tenant.Id, user.Id, service.Id, category.Id,
            "Hồ sơ nháp A", "Mô tả A", now);
        var draftB = WorkRequest.CreateDraft(tenant.Id, user.Id, service.Id, category.Id,
            "Hồ sơ nháp B", "Mô tả B", now);
        var submitted = WorkRequest.CreateSubmitted(tenant.Id, user.Id, service.Id, category.Id,
            "Hồ sơ đã gửi", "Mô tả C", now, RequestPriority.High);
        db.AddRange(draftA, draftB, submitted);
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

        return new(user.Id, tenant.Id, tenant.TenantKey, service.Id, category.Id, submitted.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid ServiceId, Guid CategoryId, Guid SubmittedId);
}
