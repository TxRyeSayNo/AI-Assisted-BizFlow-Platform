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

[Collection("Postgres")]
public sealed class RequestApiTests(PostgresFixture database)
{
    private const string Password = "Request-api-test!94742";
    private const string Endpoint = "/api/v1/requests";

    [Fact]
    public async Task Create_draft_submit_and_direct_submit_round_trip_with_scoped_list_and_detail()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        // 1. Create Draft
        var draftInput = new
        {
            serviceId = a.ServiceId,
            categoryId = a.CategoryId,
            title = "Need extra monitor",
            description = "4K display for developer workstation",
            priority = "HIGH",
            submitImmediately = false
        };

        var draftRes = await client.PostAsJsonAsync(Endpoint, draftInput);
        Assert.Equal(HttpStatusCode.Created, draftRes.StatusCode);
        var draft = (await draftRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;
        Assert.Equal("DRAFT", draft.Status);
        Assert.Equal("Need extra monitor", draft.Title);
        Assert.Equal("HIGH", draft.Priority);

        // 2. Submit Draft
        var submitRes = await client.PostAsync($"{Endpoint}/{draft.RequestId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
        var submitted = (await submitRes.Content.ReadFromJsonAsync<RequestSubmittedView>())!;
        Assert.Equal(draft.RequestId, submitted.RequestId);
        Assert.Equal("SUBMITTED", submitted.Status);

        // 3. Create Directly Submitted
        var directInput = new
        {
            serviceId = a.ServiceId,
            categoryId = a.CategoryId,
            title = "Keyboard replacement",
            description = "Mechanical keyboard issue",
            priority = "MEDIUM",
            submitImmediately = true
        };

        var directRes = await client.PostAsJsonAsync(Endpoint, directInput);
        Assert.Equal(HttpStatusCode.Created, directRes.StatusCode);
        var direct = (await directRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;
        Assert.Equal("SUBMITTED", direct.Status);

        // 4. Scoped List
        var list = (await client.GetFromJsonAsync<RequestListPage>(Endpoint))!;
        Assert.True(list.Total >= 2);
        Assert.Contains(list.Items, r => r.RequestId == draft.RequestId && r.Status == "SUBMITTED" && r.ServiceName == "IT Services");
        Assert.Contains(list.Items, r => r.RequestId == direct.RequestId && r.Status == "SUBMITTED" && r.CategoryName == "Hardware");

        // 5. Scoped Detail
        var detail = (await client.GetFromJsonAsync<RequestDetailView>($"{Endpoint}/{draft.RequestId}"))!;
        Assert.Equal(draft.RequestId, detail.RequestId);
        Assert.Equal("Need extra monitor", detail.Title);
        Assert.Equal("4K display for developer workstation", detail.Description);
        Assert.Equal("SUBMITTED", detail.Status);
        Assert.Equal("IT Services", detail.ServiceName);
        Assert.Equal("Hardware", detail.CategoryName);
    }

    [Fact]
    public async Task Replay_protection_detects_duplicate_keys_and_conflicts_on_create_and_submit()
    {
        var a = await SeedAsync("EMPLOYEE");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        var key = Guid.NewGuid().ToString();
        var createInput = new
        {
            serviceId = a.ServiceId,
            categoryId = a.CategoryId,
            title = "Software license",
            description = "IDE license renewal",
            priority = "LOW",
            submitImmediately = false
        };

        // First creation with key
        var req1 = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(createInput)
        };
        req1.Headers.Add("Idempotency-Key", key);
        var res1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        var view1 = (await res1.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        // Replay with exact same payload
        var req2 = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(createInput)
        };
        req2.Headers.Add("Idempotency-Key", key);
        var res2 = await client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
        var view2 = (await res2.Content.ReadFromJsonAsync<RequestCreatedView>())!;
        Assert.Equal(view1.RequestId, view2.RequestId);

        // Replay with different payload -> 409 Conflict
        var conflictInput = new
        {
            serviceId = a.ServiceId,
            categoryId = a.CategoryId,
            title = "DIFFERENT TITLE",
            description = "IDE license renewal",
            priority = "LOW",
            submitImmediately = false
        };
        var req3 = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(conflictInput)
        };
        req3.Headers.Add("Idempotency-Key", key);
        var res3 = await client.SendAsync(req3);
        Assert.Equal(HttpStatusCode.Conflict, res3.StatusCode);

        // Submit with key
        var submitKey = Guid.NewGuid().ToString();
        var subReq1 = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{view1.RequestId}/submit");
        subReq1.Headers.Add("Idempotency-Key", submitKey);
        var subRes1 = await client.SendAsync(subReq1);
        Assert.Equal(HttpStatusCode.OK, subRes1.StatusCode);

        // Replay submit with same key
        var subReq2 = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{view1.RequestId}/submit");
        subReq2.Headers.Add("Idempotency-Key", submitKey);
        var subRes2 = await client.SendAsync(subReq2);
        Assert.Equal(HttpStatusCode.OK, subRes2.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_requests_are_isolated_with_non_revealing_404()
    {
        var a = await SeedAsync("EMPLOYEE");
        var b = await SeedAsync("EMPLOYEE");

        await using var factory = new AuthenticationFactory(database);
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        await LoginAsync(clientA, a);
        await LoginAsync(clientB, b);

        var createInput = new
        {
            serviceId = a.ServiceId,
            categoryId = a.CategoryId,
            title = "Confidential project request",
            description = "Details inside tenant A",
            priority = "HIGH",
            submitImmediately = true
        };

        var res = await clientA.PostAsJsonAsync(Endpoint, createInput);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = (await res.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        // Tenant B requests detail -> 404 NotFound
        var notFound = await clientB.GetAsync($"{Endpoint}/{created.RequestId}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        // Tenant B lists requests -> does not contain Tenant A's request
        var listB = (await clientB.GetFromJsonAsync<RequestListPage>(Endpoint))!;
        Assert.DoesNotContain(listB.Items, r => r.RequestId == created.RequestId);
    }

    private async Task<Account> SeedAsync(string? roleName)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var dept = Department.Create(tenant.Id, "ENG", "Engineering", null, now);
        db.Add(dept);
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={dept.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        var service = InternalService.Create(tenant.Id, "IT", "IT Services", "Internal IT", true, now);
        var category = ServiceCategory.Create(service.Id, "HW", "Hardware");
        db.Services.Add(service);
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();

        return new(user.Id, tenant.Id, tenant.TenantKey, service.Id, category.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid ServiceId, Guid CategoryId);
}
