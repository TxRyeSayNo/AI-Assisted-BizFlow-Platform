using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Messaging;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for direct messages (F13). The privacy boundary is the point of this file:
/// a conversation belongs to its participants, so a colleague of the same tenant — and even a
/// company administrator — must not be able to read it, and a conversation they are not part of
/// has to look missing rather than forbidden.
/// </summary>
[Collection("Postgres")]
public sealed class DirectMessageApiTests(PostgresFixture database)
{
    private const string Password = "Direct-message-test!3307";
    private const string Conversations = "/api/v1/messages/conversations";

    [Fact]
    public async Task Opening_a_chat_with_a_colleague_is_idempotent_from_either_side()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");

        var opened = await OpenAsync(mine, a.Colleague, factory);
        Assert.Equal(a.Colleague, opened.OtherUserId);
        Assert.Equal("Đồng nghiệp", opened.OtherUserName);
        Assert.Equal("COLLEAGUE", opened.OtherUserEmployeeCode);

        // Opening it again returns the same conversation, not a second one.
        Assert.Equal(opened.ConversationId, (await OpenAsync(mine, a.Colleague, factory)).ConversationId);

        // And so does the other person opening it from their side.
        using var theirs = factory.CreateClient();
        await LoginAsync(theirs, a, "COLLEAGUE");
        var fromTheOtherSide = await OpenAsync(theirs, a.User, factory);
        Assert.Equal(opened.ConversationId, fromTheOtherSide.ConversationId);
        Assert.Equal(a.User, fromTheOtherSide.OtherUserId);

        await using var db = database.Create(a.User, a.Tenant);
        Assert.Single(await db.ChatConversations.AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.ChatParticipants.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task A_message_is_delivered_once_even_when_the_send_is_retried()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        var conversation = (await OpenAsync(mine, a.Colleague, factory)).ConversationId;

        var key = Guid.NewGuid().ToString();
        var first = await SendAsync(mine, conversation, "Chào bạn, xem giúp mình nhé.", factory, key);
        Assert.True(first.Mine);
        Assert.Equal(a.User, first.SenderId);
        Assert.Equal("Chào bạn, xem giúp mình nhé.", first.Body);

        // Same key, same body: the stored message comes back instead of a duplicate.
        var replay = await SendAsync(mine, conversation, "Chào bạn, xem giúp mình nhé.", factory, key);
        Assert.Equal(first.MessageId, replay.MessageId);

        var page = (await mine.GetFromJsonAsync<MessagePage>($"{Conversations}/{conversation}"))!;
        Assert.Equal(1, page.Total);
        Assert.Equal(conversation, page.ConversationId);
    }

    [Fact]
    public async Task The_recipient_sees_the_message_as_theirs_to_read_and_the_unread_count_clears()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        var conversation = (await OpenAsync(mine, a.Colleague, factory)).ConversationId;
        await SendAsync(mine, conversation, "Tin nhắn đầu tiên", factory);
        await SendAsync(mine, conversation, "Tin nhắn thứ hai", factory);

        using var theirs = factory.CreateClient();
        await LoginAsync(theirs, a, "COLLEAGUE");

        var inbox = (await theirs.GetFromJsonAsync<ConversationPage>(Conversations))!;
        var row = Assert.Single(inbox.Items);
        Assert.Equal(conversation, row.ConversationId);
        Assert.Equal(a.User, row.OtherUserId);
        Assert.Equal(2, row.UnreadCount);
        Assert.Equal("Tin nhắn thứ hai", row.LastMessagePreview);
        Assert.NotNull(row.LastMessageAt);

        var read = (await theirs.GetFromJsonAsync<MessagePage>($"{Conversations}/{conversation}"))!;
        Assert.Equal(2, read.Total);
        // The sender flag is relative to whoever is reading.
        Assert.All(read.Items, item => Assert.False(item.Mine));

        using var marked = await theirs.PutAsJsonAsync($"{Conversations}/{conversation}/read", new { });
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(0, Assert.Single((await theirs.GetFromJsonAsync<ConversationPage>(Conversations))!.Items).UnreadCount);

        // The sender's own view never counts their own messages as unread.
        Assert.Equal(0, Assert.Single((await mine.GetFromJsonAsync<ConversationPage>(Conversations))!.Items).UnreadCount);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task A_colleague_outside_the_conversation_cannot_reach_it_even_as_an_administrator()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        var conversation = (await OpenAsync(mine, a.Colleague, factory)).ConversationId;
        await SendAsync(mine, conversation, "Nội dung riêng tư", factory);

        // The outsider is a COMPANY_ADMIN of the same tenant and still gets nothing.
        using var outsider = factory.CreateClient();
        await LoginAsync(outsider, a, "ADMIN01");

        // Reported as missing, not forbidden, so the endpoint cannot be used to discover that two
        // colleagues are talking.
        using var read = await outsider.GetAsync($"{Conversations}/{conversation}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PostAsJsonAsync($"{Conversations}/{conversation}", new { body = "chen vao" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PutAsJsonAsync($"{Conversations}/{conversation}/read", new { })).StatusCode);
        // Their own conversation list stays empty.
        Assert.Empty((await outsider.GetFromJsonAsync<ConversationPage>(Conversations))!.Items);
        var body = await read.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Nội dung riêng tư", body);
    }

    [Fact]
    public async Task One_tenant_can_never_open_a_chat_with_another_tenants_member()
    {
        var a = await SeedAsync();
        var foreign = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        mine.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.Tenant.ToString());

        // A user id from another workspace does not exist as far as this caller is concerned.
        Assert.Equal(HttpStatusCode.NotFound,
            (await mine.PostAsJsonAsync(Conversations, new { userId = foreign.Colleague })).StatusCode);

        // And a conversation created over there is unreachable from here.
        using var theirs = factory.CreateClient();
        await LoginAsync(theirs, foreign, "EMP001");
        var foreignConversation = (await OpenAsync(theirs, foreign.Colleague)).ConversationId;
        Assert.Equal(HttpStatusCode.NotFound, (await mine.GetAsync($"{Conversations}/{foreignConversation}")).StatusCode);
        Assert.Empty((await mine.GetFromJsonAsync<ConversationPage>(Conversations))!.Items);
    }

    [Fact]
    public async Task Only_the_sender_may_edit_or_delete_and_a_deleted_message_keeps_no_words()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        var conversation = (await OpenAsync(mine, a.Colleague, factory)).ConversationId;
        var sent = await SendAsync(mine, conversation, "Bản nháp", factory);

        var edited = (await (await mine.PutAsJsonAsync($"{Conversations}/{conversation}/{sent.MessageId}",
            new { body = "Bản đã sửa" })).Content.ReadFromJsonAsync<MessageRow>())!;
        Assert.Equal("Bản đã sửa", edited.Body);
        Assert.NotNull(edited.EditedAt);

        // The recipient cannot edit or delete someone else's message.
        using var theirs = factory.CreateClient();
        await LoginAsync(theirs, a, "COLLEAGUE");
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.PutAsJsonAsync($"{Conversations}/{conversation}/{sent.MessageId}",
                new { body = "sửa hộ" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await theirs.DeleteAsync($"{Conversations}/{conversation}/{sent.MessageId}")).StatusCode);

        var deleted = (await (await mine.DeleteAsync($"{Conversations}/{conversation}/{sent.MessageId}"))
            .Content.ReadFromJsonAsync<MessageRow>())!;
        Assert.True(deleted.Deleted);
        Assert.Equal("", deleted.Body);

        // The row survives so the conversation keeps its shape, but the words are gone from storage.
        await using var db = database.Create(a.User, a.Tenant);
        var stored = Assert.Single(await db.ChatMessages.AsNoTracking().ToListAsync());
        Assert.Equal("", stored.Body);
        Assert.NotNull(stored.DeletedAt);
        // And the recipient no longer sees the text either.
        Assert.All((await theirs.GetFromJsonAsync<MessagePage>($"{Conversations}/{conversation}"))!.Items,
            item => Assert.Equal("", item.Body));
    }

    [Fact]
    public async Task Input_is_validated_before_anything_is_stored()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        var conversation = (await OpenAsync(mine, a.Colleague, factory)).ConversationId;

        // You cannot talk to yourself, to nobody, or to someone who is not active.
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await mine.PostAsJsonAsync(Conversations, new { userId = a.User })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await mine.PostAsJsonAsync(Conversations, new { userId = Guid.Empty })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await mine.PostAsJsonAsync(Conversations, new { userId = a.Inactive })).StatusCode);

        foreach (var body in new[] { "", "   ", new string('x', 4001) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                (await mine.PostAsJsonAsync($"{Conversations}/{conversation}", new { body })).StatusCode);

        foreach (var query in new[] { "?page=0", "?pageSize=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity,
                (await mine.GetAsync($"{Conversations}{query}")).StatusCode);

        // An unknown conversation is missing, not a server error.
        Assert.Equal(HttpStatusCode.NotFound, (await mine.GetAsync($"{Conversations}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_messaging_and_leaves_comments_and_notifications_alone()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:DirectMessages"] = "false" });
        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");

        Assert.Equal(HttpStatusCode.NotFound, (await mine.GetAsync(Conversations)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await mine.PostAsJsonAsync(Conversations, new { userId = a.Colleague })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mine.GetAsync($"{Conversations}/{Guid.NewGuid()}")).StatusCode);

        // The notification inbox that existed before keeps answering exactly as it did.
        Assert.Equal(HttpStatusCode.OK, (await mine.GetAsync("/api/v1/notifications")).StatusCode);
    }

    [Fact]
    public async Task Messaging_requires_a_session_and_is_never_cached()
    {
        var a = await SeedAsync();
        await using var factory = new AuthenticationFactory(database);
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Conversations)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(Conversations, new { userId = a.Colleague })).StatusCode);

        using var mine = factory.CreateClient();
        await LoginAsync(mine, a, "EMP001");
        using var response = await mine.GetAsync(Conversations);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Pass the factory to also assert no server exception was raised. Omit it after a test has
    /// deliberately triggered a fault, because the observer keeps the last one it saw.
    /// </summary>
    private static async Task<ConversationRow> OpenAsync(HttpClient client, Guid otherUserId,
        AuthenticationFactory? factory = null)
    {
        using var response = await client.PostAsJsonAsync(Conversations, new { userId = otherUserId });
        if (factory is not null) Assert.Null(factory.LastServerError);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationRow>())!;
    }

    private static async Task<MessageRow> SendAsync(HttpClient client, Guid conversationId, string body,
        AuthenticationFactory factory, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Conversations}/{conversationId}")
        {
            Content = JsonContent.Create(new { body })
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var response = await client.SendAsync(request);
        Assert.Null(factory.LastServerError);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageRow>())!;
    }

    private async Task<Account> SeedAsync()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var department = Department.Create(tenant.Id, "OURS", "Phòng của chúng tôi", null, now);
        db.Add(department);
        await db.SaveChangesAsync();

        var hasher = new PasswordHasher<UserAccount>();
        var colleague = UserAccount.CreateTenantUser(tenant.Id, "COLLEAGUE", "colleague@example.test",
            "Đồng nghiệp", "fixture-only", now, department.Id);
        var admin = UserAccount.CreateTenantUser(tenant.Id, "ADMIN01", "admin01@example.test",
            "Quản trị viên", "fixture-only", now, department.Id);
        var inactive = UserAccount.CreateTenantUser(tenant.Id, "LEAVER", "leaver@example.test",
            "Đã nghỉ", "fixture-only", now, department.Id);
        db.AddRange(colleague, admin, inactive);
        await db.SaveChangesAsync();

        var hash = hasher.HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={department.Id}
                WHERE "UserId" IN ({user.Id}, {colleague.Id}, {admin.Id}, {inactive.Id});
            UPDATE "User" SET "Status"='INACTIVE' WHERE "UserId"={inactive.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            """);

        var employee = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
        var companyAdmin = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
        db.UserRoles.AddRange(new(user.Id, employee.Id), new(colleague.Id, employee.Id),
            new(admin.Id, companyAdmin.Id));
        await db.SaveChangesAsync();

        return new(user.Id, tenant.Id, tenant.TenantKey, colleague.Id, admin.Id, inactive.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a, string employeeCode)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = employeeCode, password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid Colleague, Guid Admin, Guid Inactive);
}
