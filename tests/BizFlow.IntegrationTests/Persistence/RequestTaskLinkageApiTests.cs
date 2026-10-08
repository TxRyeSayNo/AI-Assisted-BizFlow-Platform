using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Requests;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class RequestTaskLinkageApiTests(PostgresFixture database)
{
    private const string Password = "Request-tasks!98234";
    private const string RequestsEndpoint = "/api/v1/requests";
    private const string TasksEndpoint = "/api/v1/tasks";

    [Fact]
    public async Task Request_to_task_creation_listing_and_detail_linkage_flow()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        using var resolverClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);
        await LoginAsync(resolverClient, env.Resolver);

        // 1. Create and progress request to IN_PROGRESS
        var requestId = await CreateAndStartRequestAsync(requesterClient, managerClient, resolverClient, env);

        // 2. Manager creates a task linked to the request via POST /api/v1/requests/{id}/tasks
        var taskInput = new
        {
            title = "Replace power supply unit",
            description = "Acquire compatible 750W PSU and replace in server rack",
            priority = "HIGH",
            checklist = new[] { "Order PSU component", "Power down server rack", "Swap unit and test voltage" }
        };

        var createTaskRes = await managerClient.PostAsJsonAsync($"{RequestsEndpoint}/{requestId}/tasks", taskInput);
        Assert.Equal(HttpStatusCode.Created, createTaskRes.StatusCode);
        var createdTask = (await createTaskRes.Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(taskInput.title, createdTask.Title);
        Assert.Equal("DRAFT", createdTask.Status);
        Assert.Equal(requestId, createdTask.RequestId);

        // 3. Query tasks for the request via GET /api/v1/requests/{id}/tasks
        var getTasksRes = await managerClient.GetAsync($"{RequestsEndpoint}/{requestId}/tasks");
        Assert.Equal(HttpStatusCode.OK, getTasksRes.StatusCode);
        var taskListPage = (await getTasksRes.Content.ReadFromJsonAsync<TaskListPage>())!;
        Assert.True(taskListPage.Total >= 1);
        var linkedTaskRow = Assert.Single(taskListPage.Items, t => t.TaskId == createdTask.TaskId);
        Assert.Equal(requestId, linkedTaskRow.RequestId);
        Assert.Equal("Replace power supply unit", linkedTaskRow.Title);

        // 4. Query request detail via GET /api/v1/requests/{id} to verify Tasks property is populated
        var detailRes = await managerClient.GetAsync($"{RequestsEndpoint}/{requestId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var requestDetail = (await detailRes.Content.ReadFromJsonAsync<RequestDetailView>())!;
        Assert.NotNull(requestDetail.Tasks);
        var detailTask = Assert.Single(requestDetail.Tasks!, t => t.TaskId == createdTask.TaskId);
        Assert.Equal("Replace power supply unit", detailTask.Title);
        Assert.Equal("DRAFT", detailTask.Status);
        Assert.Equal("HIGH", detailTask.Priority);

        // 5. Query task detail via GET /api/v1/tasks/{taskId} to verify RequestId is returned
        var taskDetailRes = await managerClient.GetAsync($"{TasksEndpoint}/{createdTask.TaskId}");
        Assert.Equal(HttpStatusCode.OK, taskDetailRes.StatusCode);
        var taskDetail = (await taskDetailRes.Content.ReadFromJsonAsync<TaskDetailView>())!;
        Assert.Equal(requestId, taskDetail.Task.RequestId);

        // 6. Test direct task creation with RequestId via POST /api/v1/tasks
        var directTaskInput = new
        {
            title = "Recalibrate cooling fan speeds",
            description = "Run fan curve diagnostic following PSU swap",
            priority = "MEDIUM",
            requestId = (Guid?)requestId
        };

        var directCreateRes = await managerClient.PostAsJsonAsync(TasksEndpoint, directTaskInput);
        Assert.Equal(HttpStatusCode.Created, directCreateRes.StatusCode);
        var directCreated = (await directCreateRes.Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(requestId, directCreated.RequestId);

        // Verify request now has 2 linked tasks
        var updatedTasksRes = await managerClient.GetAsync($"{RequestsEndpoint}/{requestId}/tasks");
        var updatedTaskList = (await updatedTasksRes.Content.ReadFromJsonAsync<TaskListPage>())!;
        Assert.Equal(2, updatedTaskList.Items.Count(t => t.RequestId == requestId));
    }

    [Fact]
    public async Task Creating_task_linked_to_nonexistent_request_fails()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var managerClient = factory.CreateClient();

        await LoginAsync(managerClient, env.Manager);

        var randomRequestId = Guid.NewGuid();
        var taskInput = new
        {
            title = "Task for missing request",
            priority = "LOW"
        };

        var res = await managerClient.PostAsJsonAsync($"{RequestsEndpoint}/{randomRequestId}/tasks", taskInput);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    private sealed record TestUser(Guid User, string EmpCode, string TenantKey);
    private sealed record LifecycleEnvironment(
        TestUser Requester,
        TestUser Manager,
        TestUser Resolver,
        Guid ServiceId,
        Guid CategoryId,
        Guid OpsDeptId);

    private static async Task<Guid> CreateAndStartRequestAsync(
        HttpClient requesterClient,
        HttpClient managerClient,
        HttpClient resolverClient,
        LifecycleEnvironment env)
    {
        var createRes = await requesterClient.PostAsJsonAsync(RequestsEndpoint, new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Hardware overheating issue in rack B",
            description = "Multiple thermal alerts triggered on blade server 4",
            priority = "HIGH",
            submitImmediately = true
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        var routeRes = await managerClient.PostAsJsonAsync($"{RequestsEndpoint}/{created.RequestId}/route", new
        {
            toDepartmentId = env.OpsDeptId,
            toUserId = env.Resolver.User,
            reason = "Assigned to hardware team"
        });
        Assert.Equal(HttpStatusCode.OK, routeRes.StatusCode);

        var receiveRes = await resolverClient.PostAsJsonAsync($"{RequestsEndpoint}/{created.RequestId}/receive", new
        {
            note = "Intake received"
        });
        Assert.Equal(HttpStatusCode.OK, receiveRes.StatusCode);

        var startRes = await resolverClient.PostAsync($"{RequestsEndpoint}/{created.RequestId}/start", null);
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

        var requester = UserAccount.CreateTenantUser(tenant.Id, "REQ001", "requester@example.test", "Requester User", "hash", now);
        var reqHash = hasher.HashPassword(requester, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(requester, reqHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(requester, itDept.Id);
        db.Users.Add(requester);

        var manager = UserAccount.CreateTenantUser(tenant.Id, "MGR001", "manager@example.test", "Manager User", "hash", now);
        var mgrHash = hasher.HashPassword(manager, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(manager, mgrHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(manager, opsDept.Id);
        db.Users.Add(manager);

        var resolver = UserAccount.CreateTenantUser(tenant.Id, "RES001", "resolver@example.test", "Resolver User", "hash", now);
        var resHash = hasher.HashPassword(resolver, Password);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(resolver, resHash);
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(resolver, opsDept.Id);
        db.Users.Add(resolver);

        await db.SaveChangesAsync();

        var empRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        var mgrRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");

        db.UserRoles.Add(new(requester.Id, empRole.Id));
        db.UserRoles.Add(new(manager.Id, mgrRole.Id));
        db.UserRoles.Add(new(resolver.Id, empRole.Id));

        db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, manager.Id, itDept.Id, true, adminUser.Id, now));
        db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, manager.Id, opsDept.Id, true, adminUser.Id, now));

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        var service = InternalService.Create(tenant.Id, "IT-SRV", "IT Services", "Hardware and Software", true, now);
        var category = ServiceCategory.Create(service.Id, "HW-CAT", "Hardware");
        db.Services.Add(service);
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();

        return new(
            new(requester.Id, requester.EmployeeCode, tenant.TenantKey),
            new(manager.Id, manager.EmployeeCode, tenant.TenantKey),
            new(resolver.Id, resolver.EmployeeCode, tenant.TenantKey),
            service.Id,
            category.Id,
            opsDept.Id);
    }
}
