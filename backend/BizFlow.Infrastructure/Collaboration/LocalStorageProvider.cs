using System.Security.Cryptography;
using BizFlow.Application.Collaboration;
using Microsoft.Extensions.Configuration;

namespace BizFlow.Infrastructure.Collaboration;

public sealed class LocalStorageProvider : IObjectStorageProvider
{
    private readonly string _basePath;

    public LocalStorageProvider(IConfiguration? configuration = null)
    {
        var configured = configuration?["Storage:BasePath"];
        _basePath = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "bizflow-storage");
    }

    public LocalStorageProvider(string basePath)
    {
        _basePath = basePath;
    }

    private string ResolvePhysicalPath(string objectKey)
    {
        var normalizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, normalizedKey));
        var baseFullPath = Path.GetFullPath(_basePath);

        if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path traversal is not permitted.");

        return fullPath;
    }

    public Task<UploadSessionDescriptor> CreateUploadSessionAsync(
        Guid attachmentId,
        string objectKey,
        string contentType,
        long sizeBytes,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        // For local storage, clients stream binary directly to the backend upload endpoint
        var url = $"/api/v1/attachments/upload-session/{attachmentId:D}/binary";
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(new UploadSessionDescriptor(url, expiresAt));
    }

    public async Task SaveBinaryDirectAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken)
    {
        var physicalPath = ResolvePhysicalPath(objectKey);
        var directory = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            physicalPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public async Task<StorageFileInspection> InspectFileAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var physicalPath = ResolvePhysicalPath(objectKey);
        if (!File.Exists(physicalPath))
        {
            return new StorageFileInspection(false, 0, string.Empty);
        }

        var fileInfo = new FileInfo(physicalPath);
        var length = fileInfo.Length;

        using var sha = SHA256.Create();
        await using var fileStream = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hashBytes = await sha.ComputeHashAsync(fileStream, cancellationToken);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new StorageFileInspection(true, length, hashHex);
    }

    public Task<Stream?> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var physicalPath = ResolvePhysicalPath(objectKey);
        if (!File.Exists(physicalPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteFileAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var physicalPath = ResolvePhysicalPath(objectKey);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
