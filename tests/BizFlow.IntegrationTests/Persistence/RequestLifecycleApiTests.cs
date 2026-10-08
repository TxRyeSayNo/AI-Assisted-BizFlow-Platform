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
public sealed class RequestLifecycleApiTests(PostgresFixture database)
{
    private const string Password = "Request-lifecycle!94742";
    private const string Endpoint = "/api/v1/requests";

    [Fact]
    public async Task Request_lifecycle_flow_route_receive_and_start_work()
    {
        var env = await SeedLifecycleEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        using var processorClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);
        await LoginAsync(processorClient, env.Processor);

        // 1. Requester creates submitted request
        var createInput = new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Hardware laptop replacement",
            description = "Battery expanded on dev laptop",
            priority = "HIGH",
            submitImmediately = true
        };

        var createRes = await requesterClient.PostAsJsonAsync(Endpoint, createInput);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;
        Assert.Equal("SUBMITTED", created.Status);

        // 2. Manager routes request to Operations department and Processor user
        var routeInput = new
        {
            toDepartmentId = env.OpsDeptId,
            toUserId = env.Processor.User,
            reason = "Assigned to primary hardware engineer",
            source = "MANUAL"
        };

        var routeRes = await managerClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/route", routeInput);
        Assert.Equal(HttpStatusCode.OK, routeRes.StatusCode);
        var routed = (await routeRes.Content.ReadFromJsonAsync<RequestRoutedView>())!;
        Assert.Equal("ROUTED", routed.Status);
        Assert.Equal(env.OpsDeptId, routed.ToDepartmentId);
        Assert.Equal(env.Processor.User, routed.ToUserId);

        // 3. Processor receives request with receipt confirmation note
        var receiveInput = new
        {
            note = "Diagnosing battery replacement parts"
        };

        var receiveRes = await processorClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/receive", receiveInput);
        Assert.Equal(HttpStatusCode.OK, receiveRes.StatusCode);
        var received = (await receiveRes.Content.ReadFromJsonAsync<RequestReceivedView>())!;
        Assert.Equal("RECEIVED", received.Status);

        // 4. Processor starts processing work
        var startRes = await processorClient.PostAsync($"{Endpoint}/{created.RequestId}/start", null);
        Assert.Equal(HttpStatusCode.OK, startRes.StatusCode);
        var started = (await startRes.Content.ReadFromJsonAsync<RequestProcessingView>())!;
        Assert.Equal("IN_PROGRESS", started.Status);

        // 5. Requester views request detail: verified updated status, routing, and receipt timestamps
        var detailRes = await requesterClient.GetAsync($"{Endpoint}/{created.RequestId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detail = (await detailRes.Content.ReadFromJsonAsync<RequestDetailView>())!;
        Assert.Equal("IN_PROGRESS", detail.Status);
        Assert.Equal(env.OpsDeptId, detail.CurrentRoutingDepartmentId);
        Assert.Equal("Operations", detail.CurrentRoutingDepartmentName);
        Assert.Equal(env.Processor.User, detail.CurrentRoutingUserId);
        Assert.NotNull(detail.RoutedAt);
        Assert.NotNull(detail.ReceivedAt);
    }

    [Fact]
    public async Task Request_rejection_sets_rejected_status_and_records_reason()
    {
        var env = await SeedLifecycleEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);

        var createInput = new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Request for dual monitors",
            description = "Desk setup upgrade",
            priority = "LOW",
            submitImmediately = true
        };

        var createRes = await requesterClient.PostAsJsonAsync(Endpoint, createInput);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        // Manager rejects request
        var rejectInput = new
        {
            reason = "Policy does not permit dual monitors for remote contractors."
        };

        var rejectRes = await managerClient.PostAsJsonAsync($"{Endpoint}/{created.RequestId}/reject", rejectInput);
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
        var rejected = (await rejectRes.Content.ReadFromJsonAsync<RequestRejectedView>())!;
        Assert.Equal("REJECTED", rejected.Status);
        Assert.Equal(rejectInput.reason, rejected.Reason);

        // Requester views detail: rejected status and reason are readable
        var detail = (await requesterClient.GetFromJsonAsync<RequestDetailView>($"{Endpoint}/{created.RequestId}"))!;
        Assert.Equal("REJECTED", detail.Status);
        Assert.Equal(rejectInput.reason, detail.RejectionReason);
    }

    [Fact]
    public async Task Request_cancellation_cancels_submitted_request()
    {
        var env = await SeedLifecycleEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        await LoginAsync(requesterClient, env.Requester);

        var createInput = new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "No longer needed request",
            description = "Mistakenly created request",
            priority = "MEDIUM",
            submitImmediately = true
        };

        var createRes = await requesterClient.PostAsJsonAsync(Endpoint, createInput);
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        var cancelRes = await requesterClient.PostAsync($"{Endpoint}/{created.RequestId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelled = (await cancelRes.Content.ReadFromJsonAsync<RequestCancelledView>())!;
        Assert.Equal("CANCELLED", cancelled.Status);

        var detail = (await requesterClient.GetFromJsonAsync<RequestDetailView>($"{Endpoint}/{created.RequestId}"))!;
        Assert.Equal("CANCELLED", detail.Status);
    }

    [Fact]
    public async Task Request_routing_idempotency_replay_and_conflict()
    {
        var env = await SeedLifecycleEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);

        var createInput = new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Idempotency test request",
            description = "Testing replay",
            priority = "MEDIUM",
            submitImmediately = true
        };

        var createRes = await requesterClient.PostAsJsonAsync(Endpoint, createInput);
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        var key = Guid.NewGuid().ToString("N");

        // First route
        var routeReq1 = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{created.RequestId}/route")
        {
            Content = JsonContent.Create(new { toDepartmentId = env.OpsDeptId, source = "MANUAL" })
        };
        routeReq1.Headers.Add("Idempotency-Key", key);
        var routeRes1 = await managerClient.SendAsync(routeReq1);
        Assert.Equal(HttpStatusCode.OK, routeRes1.StatusCode);
        var routed1 = (await routeRes1.Content.ReadFromJsonAsync<RequestRoutedView>())!;

        // Exact replay with same key
        var routeReq2 = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{created.RequestId}/route")
        {
            Content = JsonContent.Create(new { toDepartmentId = env.OpsDeptId, source = "MANUAL" })
        };
        routeReq2.Headers.Add("Idempotency-Key", key);
        var routeRes2 = await managerClient.SendAsync(routeReq2);
        Assert.Equal(HttpStatusCode.OK, routeRes2.StatusCode);
        var routed2 = (await routeRes2.Content.ReadFromJsonAsync<RequestRoutedView>())!;
        Assert.Equal(routed1.RoutingId, routed2.RoutingId);

        // Different payload with same key -> 409 Conflict
        var routeReq3 = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{created.RequestId}/route")
        {
            Content = JsonContent.Create(new { toDepartmentId = env.OpsDeptId, reason = "Changed payload" })
        };
        routeReq3.Headers.Add("Idempotency-Key", key);
        var routeRes3 = await managerClient.SendAsync(routeReq3);
        Assert.Equal(HttpStatusCode.Conflict, routeRes3.StatusCode);
    }

    private async Task<LifecycleEnvironment> SeedLifecycleEnvironmentAsync()
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

        // Processor (Employee in Ops)
        var processor = UserAccount.CreateTenantUser(tenant.Id, "PRC001", "processor@example.test", "Processor User", "hash", now);
        var prcHash = hasher.HashPassword(processor, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(processor, prcHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(processor, opsDept.Id);
        db.Users.Add(processor);

        await db.SaveChangesAsync();

        // Assign Roles
        var empRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        var mgrRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");

        db.UserRoles.Add(new(requester.Id, empRole.Id));
        db.UserRoles.Add(new(manager.Id, mgrRole.Id));
        db.UserRoles.Add(new(processor.Id, empRole.Id));

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
            new(processor.Id, tenant.Id, tenant.TenantKey, "PRC001"),
            opsDept.Id,
            service.Id,
            category.Id);
    }

    private static async Task LoginAsync(HttpClient client, TestUser a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = a.EmpCode, password = Password, tenantKey = a.TenantKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record TestUser(Guid User, Guid Tenant, string TenantKey, string EmpCode);
    private sealed record LifecycleEnvironment(
        TestUser Requester,
        TestUser Manager,
        TestUser Processor,
        Guid OpsDeptId,
        Guid ServiceId,
        Guid CategoryId);
}
