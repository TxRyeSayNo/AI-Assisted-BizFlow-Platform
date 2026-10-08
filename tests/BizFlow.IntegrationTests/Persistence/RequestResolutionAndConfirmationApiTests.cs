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
using Xunit;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class RequestResolutionAndConfirmationApiTests(PostgresFixture database)
{
    private const string Password = "Request-resolution!94742";
    private const string Endpoint = "/api/v1/requests";

    [Fact]
    public async Task Request_resolution_confirmation_and_closure_flow()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        using var resolverClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);
        await LoginAsync(resolverClient, env.Resolver);

        // 1. Create and progress request to InProgress
        var requestId = await CreateAndStartRequestAsync(requesterClient, managerClient, resolverClient, env);

        // 2. Resolver resolves the request
        var resolveInput = new
        {
            content = "Replacement motherboard installed and firmware updated to v2.4"
        };

        var resolveRes = await resolverClient.PostAsJsonAsync($"{Endpoint}/{requestId}/resolve", resolveInput);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
        var resolved = (await resolveRes.Content.ReadFromJsonAsync<RequestResolvedView>())!;
        Assert.Equal("RESOLVED", resolved.Status);
        Assert.Equal(1, resolved.RevisionNo);
        Assert.Equal(resolveInput.content, resolved.Content);

        // 3. Requester checks detail: status is RESOLVED, resolution history contains revision 1
        var detailRes = await requesterClient.GetAsync($"{Endpoint}/{requestId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = (await detailRes.Content.ReadFromJsonAsync<RequestDetailView>())!;
        Assert.Equal("RESOLVED", detail.Status);
        Assert.NotNull(detail.ResolvedAt);
        Assert.NotNull(detail.Resolutions);
        Assert.Single(detail.Resolutions);
        Assert.Equal(1, detail.Resolutions[0].RevisionNo);
        Assert.Equal(resolveInput.content, detail.Resolutions[0].Content);

        // 4. Requester confirms resolution -> CLOSED
        var confirmInput = new
        {
            decision = "CONFIRMED",
            note = "Verified hardware is working as expected"
        };

        var confirmRes = await requesterClient.PostAsJsonAsync($"{Endpoint}/{requestId}/confirm", confirmInput);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmed = (await confirmRes.Content.ReadFromJsonAsync<RequestConfirmedView>())!;
        Assert.Equal("CONFIRMED", confirmed.Decision);
        Assert.Equal("CLOSED", confirmed.Status);

        // 5. Final detail verification: status is CLOSED, ClosedAt is set, Confirmation record present
        var finalDetailRes = await requesterClient.GetAsync($"{Endpoint}/{requestId}");
        Assert.Equal(HttpStatusCode.OK, finalDetailRes.StatusCode);
        var finalDetail = (await finalDetailRes.Content.ReadFromJsonAsync<RequestDetailView>())!;
        Assert.Equal("CLOSED", finalDetail.Status);
        Assert.NotNull(finalDetail.ClosedAt);
        Assert.NotNull(finalDetail.Confirmations);
        Assert.Contains(finalDetail.Confirmations, c => c.MilestoneType == "RESOLUTION" && c.Decision == "CONFIRMED");
    }

    [Fact]
    public async Task Request_resolution_rework_flow_preserves_history()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        using var resolverClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);
        await LoginAsync(resolverClient, env.Resolver);

        var requestId = await CreateAndStartRequestAsync(requesterClient, managerClient, resolverClient, env);

        // 1. Resolver resolves revision 1
        var res1 = await resolverClient.PostAsJsonAsync($"{Endpoint}/{requestId}/resolve", new { content = "Temporary workaround applied" });
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // 2. Requester requests rework with reason
        var reworkRes = await requesterClient.PostAsJsonAsync($"{Endpoint}/{requestId}/confirm", new
        {
            decision = "REWORK",
            note = "Need permanent repair, not workaround"
        });
        Assert.Equal(HttpStatusCode.OK, reworkRes.StatusCode);
        var rework = (await reworkRes.Content.ReadFromJsonAsync<RequestConfirmedView>())!;
        Assert.Equal("REWORK", rework.Decision);
        Assert.Equal("IN_PROGRESS", rework.Status);

        // 3. Resolver resolves revision 2
        var res2 = await resolverClient.PostAsJsonAsync($"{Endpoint}/{requestId}/resolve", new { content = "Permanent component replaced" });
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var resolved2 = (await res2.Content.ReadFromJsonAsync<RequestResolvedView>())!;
        Assert.Equal(2, resolved2.RevisionNo);

        // 4. Verify detail contains both revisions in history
        var detailRes = await requesterClient.GetAsync($"{Endpoint}/{requestId}");
        var detail = (await detailRes.Content.ReadFromJsonAsync<RequestDetailView>())!;
        Assert.Equal("RESOLVED", detail.Status);
        Assert.Equal(2, detail.Resolutions?.Count);
        Assert.Equal(1, detail.Resolutions?[0].RevisionNo);
        Assert.Equal(2, detail.Resolutions?[1].RevisionNo);
    }

    [Fact]
    public async Task Request_revision_from_rejected_creates_new_linked_request()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);

        // 1. Requester creates submitted request
        var createRes = await requesterClient.PostAsJsonAsync(Endpoint, new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Request to reject and revise",
            description = "Initial submission",
            priority = "MEDIUM",
            submitImmediately = true
        });
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        // 2. Manager rejects the request with reason
        var rejectRes = await managerClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/reject", new
        {
            reason = "Insufficient justification provided"
        });
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);

        // 3. Requester creates revised request linked to rejected request
        var reviseRes = await requesterClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/revise", new
        {
            title = "Revised: Request with full justification",
            description = "Detailed justification added",
            priority = "HIGH",
            submitImmediately = true
        });
        Assert.Equal(HttpStatusCode.Created, reviseRes.StatusCode);
        var revised = (await reviseRes.Content.ReadFromJsonAsync<RequestRevisedView>())!;
        Assert.NotEqual(created.RequestId, revised.RequestId);
        Assert.Equal(created.RequestId, revised.SourceRequestId);
        Assert.Equal("SUBMITTED", revised.Status);

        // 4. Verify new request detail has RevisedFromRequestId set
        var detailRes = await requesterClient.GetAsync($"{Endpoint}/{revised.RequestId}");
        var detail = (await detailRes.Content.ReadFromJsonAsync<RequestDetailView>())!;
        Assert.Equal(created.RequestId, detail.RevisedFromRequestId);
        Assert.Equal("SUBMITTED", detail.Status);
    }

    private static async Task<Guid> CreateAndStartRequestAsync(
        HttpClient requesterClient,
        HttpClient managerClient,
        HttpClient resolverClient,
        LifecycleEnvironment env)
    {
        var createRes = await requesterClient.PostAsJsonAsync(Endpoint, new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Workstation fan failure",
            description = "Fan bearing noise and overheating",
            priority = "HIGH",
            submitImmediately = true
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        var routeRes = await managerClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/route", new
        {
            toDepartmentId = env.OpsDeptId,
            toUserId = env.Resolver.User,
            reason = "Assigned to hardware team"
        });
        Assert.Equal(HttpStatusCode.OK, routeRes.StatusCode);

        var receiveRes = await resolverClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/receive", new
        {
            note = "Intake received"
        });
        Assert.Equal(HttpStatusCode.OK, receiveRes.StatusCode);

        var startRes = await resolverClient.PostAsync($"{Endpoint}/{created.RequestId}/start", null);
        Assert.Equal(HttpStatusCode.OK, startRes.StatusCode);
        return created.RequestId;
    }

    private static async Task LoginAsync(HttpClient client, TestUser a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = a.EmpCode, password = Password, tenantKey = a.TenantKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private async Task<LifecycleEnvironment> SeedEnvironmentAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var adminUser = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var itDept = Department.Create(tenant.Id, "IT", "Information Technology", null, now);
        var opsDept = Department.Create(tenant.Id, "OPS", "Operations", null, now);
        db.Departments.AddRange(itDept, opsDept);
        await db.SaveChangesAsync();

        var hasher = new PasswordHasher<UserAccount>();

        // Requester (Employee in IT)
        var requester = UserAccount.CreateTenantUser(tenant.Id, "REQ001", "requester@example.test", "Requester User", "hash", now);
        var reqHash = hasher.HashPassword(requester, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(requester, reqHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(requester, itDept.Id);
        db.Users.Add(requester);

        // Manager (Manager over Ops)
        var manager = UserAccount.CreateTenantUser(tenant.Id, "MGR001", "manager@example.test", "Manager User", "hash", now);
        var mgrHash = hasher.HashPassword(manager, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(manager, mgrHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(manager, opsDept.Id);
        db.Users.Add(manager);

        // Resolver (Employee in Ops)
        var resolver = UserAccount.CreateTenantUser(tenant.Id, "RES001", "resolver@example.test", "Resolver User", "hash", now);
        var resHash = hasher.HashPassword(resolver, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(resolver, resHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(resolver, opsDept.Id);
        db.Users.Add(resolver);

        await db.SaveChangesAsync();

        // Assign Roles
        var empRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        var mgrRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");

        db.UserRoles.Add(new(requester.Id, empRole.Id));
        db.UserRoles.Add(new(manager.Id, mgrRole.Id));
        db.UserRoles.Add(new(resolver.Id, empRole.Id));

        // Manager scope over Ops dept
        db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, manager.Id, opsDept.Id, true, adminUser.Id, now));

        // Company status active
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        // Service & Category
        var service = InternalService.Create(tenant.Id, "IT-SRV", "IT Services", "Hardware and Software", true, now);
        var category = ServiceCategory.Create(service.Id, "HW-CAT", "Hardware");
        db.Services.Add(service);
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();

        return new(
            new(requester.Id, tenant.Id, tenant.TenantKey, "REQ001"),
            new(manager.Id, tenant.Id, tenant.TenantKey, "MGR001"),
            new(resolver.Id, tenant.Id, tenant.TenantKey, "RES001"),
            opsDept.Id,
            service.Id,
            category.Id);
    }

    private sealed record TestUser(Guid User, Guid Tenant, string TenantKey, string EmpCode);
    private sealed record LifecycleEnvironment(
        TestUser Requester,
        TestUser Manager,
        TestUser Resolver,
        Guid OpsDeptId,
        Guid ServiceId,
        Guid CategoryId);
}
