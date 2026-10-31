using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Infrastructure.Collaboration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BizFlow.IntegrationTests.Collaboration;

public sealed class S3CompatibleStorageProviderTests
{
    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> SentRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            return await responder(request);
        }
    }

    [Fact]
    public void IsConfigured_DetectsPresenceOfCredentials()
    {
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var unconfigured = new S3CompatibleStorageProvider(new HttpClient(), emptyConfig);
        Assert.False(unconfigured.IsConfigured);

        var configuredSettings = new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = "http://localhost:9000",
            ["Storage:AccessKey"] = "minioadmin",
            ["Storage:SecretKey"] = "minioadmin_secret"
        };
        var fullConfig = new ConfigurationBuilder().AddInMemoryCollection(configuredSettings).Build();
        var configured = new S3CompatibleStorageProvider(new HttpClient(), fullConfig);
        Assert.True(configured.IsConfigured);
    }

    [Fact]
    public async Task CreateUploadSessionAsync_GeneratesValidAwsSigV4PresignedUrl()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = "http://minio:9000",
            ["Storage:PublicEndpoint"] = "http://localhost:9000",
            ["Storage:Bucket"] = "bizflow-evidence",
            ["Storage:AccessKey"] = "bizflow_access",
            ["Storage:SecretKey"] = "bizflow_secret_123"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new S3CompatibleStorageProvider(new HttpClient(), config);

        var attachmentId = Guid.NewGuid();
        var objectKey = $"{Guid.NewGuid()}/{attachmentId}/report.pdf";
        var expiry = TimeSpan.FromMinutes(15);

        var session = await provider.CreateUploadSessionAsync(
            attachmentId,
            objectKey,
            "application/pdf",
            1024,
            expiry,
            CancellationToken.None);

        Assert.NotNull(session.UploadUrl);
        Assert.StartsWith("http://localhost:9000/bizflow-evidence/", session.UploadUrl);
        Assert.Contains("X-Amz-Algorithm=AWS4-HMAC-SHA256", session.UploadUrl);
        Assert.Contains("X-Amz-Credential=bizflow_access", session.UploadUrl);
        Assert.Contains("X-Amz-Signature=", session.UploadUrl);
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task InspectFileAsync_ReturnsNotFound_WhenHeadReturns404()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = "http://localhost:9000",
            ["Storage:Bucket"] = "bizflow-evidence",
            ["Storage:AccessKey"] = "key",
            ["Storage:SecretKey"] = "secret"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var provider = new S3CompatibleStorageProvider(new HttpClient(handler), config);

        var inspection = await provider.InspectFileAsync("tenant/file.txt", CancellationToken.None);

        Assert.False(inspection.Exists);
        Assert.Equal(0, inspection.SizeBytes);
        Assert.Equal(string.Empty, inspection.HashSha256);
    }

    [Fact]
    public async Task InspectFileAsync_ReturnsFileDetails_WhenObjectExists()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = "http://localhost:9000",
            ["Storage:Bucket"] = "bizflow-evidence",
            ["Storage:AccessKey"] = "key",
            ["Storage:SecretKey"] = "secret"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var fileBytes = Encoding.UTF8.GetBytes("sample attachment content");

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Head)
            {
                var headRes = new HttpResponseMessage(HttpStatusCode.OK);
                headRes.Content.Headers.ContentLength = fileBytes.Length;
                return Task.FromResult(headRes);
            }
            if (req.Method == HttpMethod.Get)
            {
                var getRes = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fileBytes)
                };
                return Task.FromResult(getRes);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        var provider = new S3CompatibleStorageProvider(new HttpClient(handler), config);

        var inspection = await provider.InspectFileAsync("tenant/file.txt", CancellationToken.None);

        Assert.True(inspection.Exists);
        Assert.Equal(fileBytes.Length, inspection.SizeBytes);
        Assert.False(string.IsNullOrEmpty(inspection.HashSha256));
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsTrue_OnSuccessOr404()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = "http://localhost:9000",
            ["Storage:Bucket"] = "bizflow-evidence",
            ["Storage:AccessKey"] = "key",
            ["Storage:SecretKey"] = "secret"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var provider = new S3CompatibleStorageProvider(new HttpClient(handler), config);

        var deleted = await provider.DeleteFileAsync("tenant/file.txt", CancellationToken.None);
        Assert.True(deleted);
    }
}
