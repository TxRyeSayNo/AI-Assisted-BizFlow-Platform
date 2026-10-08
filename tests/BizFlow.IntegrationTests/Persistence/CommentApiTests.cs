using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
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
public sealed class CommentApiTests(PostgresFixture database)
{
    private const string Password = "Comment-tests!98234";
    private const string CommentsEndpoint = "/api/v1/comments";
    private const string TasksEndpoint = "/api/v1/tasks";
    private const string RequestsEndpoint = "/api/v1/requests";

    [Fact]
    public async Task Comment_lifecycle_creation_replay_editing_and_soft_delete_flow()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        using var bystanderClient = factory.CreateClient();

        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);
        await LoginAsync(bystanderClient, env.Bystander);

        // 1. Create a task via manager
        var taskRes = await managerClient.PostAsJsonAsync(TasksEndpoint, new
        {
            title = "Prepare quarterly review slides",
            description = "Consolidate KPI metrics for Q3",
            priority = "MEDIUM"
        });
        Assert.Equal(HttpStatusCode.Created, taskRes.StatusCode);
        var createdTask = (await taskRes.Content.ReadFromJsonAsync<TaskCreatedView>())!;

        // 2. Requester posts a comment on the task via POST /api/v1/comments with Idempotency-Key
        var idempotencyKey = Guid.NewGuid().ToString("N");
        using var reqMsg1 = new HttpRequestMessage(HttpMethod.Post, CommentsEndpoint)
        {
            Content = JsonContent.Create(new
            {
                objectType = "TASK",
                objectId = createdTask.TaskId,
                content = "Could you please add section 4 for finance?"
            })
        };
        reqMsg1.Headers.Add("Idempotency-Key", idempotencyKey);
        var postCommentRes1 = await requesterClient.SendAsync(reqMsg1);
        Assert.Equal(HttpStatusCode.Created, postCommentRes1.StatusCode);
        var commentView1 = (await postCommentRes1.Content.ReadFromJsonAsync<CommentItemView>())!;
        Assert.Equal("TASK", commentView1.ObjectType);
        Assert.Equal(createdTask.TaskId, commentView1.ObjectId);
        Assert.Equal("Could you please add section 4 for finance?", commentView1.Content);
        Assert.True(commentView1.IsOwner);

        // 3. Replay with the same Idempotency-Key returns cached comment without creating duplicate
        using var reqMsg2 = new HttpRequestMessage(HttpMethod.Post, CommentsEndpoint)
        {
            Content = JsonContent.Create(new
            {
                objectType = "TASK",
                objectId = createdTask.TaskId,
                content = "Could you please add section 4 for finance?"
            })
        };
        reqMsg2.Headers.Add("Idempotency-Key", idempotencyKey);
        var postCommentRes2 = await requesterClient.SendAsync(reqMsg2);
        Assert.Equal(HttpStatusCode.Created, postCommentRes2.StatusCode);
        var replayView = (await postCommentRes2.Content.ReadFromJsonAsync<CommentItemView>())!;
        Assert.Equal(commentView1.CommentId, replayView.CommentId);

        // 4. Manager replies via sub-resource route POST /api/v1/tasks/{id}/comments
        var replyRes = await managerClient.PostAsJsonAsync($"{TasksEndpoint}/{createdTask.TaskId}/comments", new
        {
            content = "Sure, I have updated the finance slides."
        });
        Assert.Equal(HttpStatusCode.Created, replyRes.StatusCode);
        var managerComment = (await replyRes.Content.ReadFromJsonAsync<CommentItemView>())!;
        Assert.Equal("Sure, I have updated the finance slides.", managerComment.Content);

        // 5. Query comments via GET /api/v1/tasks/{id}/comments
        var listRes = await requesterClient.GetAsync($"{TasksEndpoint}/{createdTask.TaskId}/comments");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var comments = (await listRes.Content.ReadFromJsonAsync<List<CommentItemView>>())!;
        Assert.Equal(2, comments.Count);
        Assert.Equal(commentView1.CommentId, comments[0].CommentId);
        Assert.Equal(managerComment.CommentId, comments[1].CommentId);
        Assert.True(comments[0].IsOwner); // requester is owner of first
        Assert.False(comments[1].IsOwner); // manager is owner of second

        // 6. Requester edits own comment via PUT /api/v1/comments/{id}
        var editRes = await requesterClient.PutAsJsonAsync($"{CommentsEndpoint}/{commentView1.CommentId}", new
        {
            content = "Could you please add section 4 for finance and tax?"
        });
        Assert.Equal(HttpStatusCode.OK, editRes.StatusCode);
        var editedView = (await editRes.Content.ReadFromJsonAsync<CommentItemView>())!;
        Assert.Equal("Could you please add section 4 for finance and tax?", editedView.Content);
        Assert.NotNull(editedView.EditedAt);

        // 7. Bystander attempts to edit requester's comment -> 403 Forbidden
        var unauthorizedEditRes = await bystanderClient.PutAsJsonAsync($"{CommentsEndpoint}/{commentView1.CommentId}", new
        {
            content = "Hacked comment content"
        });
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedEditRes.StatusCode);

        // 8. Bystander attempts to delete requester's comment -> 403 Forbidden
        var unauthorizedDeleteRes = await bystanderClient.DeleteAsync($"{CommentsEndpoint}/{commentView1.CommentId}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedDeleteRes.StatusCode);

        // 9. Requester soft-deletes own comment via DELETE /api/v1/comments/{id}
        var deleteRes = await requesterClient.DeleteAsync($"{CommentsEndpoint}/{commentView1.CommentId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // 10. Verify deleted comment is no longer returned in comments list
        var listAfterDelete = await requesterClient.GetFromJsonAsync<List<CommentItemView>>($"{TasksEndpoint}/{createdTask.TaskId}/comments");
        Assert.NotNull(listAfterDelete);
        Assert.Single(listAfterDelete);
        Assert.Equal(managerComment.CommentId, listAfterDelete[0].CommentId);
    }

    private static async Task LoginAsync(HttpClient client, TestUser a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = a.EmpCode, password = Password, tenantKey = a.TenantKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record TestUser(Guid User, string EmpCode, string TenantKey);
    private sealed record CommentTestEnvironment(Guid TenantId, TestUser Requester, TestUser Manager, TestUser Bystander);

    private async Task<CommentTestEnvironment> SeedEnvironmentAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var itDept = Department.Create(tenant.Id, "IT", "Information Technology", null, now);
        db.Departments.Add(itDept);
        await db.SaveChangesAsync();

        var hasher = new PasswordHasher<UserAccount>();

        var requester = UserAccount.CreateTenantUser(tenant.Id, "REQ_C01", "req_c01@example.test", "Requester User", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(requester, hasher.HashPassword(requester, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(requester, itDept.Id);
        db.Users.Add(requester);

        var manager = UserAccount.CreateTenantUser(tenant.Id, "MGR_C01", "mgr_c01@example.test", "Manager User", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(manager, hasher.HashPassword(manager, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(manager, itDept.Id);
        db.Users.Add(manager);

        var bystander = UserAccount.CreateTenantUser(tenant.Id, "BYST_C01", "byst_c01@example.test", "Bystander User", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(bystander, hasher.HashPassword(bystander, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(bystander, itDept.Id);
        db.Users.Add(bystander);

        await db.SaveChangesAsync();

        var empRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        var mgrRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");

        db.UserRoles.Add(new(requester.Id, empRole.Id));
        db.UserRoles.Add(new(manager.Id, mgrRole.Id));
        db.UserRoles.Add(new(bystander.Id, empRole.Id));

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        await db.SaveChangesAsync();

        return new(
            tenant.Id,
            new(requester.Id, requester.EmployeeCode, tenant.TenantKey),
            new(manager.Id, manager.EmployeeCode, tenant.TenantKey),
            new(bystander.Id, bystander.EmployeeCode, tenant.TenantKey));
    }
}
