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
    public async Task Nicknames_are_used_for_discussion_but_official_names_remain_in_tasks_and_requests()
    {
        var env = await SeedEnvironmentAsync();
        await using (var setup = database.Create(env.Manager.User, env.TenantId))
        {
            var actor = await setup.Users.SingleAsync(u => u.Id == env.Manager.User);
            setup.ManagementScopes.Add(ManagementScope.Create(env.TenantId, actor.Id, actor.DepartmentId!.Value, true, actor.Id, DateTimeOffset.UtcNow));
            await setup.SaveChangesAsync();
        }
        await using var factory = new AuthenticationFactory(database);
        using var manager = factory.CreateClient(); using var requester = factory.CreateClient();
        await LoginAsync(manager, env.Manager); await LoginAsync(requester, env.Requester);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PatchAsJsonAsync("/api/v1/auth/profile", new { nickname = "BossAlias", fullName = "Spoofed name", roles = new[] { "PLATFORM_ADMIN" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await requester.PatchAsJsonAsync("/api/v1/auth/profile", new { nickname = "SupportAlias" })).StatusCode);
        var profile = (await manager.GetFromJsonAsync<PersonalProfile>("/api/v1/auth/profile"))!;
        Assert.Equal("Manager User", profile.FullName); Assert.Equal("BossAlias", profile.Nickname); Assert.Contains("MANAGER", profile.Roles); Assert.DoesNotContain("PLATFORM_ADMIN", profile.Roles);
        var created = await manager.PostAsJsonAsync(TasksEndpoint, new { title = "Identity regression", priority = "MEDIUM" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<TaskCreatedView>())!;
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync($"{TasksEndpoint}/{task.TaskId}/assign", new { targetType = "USER", targetId = env.Requester.User })).StatusCode);
        var detail = (await manager.GetFromJsonAsync<TaskDetailView>($"{TasksEndpoint}/{task.TaskId}"))!;
        Assert.Equal("Manager User", detail.CreatorName); Assert.Equal("Requester User", detail.Task.AssignedUserName);
        var key = Guid.NewGuid().ToString("N");
        async Task<CommentItemView> SendAsync()
        {
            using var input = new HttpRequestMessage(HttpMethod.Post, CommentsEndpoint) { Content = JsonContent.Create(new { objectType = "TASK", objectId = task.TaskId, content = "Nickname discussion" }) };
            input.Headers.Add("Idempotency-Key", key);
            using var result = await manager.SendAsync(input); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            return (await result.Content.ReadFromJsonAsync<CommentItemView>())!;
        }
        foreach (var comment in new[] { await SendAsync(), await SendAsync() })
        { Assert.Equal("BossAlias", comment.AuthorNickname); Assert.Equal("Manager User", comment.AuthorName); Assert.Contains("MANAGER", comment.AuthorRoles!); }
        var listed = Assert.Single((await manager.GetFromJsonAsync<List<CommentItemView>>($"{TasksEndpoint}/{task.TaskId}/comments"))!);
        Assert.Equal("BossAlias", listed.AuthorNickname); Assert.Equal("Manager User", listed.AuthorName);
        await using var db = database.Create(env.Requester.User, env.TenantId);
        var now = DateTimeOffset.UtcNow;
        var service = InternalService.Create(env.TenantId, "IDENTITY", "Identity service", null, true, now);
        var category = ServiceCategory.Create(service.Id, "SUPPORT", "Support");
        db.Services.Add(service); db.ServiceCategories.Add(category); await db.SaveChangesAsync();
        using var requestResult = await requester.PostAsJsonAsync(RequestsEndpoint, new { serviceId = service.Id, categoryId = category.Id, title = "Official requester", description = "Keep official identity", priority = "MEDIUM" });
        Assert.Equal(HttpStatusCode.Created, requestResult.StatusCode);
        var requestJson = await requestResult.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var requestId = requestJson.GetProperty("requestId").GetGuid();
        var requestDetail = await requester.GetFromJsonAsync<System.Text.Json.JsonElement>($"{RequestsEndpoint}/{requestId}");
        Assert.Equal("Requester User", requestDetail.GetProperty("requesterName").GetString());
    }

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
