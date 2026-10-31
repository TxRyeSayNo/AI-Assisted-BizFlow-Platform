using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Collaboration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class S3CompatibleStorageProvider : IObjectStorageProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _publicEndpoint;
    private readonly string _bucket;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _region;
    private readonly ILogger<S3CompatibleStorageProvider>? _logger;
    private bool _bucketChecked;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_endpoint)
                             && !string.IsNullOrWhiteSpace(_accessKey)
                             && !string.IsNullOrWhiteSpace(_secretKey);

    public S3CompatibleStorageProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<S3CompatibleStorageProvider>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        var rawEndpoint = configuration["Storage:Endpoint"]
            ?? configuration["Storage__Endpoint"]
            ?? configuration["STORAGE_ENDPOINT"]
            ?? configuration["MINIO_ENDPOINT"]
            ?? "http://localhost:9000";
        _endpoint = rawEndpoint.TrimEnd('/');

        var rawPublic = configuration["Storage:PublicEndpoint"]
            ?? configuration["Storage__PublicEndpoint"]
            ?? configuration["STORAGE_PUBLIC_ENDPOINT"]
            ?? _endpoint;
        _publicEndpoint = rawPublic.TrimEnd('/');

        _bucket = configuration["Storage:Bucket"]
            ?? configuration["Storage__Bucket"]
            ?? configuration["STORAGE_BUCKET"]
            ?? "bizflow-evidence";

        _accessKey = configuration["Storage:AccessKey"]
            ?? configuration["Storage__AccessKey"]
            ?? configuration["STORAGE_ACCESS_KEY"]
            ?? configuration["MINIO_ROOT_USER"]
            ?? "bizflow";

        _secretKey = configuration["Storage:SecretKey"]
            ?? configuration["Storage__SecretKey"]
            ?? configuration["STORAGE_SECRET_KEY"]
            ?? configuration["MINIO_ROOT_PASSWORD"]
            ?? string.Empty;

        _region = configuration["Storage:Region"]
            ?? configuration["Storage__Region"]
            ?? configuration["STORAGE_REGION"]
            ?? "us-east-1";
    }

    public async Task<UploadSessionDescriptor> CreateUploadSessionAsync(
        Guid attachmentId,
        string objectKey,
        string contentType,
        long sizeBytes,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        var uri = new Uri(_publicEndpoint);
        var host = uri.Authority;

        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var canonicalUri = $"/{_bucket.Trim('/')}/{EscapePath(objectKey)}";

        var credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
        var escapedCredential = Uri.EscapeDataString($"{_accessKey}/{credentialScope}");

        var queryParams = new[]
        {
            ("X-Amz-Algorithm", "AWS4-HMAC-SHA256"),
            ("X-Amz-Credential", escapedCredential),
            ("X-Amz-Date", amzDate),
            ("X-Amz-Expires", ((int)expiry.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
            ("X-Amz-SignedHeaders", "host")
        };

        Array.Sort(queryParams, (a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        var canonicalQuery = string.Join("&", Array.ConvertAll(queryParams, p => $"{p.Item1}={p.Item2}"));

        var canonicalHeaders = $"host:{host}\n";
        var signedHeaders = "host";
        var payloadHash = "UNSIGNED-PAYLOAD";

        var canonicalRequest = $"PUT\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var hashedCanonicalRequest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();

        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{hashedCanonicalRequest}";
        var signingKey = DeriveSigningKey(_secretKey, dateStamp, _region, "s3");
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        var uploadUrl = $"{_publicEndpoint}{canonicalUri}?{canonicalQuery}&X-Amz-Signature={signature}";
        return new UploadSessionDescriptor(uploadUrl, expiresAt);
    }

    public string? GeneratePresignedDownloadUrl(string objectKey, TimeSpan expiry)
    {
        var uri = new Uri(_publicEndpoint);
        var host = uri.Authority;
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var canonicalUri = $"/{_bucket.Trim('/')}/{EscapePath(objectKey)}";
        var credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
        var escapedCredential = Uri.EscapeDataString($"{_accessKey}/{credentialScope}");

        var queryParams = new[]
        {
            ("X-Amz-Algorithm", "AWS4-HMAC-SHA256"),
            ("X-Amz-Credential", escapedCredential),
            ("X-Amz-Date", amzDate),
            ("X-Amz-Expires", ((int)expiry.TotalSeconds).ToString(CultureInfo.InvariantCulture)),
            ("X-Amz-SignedHeaders", "host")
        };

        Array.Sort(queryParams, (a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        var canonicalQuery = string.Join("&", Array.ConvertAll(queryParams, p => $"{p.Item1}={p.Item2}"));

        var canonicalHeaders = $"host:{host}\n";
        var signedHeaders = "host";
        var payloadHash = "UNSIGNED-PAYLOAD";

        var canonicalRequest = $"GET\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var hashedCanonicalRequest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();

        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{hashedCanonicalRequest}";
        var signingKey = DeriveSigningKey(_secretKey, dateStamp, _region, "s3");
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        return $"{_publicEndpoint}{canonicalUri}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    public async Task SaveBinaryDirectAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var targetUri = BuildDirectUri(objectKey);
        using var request = new HttpRequestMessage(HttpMethod.Put, targetUri);
        request.Content = new StreamContent(content);

        SignRequest(request, HttpMethod.Put, targetUri, "UNSIGNED-PAYLOAD");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("Failed to upload object {Key} to S3/MinIO: {StatusCode} - {Error}", objectKey, response.StatusCode, err);
            throw new InvalidOperationException($"Storage upload failed with HTTP {response.StatusCode}.");
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketChecked || !IsConfigured) return;

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketChecked) return;

            var bucketUri = new Uri($"{_endpoint}/{_bucket.Trim('/')}");
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, bucketUri);
            SignRequest(headRequest, HttpMethod.Head, bucketUri, "UNSIGNED-PAYLOAD");
            var headRes = await _httpClient.SendAsync(headRequest, cancellationToken);
            if (headRes.IsSuccessStatusCode)
            {
                _bucketChecked = true;
                return;
            }

            if (headRes.StatusCode == HttpStatusCode.NotFound)
            {
                using var putRequest = new HttpRequestMessage(HttpMethod.Put, bucketUri);
                SignRequest(putRequest, HttpMethod.Put, bucketUri, "UNSIGNED-PAYLOAD");
                var putRes = await _httpClient.SendAsync(putRequest, cancellationToken);
                if (putRes.IsSuccessStatusCode || putRes.StatusCode == HttpStatusCode.Conflict)
                {
                    _bucketChecked = true;
                    return;
                }
            }
            _bucketChecked = true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure bucket {Bucket} exists in S3/MinIO", _bucket);
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    public async Task<StorageFileInspection> InspectFileAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var targetUri = BuildDirectUri(objectKey);

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, targetUri);
        SignRequest(headRequest, HttpMethod.Head, targetUri, "UNSIGNED-PAYLOAD");

        var response = await _httpClient.SendAsync(headRequest, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new StorageFileInspection(false, 0, string.Empty);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning("InspectFileAsync returned status {StatusCode} for key {Key}", response.StatusCode, objectKey);
            return new StorageFileInspection(false, 0, string.Empty);
        }

        var contentLength = response.Content.Headers.ContentLength ?? 0;

        // Fetch stream to compute exact SHA-256
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, targetUri);
        SignRequest(getRequest, HttpMethod.Get, targetUri, "UNSIGNED-PAYLOAD");

        var getResponse = await _httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!getResponse.IsSuccessStatusCode)
        {
            return new StorageFileInspection(false, 0, string.Empty);
        }

        await using var stream = await getResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(stream, cancellationToken);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new StorageFileInspection(true, contentLength, hashHex);
    }

    public async Task<Stream?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var targetUri = BuildDirectUri(objectKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
        SignRequest(request, HttpMethod.Get, targetUri, "UNSIGNED-PAYLOAD");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("OpenReadAsync failed with status {StatusCode} for object {Key}", response.StatusCode, objectKey);
            return null;
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<bool> DeleteFileAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var targetUri = BuildDirectUri(objectKey);
        using var request = new HttpRequestMessage(HttpMethod.Delete, targetUri);
        SignRequest(request, HttpMethod.Delete, targetUri, "UNSIGNED-PAYLOAD");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    private Uri BuildDirectUri(string objectKey)
    {
        var path = $"/{_bucket.Trim('/')}/{EscapePath(objectKey)}";
        return new Uri($"{_endpoint}{path}");
    }

    private static string EscapePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", Array.ConvertAll(parts, Uri.EscapeDataString));
    }

    private void SignRequest(HttpRequestMessage request, HttpMethod method, Uri uri, string payloadHash)
    {
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var host = uri.Authority;
        request.Headers.Host = host;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var canonicalUri = uri.AbsolutePath;
        var canonicalQuery = string.Empty;
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = $"{method.Method}\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var hashedCanonicalRequest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))).ToLowerInvariant();

        var credentialScope = $"{dateStamp}/{_region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{hashedCanonicalRequest}";

        var signingKey = DeriveSigningKey(_secretKey, dateStamp, _region, "s3");
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        var authHeader = $"AWS4-HMAC-SHA256 Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
    }

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(region));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(service));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }
}
