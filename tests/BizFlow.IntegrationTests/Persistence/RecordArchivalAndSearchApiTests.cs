using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Requests;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class RecordArchivalAndSearchApiTests(PostgresFixture database)
{
    private const string Password = "Record-tests!78192";
    private const string RecordsEndpoint = "/api/v1/records";
    private const string TasksEndpoint = "/api/v1/tasks";
    private const string RequestsEndpoint = "/api/v1/requests";

    [Fact]
    public async Task Unified_record_search_and_record_archival_lifecycle()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var managerClient = factory.CreateClient();
        using var employeeClient = factory.CreateClient();

        await LoginAsync(managerClient, env.Manager);
        await LoginAsync(employeeClient, env.Employee);

        // 1. Create a Task via manager
        var taskRes = await managerClient.PostAsJsonAsync(TasksEndpoint, new
        {
            title = "Alpha task for database indexing",
            description = "Optimize queries and execute archival verification",
            priority = "HIGH"
        });
        Assert.Equal(HttpStatusCode.Created, taskRes.StatusCode);
        var createdTask = (await taskRes.Content.ReadFromJsonAsync<TaskCreatedView>())!;

        // 2. Create a Request via employee
        var reqRes = await employeeClient.PostAsJsonAsync(RequestsEndpoint, new
        {
            serviceId = env.ServiceId,
            categoryId = env.CategoryId,
            title = "Beta request for system diagnostics",
            description = "Investigate system health telemetry logs",
            priority = "MEDIUM"
        });
        Assert.Equal(HttpStatusCode.Created, reqRes.StatusCode);
        var createdRequest = (await reqRes.Content.ReadFromJsonAsync<RequestCreatedView>())!;

        // 3. Search without filter - finds both task and request
        var searchAllRes = await employeeClient.GetAsync($"{RecordsEndpoint}/search");
        var errorBody = await searchAllRes.Content.ReadAsStringAsync();
        Assert.True(searchAllRes.IsSuccessStatusCode, $"Search failed with {searchAllRes.StatusCode}: {errorBody}");
        var pagedAll = (await searchAllRes.Content.ReadFromJsonAsync<RecordSearchPagedResult>())!;
        Assert.True(pagedAll.TotalCount >= 2);
        Assert.Contains(pagedAll.Items, i => i.Id == createdTask.TaskId && i.RecordType == "TASK");
        Assert.Contains(pagedAll.Items, i => i.Id == createdRequest.RequestId && i.RecordType == "REQUEST");

        // 4. Text query search for "Alpha"
        var searchAlphaRes = await employeeClient.GetAsync($"{RecordsEndpoint}/search?q=Alpha");
        Assert.Equal(HttpStatusCode.OK, searchAlphaRes.StatusCode);
        var pagedAlpha = (await searchAlphaRes.Content.ReadFromJsonAsync<RecordSearchPagedResult>())!;
        Assert.Contains(pagedAlpha.Items, i => i.Id == createdTask.TaskId);
        Assert.DoesNotContain(pagedAlpha.Items, i => i.Id == createdRequest.RequestId);

        // 5. Type filter "TASK"
        var searchTaskOnly = await employeeClient.GetAsync($"{RecordsEndpoint}/search?type=task");
        Assert.Equal(HttpStatusCode.OK, searchTaskOnly.StatusCode);
        var pagedTask = (await searchTaskOnly.Content.ReadFromJsonAsync<RecordSearchPagedResult>())!;
        Assert.All(pagedTask.Items, i => Assert.Equal("TASK", i.RecordType));

        // 6. Type filter "REQUEST"
        var searchReqOnly = await employeeClient.GetAsync($"{RecordsEndpoint}/search?type=request");
        Assert.Equal(HttpStatusCode.OK, searchReqOnly.StatusCode);
        var pagedReq = (await searchReqOnly.Content.ReadFromJsonAsync<RecordSearchPagedResult>())!;
        Assert.All(pagedReq.Items, i => Assert.Equal("REQUEST", i.RecordType));

        // 7. Non-terminal archival attempt returns 409 Conflict
        var nonTerminalArchiveRes = await managerClient.PostAsync($"{RecordsEndpoint}/task/{createdTask.TaskId}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, nonTerminalArchiveRes.StatusCode);

        // 8. Employee (lacking records.archive permission) gets 403 Forbidden
        var empArchiveRes = await employeeClient.PostAsync($"{RecordsEndpoint}/task/{createdTask.TaskId}/archive", null);
        Assert.Equal(HttpStatusCode.Forbidden, empArchiveRes.StatusCode);

        // 9. Simulate task moving to COMPLETED and request to CLOSED in database
        await using (var db = database.Create(env.Manager.User, env.TenantId))
        {
            var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Task" SET "Status"='COMPLETED', "CompletedAt"={now}, "UpdatedAt"={now}
                WHERE "TaskId"={createdTask.TaskId};
                UPDATE "Request" SET "Status"='CLOSED', "ClosedAt"={now}, "UpdatedAt"={now}
                WHERE "RequestId"={createdRequest.RequestId};
                """);
        }

        // 10. Archive the task as manager
        var archiveTaskRes = await managerClient.PostAsync($"{RecordsEndpoint}/task/{createdTask.TaskId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveTaskRes.StatusCode);
        var archiveTaskResult = (await archiveTaskRes.Content.ReadFromJsonAsync<RecordArchivalResult>())!;
        Assert.Equal(createdTask.TaskId, archiveTaskResult.RecordId);
        Assert.Equal("TASK", archiveTaskResult.RecordType);
        Assert.Equal("COMPLETED", archiveTaskResult.Status);

        // 11. Archiving again returns 409 Conflict (already archived)
        var reArchiveRes = await managerClient.PostAsync($"{RecordsEndpoint}/task/{createdTask.TaskId}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, reArchiveRes.StatusCode);

        // 12. Search without includeArchived -> archived task is omitted
        var searchActiveRes = await employeeClient.GetAsync($"{RecordsEndpoint}/search?q=Alpha");
        Assert.Equal(HttpStatusCode.OK, searchActiveRes.StatusCode);
        var pagedActive = (await searchActiveRes.Content.ReadFromJsonAsync<RecordSearchPagedResult>())!;
        Assert.DoesNotContain(pagedActive.Items, i => i.Id == createdTask.TaskId);

        // 13. Search with includeArchived=true -> archived task is returned with isArchived=true
        var searchArchivedRes = await employeeClient.GetAsync($"{RecordsEndpoint}/search?q=Alpha&includeArchived=true");
        Assert.Equal(HttpStatusCode.OK, searchArchivedRes.StatusCode);
        var pagedArchived = (await searchArchivedRes.Content.ReadFromJsonAsync<RecordSearchPagedResult>())!;
        var archivedItem = Assert.Single(pagedArchived.Items, i => i.Id == createdTask.TaskId);
        Assert.True(archivedItem.IsArchived);
        Assert.NotNull(archivedItem.DeletedAt);

        // 14. Archive the request as manager
        var archiveReqRes = await managerClient.PostAsync($"{RecordsEndpoint}/request/{createdRequest.RequestId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveReqRes.StatusCode);
        var archiveReqResult = (await archiveReqRes.Content.ReadFromJsonAsync<RecordArchivalResult>())!;
        Assert.Equal(createdRequest.RequestId, archiveReqResult.RecordId);
        Assert.Equal("REQUEST", archiveReqResult.RecordType);
        Assert.Equal("CLOSED", archiveReqResult.Status);

        // 15. Verify structured AuditLog records in PostgreSQL
        await using (var db = database.Create(env.Manager.User, env.TenantId))
        {
            var auditLogs = await db.AuditLogs
                .Where(a => a.TenantId == env.TenantId && a.Action == "RECORD.ARCHIVED")
                .ToListAsync();

            Assert.Equal(2, auditLogs.Count);
            Assert.Contains(auditLogs, a => a.ObjectId == createdTask.TaskId && a.ObjectType == "Task");
            Assert.Contains(auditLogs, a => a.ObjectId == createdRequest.RequestId && a.ObjectType == "Request");
        }
    }

    private static async Task LoginAsync(HttpClient client, TestUser a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = a.EmpCode, password = Password, tenantKey = a.TenantKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record TestUser(Guid User, string EmpCode, string TenantKey);
    private sealed record RecordTestEnvironment(Guid TenantId, Guid ServiceId, Guid CategoryId, TestUser Manager, TestUser Employee);

    private async Task<RecordTestEnvironment> SeedEnvironmentAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var itDept = Department.Create(tenant.Id, "ENG", "Engineering", null, now);
        db.Departments.Add(itDept);

        var service = InternalService.Create(tenant.Id, "DIAG", "Diagnostics Service", null, true, now);
        db.Services.Add(service);

        var category = ServiceCategory.Create(service.Id, "SYS", "System Health");
        db.ServiceCategories.Add(category);

        await db.SaveChangesAsync();

        var hasher = new PasswordHasher<UserAccount>();

        var manager = UserAccount.CreateTenantUser(tenant.Id, "MGR_REC", "mgr_rec@example.test", "Manager Rec", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(manager, hasher.HashPassword(manager, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(manager, itDept.Id);
        db.Users.Add(manager);

        var employee = UserAccount.CreateTenantUser(tenant.Id, "EMP_REC", "emp_rec@example.test", "Employee Rec", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(employee, hasher.HashPassword(employee, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(employee, itDept.Id);
        db.Users.Add(employee);

        await db.SaveChangesAsync();

        var empRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        var mgrRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");

        db.UserRoles.Add(new(manager.Id, mgrRole.Id));
        db.UserRoles.Add(new(employee.Id, empRole.Id));

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        await db.SaveChangesAsync();

        return new(
            tenant.Id,
            service.Id,
            category.Id,
            new(manager.Id, manager.EmployeeCode, tenant.TenantKey),
            new(employee.Id, employee.EmployeeCode, tenant.TenantKey));
    }
}
