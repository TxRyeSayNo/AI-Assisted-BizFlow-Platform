using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BizFlow.IntegrationTests.Authentication;

[Collection("Postgres")]
public sealed class PersonalProfileTests(PostgresFixture database)
{
    private const string OldPassword = "Profile-test-password!123";
    private const string NewPassword = "Profile-new-passphrase!456";

    [Fact]
    public async Task Profile_requires_authentication_and_only_updates_the_authenticated_owner()
    {
        var a = await SeedAsync(); var b = await SeedAsync();
        await using var factory = Factory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/profile")).StatusCode);
        await LoginAsync(client, a.Key);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", b.TenantId.ToString());
        var result = (await client.GetFromJsonAsync<PersonalProfile>("/api/v1/auth/profile"))!;
        Assert.Equal(a.UserId, result.UserId);
        Assert.Equal(a.Key, result.TenantKey);
        Assert.Equal(result.FullName, result.Nickname);
        using var update = await client.PatchAsJsonAsync("/api/v1/auth/profile", new { nickname = "Updated Nickname", fullName = "Forbidden Name", roles = new[] { "PLATFORM_ADMIN" }, userId = b.UserId, tenantId = b.TenantId, email = "stolen@example.test", departmentId = Guid.NewGuid(), isPlatformAdministrator = true });
        Assert.True(update.StatusCode == HttpStatusCode.NoContent, factory.LastServerError?.ToString());
        await using var db = database.Create(a.UserId, a.TenantId);
        var owner = await db.Users.SingleAsync();
        Assert.Equal("Test Employee", owner.FullName); Assert.Equal("Updated Nickname", owner.Nickname); Assert.Equal("employee@example.test", owner.Email); Assert.Null(owner.DepartmentId); Assert.False(owner.IsPlatformAdministrator);
        await using var other = database.Create(b.UserId, b.TenantId);
        Assert.Equal("Test Employee", (await other.Users.SingleAsync()).FullName);
        using var officialOnly = await client.PatchAsJsonAsync("/api/v1/auth/profile", new { fullName = "Forbidden Name" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, officialOnly.StatusCode);
        var changed = (await client.GetFromJsonAsync<PersonalProfile>("/api/v1/auth/profile"))!;
        Assert.Equal("Test Employee", changed.FullName); Assert.Equal("Updated Nickname", changed.Nickname);
        Assert.Equal(result.Roles, changed.Roles);
        using var empty = await client.PatchAsJsonAsync("/api/v1/auth/profile", new { nickname = " " });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);
    }

    [Fact]
    public async Task Password_change_verifies_current_password_and_revokes_all_existing_sessions()
    {
        var a = await SeedAsync(); await using var factory = Factory(); using var client = factory.CreateClient();
        var first = await LoginAsync(client, a.Key); var second = await LoginAsync(client, a.Key);
        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/profile/change-password", new { currentPassword = "wrong", newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        using var weak = await client.PostAsJsonAsync("/api/v1/auth/profile/change-password", new { currentPassword = OldPassword, newPassword = "weak" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        using var unchanged = await client.PostAsJsonAsync("/api/v1/auth/profile/change-password", new { currentPassword = OldPassword, newPassword = OldPassword });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unchanged.StatusCode);
        using var success = await client.PostAsJsonAsync("/api/v1/auth/profile/change-password", new { currentPassword = OldPassword, newPassword = NewPassword });
        Assert.True(success.StatusCode == HttpStatusCode.NoContent, factory.LastServerError?.ToString());
        foreach (var session in new[] { first, second })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/profile")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { session.RefreshToken })).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "EMP001", password = OldPassword, tenantKey = a.Key })).StatusCode);
        await LoginAsync(client, a.Key, NewPassword);
        await using var db = database.Create(a.UserId, a.TenantId);
        Assert.True(await db.AuditLogs.AnyAsync(l => l.Action == "AUTH.PASSWORD_CHANGED"));
        Assert.DoesNotContain(NewPassword, string.Join(" ", await db.AuditLogs.Select(l => l.MetadataJson).ToArrayAsync()));
    }

    [Fact]
    public async Task Repeated_incorrect_current_password_locks_the_account()
    {
        var a = await SeedAsync(); await using var factory = Factory(); using var client = factory.CreateClient();
        await LoginAsync(client, a.Key);
        for (int i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/v1/auth/profile/change-password", new { currentPassword = "wrong", newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/profile")).StatusCode);
    }

    [Fact]
    public async Task Avatar_is_persisted_private_validated_replaceable_and_removable()
    {
        var a = await SeedAsync(); var b = await SeedAsync(); var storage = new ProfileStorage();
        await using var factory = Factory(storage); using var client = factory.CreateClient();
        await LoginAsync(client, a.Key);
        var png = Png();
        using var data = Upload(png);
        using var upload = await client.PutAsync("/api/v1/auth/profile/avatar", data);
        Assert.True(upload.StatusCode == HttpStatusCode.NoContent, factory.LastServerError?.ToString());
        Assert.True((await client.GetFromJsonAsync<PersonalProfile>("/api/v1/auth/profile"))!.HasAvatar);
        using var downloaded = await client.GetAsync("/api/v1/auth/profile/avatar");
        Assert.Equal(png, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", downloaded.Content.Headers.ContentType?.MediaType);
        Assert.True(downloaded.Headers.CacheControl?.NoStore);
        await LoginAsync(client, b.Key);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/auth/profile/avatar")).StatusCode);
        await LoginAsync(client, a.Key);
        using var replacement = Upload(png);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/v1/auth/profile/avatar", replacement)).StatusCode);
        Assert.Single(storage.Files);
        using var fake = Upload(System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PutAsync("/api/v1/auth/profile/avatar", fake)).StatusCode);
        var corrupted = png.ToArray(); corrupted[30] ^= 0xff;
        using var corruptUpload = Upload(corrupted);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PutAsync("/api/v1/auth/profile/avatar", corruptUpload)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/auth/profile/avatar")).StatusCode);
        Assert.Empty(storage.Files);
        Assert.False((await client.GetFromJsonAsync<PersonalProfile>("/api/v1/auth/profile"))!.HasAvatar);
    }

    [Fact]
    public async Task Platform_account_can_read_and_update_its_own_profile()
    {
        await using var db = database.Create(database.PlatformUserId, null);
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, OldPassword);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}");
        await using var factory = Factory(); using var client = factory.CreateClient();
        await LoginAsync(client, null, identifier: "PLATFORM");
        Assert.Null((await client.GetFromJsonAsync<PersonalProfile>("/api/v1/auth/profile"))!.TenantKey);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync("/api/v1/auth/profile", new { nickname = "Platform nickname", fullName = "Forbidden Name" })).StatusCode);
    }

    private AuthenticationFactory Factory(ProfileStorage? storage = null) => new(database, configureServices: services =>
    {
        services.RemoveAll<IObjectStorageProvider>(); services.AddSingleton<IObjectStorageProvider>(storage ?? new ProfileStorage());
    });
    private async Task<(Guid UserId, Guid TenantId, string Key)> SeedAsync()
    {
        var a = await database.SeedTenantAsync(); await using var db = database.Create(a.UserId, a.TenantId);
        var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
        var hash = new PasswordHasher<UserAccount>().HashPassword(user, OldPassword);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"PasswordHash\"={hash} WHERE \"UserId\"={user.Id}; UPDATE \"Company\" SET \"Status\"='ACTIVE' WHERE \"CompanyId\"={tenant.CompanyId};");
        return (a.UserId, a.TenantId, tenant.TenantKey);
    }
    private static async Task<AuthenticationResponse> LoginAsync(HttpClient client, string? key, string password = OldPassword, string identifier = "EMP001")
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password, tenantKey = key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken); return session;
    }
    private static MultipartFormDataContent Upload(byte[] bytes)
    {
        var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(content, "file", "avatar.png"); return form;
    }
    private static byte[] Png()
    {
        using var stream = new MemoryStream(); stream.Write(new byte[] {137,80,78,71,13,10,26,10});
        void Chunk(string type, byte[] data)
        {
            var size = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(size, data.Length); stream.Write(size);
            var bytes = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); stream.Write(bytes);
            uint crc = 0xffffffff; foreach (var b in bytes) { crc ^= b; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u); }
            BinaryPrimitives.WriteUInt32BigEndian(size, ~crc); stream.Write(size);
        }
        var header = new byte[13]; header[3] = 1; header[7] = 1; header[8] = 8; header[9] = 6; Chunk("IHDR", header);
        using var compressed = new MemoryStream(); using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, true)) z.Write(new byte[] {0,255,0,0,255});
        Chunk("IDAT", compressed.ToArray()); Chunk("IEND", []); return stream.ToArray();
    }
    private sealed class ProfileStorage : IObjectStorageProvider
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public async Task SaveBinaryDirectAsync(string key, Stream content, CancellationToken ct) { using var ms = new MemoryStream(); await content.CopyToAsync(ms, ct); Files[key] = ms.ToArray(); }
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
        public Task<bool> DeleteFileAsync(string key, CancellationToken ct) => Task.FromResult(Files.TryRemove(key, out _));
        public string? GeneratePresignedDownloadUrl(string key, TimeSpan expiry) => null;
        public Task<StorageFileInspection> InspectFileAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<UploadSessionDescriptor> CreateUploadSessionAsync(Guid id, string key, string contentType, long size, TimeSpan expiry, CancellationToken ct) => throw new NotSupportedException();
    }
}
