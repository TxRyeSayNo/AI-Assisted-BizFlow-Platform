using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BizFlow.IntegrationTests.Persistence;

[Collection("Postgres")]
public sealed class AttachmentApiTests(PostgresFixture database)
{
    private const string Password = "Attachment-tests!98234";
    private const string AttachmentsEndpoint = "/api/v1/attachments";
    private const string TasksEndpoint = "/api/v1/tasks";
    private const string RequestsEndpoint = "/api/v1/requests";

    [Fact]
    public async Task Attachment_upload_session_binary_upload_finalize_download_and_soft_delete_flow()
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
            title = "Prepare audit deliverables",
            description = "Gather evidence documents for ISO 27001",
            priority = "HIGH"
        });
        Assert.Equal(HttpStatusCode.Created, taskRes.StatusCode);
        var createdTask = (await taskRes.Content.ReadFromJsonAsync<TaskCreatedView>())!;

        // 2. Prepare random binary payload and its SHA-256 hash
        var payloadBytes = new byte[2048];
        Random.Shared.NextBytes(payloadBytes);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

        // 3. Create upload session via POST /api/v1/attachments/upload-session with Idempotency-Key
        var idempotencyKey = Guid.NewGuid().ToString("N");
        using var reqMsg1 = new HttpRequestMessage(HttpMethod.Post, $"{AttachmentsEndpoint}/upload-session")
        {
            Content = JsonContent.Create(new
            {
                objectType = "TASK",
                objectId = createdTask.TaskId,
                fileName = "audit-checklist.pdf",
                contentType = "application/pdf",
                sizeBytes = (long)payloadBytes.Length
            })
        };
        reqMsg1.Headers.Add("Idempotency-Key", idempotencyKey);
        var sessionRes1 = await requesterClient.SendAsync(reqMsg1);
        Assert.Equal(HttpStatusCode.Created, sessionRes1.StatusCode);
        var sessionView1 = (await sessionRes1.Content.ReadFromJsonAsync<UploadSessionResult>())!;
        Assert.NotEqual(Guid.Empty, sessionView1.AttachmentId);
        Assert.False(string.IsNullOrWhiteSpace(sessionView1.UploadUrl));
        Assert.False(string.IsNullOrWhiteSpace(sessionView1.ObjectKey));

        // 4. Replay with the same Idempotency-Key returns cached session
        using var reqMsg2 = new HttpRequestMessage(HttpMethod.Post, $"{AttachmentsEndpoint}/upload-session")
        {
            Content = JsonContent.Create(new
            {
                objectType = "TASK",
                objectId = createdTask.TaskId,
                fileName = "audit-checklist.pdf",
                contentType = "application/pdf",
                sizeBytes = (long)payloadBytes.Length
            })
        };
        reqMsg2.Headers.Add("Idempotency-Key", idempotencyKey);
        var sessionRes2 = await requesterClient.SendAsync(reqMsg2);
        Assert.Equal(HttpStatusCode.Created, sessionRes2.StatusCode);
        var replaySession = (await sessionRes2.Content.ReadFromJsonAsync<UploadSessionResult>())!;
        Assert.Equal(sessionView1.AttachmentId, replaySession.AttachmentId);

        // 5. Upload binary bytes to the session endpoint via PUT
        using var binaryContent = new ByteArrayContent(payloadBytes);
        binaryContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var putRes = await requesterClient.PutAsync(sessionView1.UploadUrl, binaryContent);
        Assert.Equal(HttpStatusCode.NoContent, putRes.StatusCode);

        // 6. Finalize the attachment with the payload SHA-256 hash
        var finalizeRes = await requesterClient.PostAsJsonAsync($"{AttachmentsEndpoint}/finalize", new
        {
            attachmentId = sessionView1.AttachmentId,
            clientHash = payloadHash
        });
        Assert.Equal(HttpStatusCode.OK, finalizeRes.StatusCode);
        var finalizedItem = (await finalizeRes.Content.ReadFromJsonAsync<AttachmentItemView>())!;
        Assert.Equal(sessionView1.AttachmentId, finalizedItem.AttachmentId);
        Assert.Equal("audit-checklist.pdf", finalizedItem.FileName);
        Assert.Equal("application/pdf", finalizedItem.ContentType);
        Assert.Equal(payloadBytes.Length, finalizedItem.SizeBytes);
        Assert.Equal(payloadHash, finalizedItem.Hash);
        Assert.Equal("READY", finalizedItem.Status);
        Assert.True(finalizedItem.IsOwner);

        // 7. Query task attachments via sub-resource route GET /api/v1/tasks/{id}/attachments
        var taskAttachmentsRes = await requesterClient.GetAsync($"{TasksEndpoint}/{createdTask.TaskId}/attachments");
        Assert.Equal(HttpStatusCode.OK, taskAttachmentsRes.StatusCode);
        var attachments = (await taskAttachmentsRes.Content.ReadFromJsonAsync<List<AttachmentItemView>>())!;
        Assert.Single(attachments);
        Assert.Equal(sessionView1.AttachmentId, attachments[0].AttachmentId);
        Assert.Equal("audit-checklist.pdf", attachments[0].FileName);
        Assert.Equal("READY", attachments[0].Status);

        // 8. Download the attachment binary and verify byte-for-byte integrity
        var downloadRes = await requesterClient.GetAsync($"{AttachmentsEndpoint}/{sessionView1.AttachmentId}/download");
        Assert.Equal(HttpStatusCode.OK, downloadRes.StatusCode);
        Assert.Equal("application/pdf", downloadRes.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await downloadRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(payloadBytes, downloadedBytes);

        // 9. Bystander attempts to delete -> 403 Forbidden
        var bystanderDeleteRes = await bystanderClient.DeleteAsync($"{AttachmentsEndpoint}/{sessionView1.AttachmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, bystanderDeleteRes.StatusCode);

        // 10. Requester soft-deletes own attachment via DELETE /api/v1/attachments/{id}
        var requesterDeleteRes = await requesterClient.DeleteAsync($"{AttachmentsEndpoint}/{sessionView1.AttachmentId}");
        Assert.Equal(HttpStatusCode.NoContent, requesterDeleteRes.StatusCode);

        // 11. Query attachments again: list is now empty
        var afterDeleteList = await requesterClient.GetFromJsonAsync<List<AttachmentItemView>>($"{TasksEndpoint}/{createdTask.TaskId}/attachments");
        Assert.NotNull(afterDeleteList);
        Assert.Empty(afterDeleteList);
    }

    [Fact]
    public async Task Attachment_validation_and_size_limits()
    {
        var env = await SeedEnvironmentAsync();
        await using var factory = new AuthenticationFactory(database);
        using var requesterClient = factory.CreateClient();
        using var managerClient = factory.CreateClient();
        await LoginAsync(requesterClient, env.Requester);
        await LoginAsync(managerClient, env.Manager);

        var taskRes = await managerClient.PostAsJsonAsync(TasksEndpoint, new
        {
            title = "Task for validation test",
            description = "Validation boundary checks",
            priority = "LOW"
        });
        Assert.Equal(HttpStatusCode.Created, taskRes.StatusCode);
        var createdTask = (await taskRes.Content.ReadFromJsonAsync<TaskCreatedView>())!;

        // 1. Exceeds 500 MB (524288000 bytes) -> 422 Unprocessable Entity
        var excessiveSizeRes = await requesterClient.PostAsJsonAsync($"{AttachmentsEndpoint}/upload-session", new
        {
            objectType = "TASK",
            objectId = createdTask.TaskId,
            fileName = "massive-database.bak",
            contentType = "application/zip",
            sizeBytes = 524288001L // 500 MB + 1 byte
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, excessiveSizeRes.StatusCode);

        // 2. Disallowed Content-Type (e.g. application/x-msdownload) -> 422 Unprocessable Entity
        var disallowedTypeRes = await requesterClient.PostAsJsonAsync($"{AttachmentsEndpoint}/upload-session", new
        {
            objectType = "TASK",
            objectId = createdTask.TaskId,
            fileName = "installer.exe",
            contentType = "application/x-msdownload",
            sizeBytes = 1048576L
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, disallowedTypeRes.StatusCode);

        // 3. Valid session but finalize with mismatched hash -> 422 Unprocessable Entity
        var validSessionRes = await requesterClient.PostAsJsonAsync($"{AttachmentsEndpoint}/upload-session", new
        {
            objectType = "TASK",
            objectId = createdTask.TaskId,
            fileName = "notes.txt",
            contentType = "text/plain",
            sizeBytes = 5L
        });
        Assert.Equal(HttpStatusCode.Created, validSessionRes.StatusCode);
        var session = (await validSessionRes.Content.ReadFromJsonAsync<UploadSessionResult>())!;

        using var content = new ByteArrayContent("hello"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var putRes = await requesterClient.PutAsync(session.UploadUrl, content);
        Assert.Equal(HttpStatusCode.NoContent, putRes.StatusCode);

        var badHashRes = await requesterClient.PostAsJsonAsync($"{AttachmentsEndpoint}/finalize", new
        {
            attachmentId = session.AttachmentId,
            clientHash = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badHashRes.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, TestUser a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = a.EmpCode, password = Password, tenantKey = a.TenantKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record TestUser(Guid User, string EmpCode, string TenantKey);
    private sealed record AttachmentTestEnvironment(Guid TenantId, TestUser Requester, TestUser Manager, TestUser Bystander);

    private async Task<AttachmentTestEnvironment> SeedEnvironmentAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var itDept = Department.Create(tenant.Id, "IT", "Information Technology", null, now);
        db.Departments.Add(itDept);
        await db.SaveChangesAsync();

        var hasher = new PasswordHasher<UserAccount>();

        var requester = UserAccount.CreateTenantUser(tenant.Id, "REQ_A01", "req_a01@example.test", "Requester User", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(requester, hasher.HashPassword(requester, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(requester, itDept.Id);
        db.Users.Add(requester);

        var manager = UserAccount.CreateTenantUser(tenant.Id, "MGR_A01", "mgr_a01@example.test", "Manager User", "hash", now);
        typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash))!.SetValue(manager, hasher.HashPassword(manager, Password));
        typeof(UserAccount).GetProperty(nameof(UserAccount.DepartmentId))!.SetValue(manager, itDept.Id);
        db.Users.Add(manager);

        var bystander = UserAccount.CreateTenantUser(tenant.Id, "BYST_A01", "byst_a01@example.test", "Bystander User", "hash", now);
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
