using System.Buffers.Binary;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using BizFlow.Application.Authentication;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/auth/profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PersonalProfileController(PersonalProfileService profile, IObjectStorageProvider storage,
    ILogger<PersonalProfileController> logger) : ControllerBase
{
    [HttpGet]
    public Task<PersonalProfile> Get(CancellationToken cancellationToken) => profile.ReadAsync(cancellationToken);

    [HttpPatch]
    public async Task<IActionResult> Update(UpdatePersonalProfileInput input, CancellationToken cancellationToken)
    {
        await profile.UpdateNicknameAsync(input.Nickname, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> ChangePassword(ChangePersonalPasswordInput input, CancellationToken cancellationToken)
    {
        await profile.ChangePasswordAsync(input.CurrentPassword, input.NewPassword, cancellationToken);
        return NoContent();
    }

    [HttpGet("avatar")]
    public async Task<IActionResult> Avatar(CancellationToken cancellationToken)
    {
        var key = await profile.GetAvatarKeyAsync(cancellationToken);
        if (key is null) return NotFound();
        var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "image/png");
    }

    [HttpPut("avatar"), RequestSizeLimit(2_097_152)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 1_100_000 || file.ContentType != "image/png") throw InvalidImage();
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        ValidatePng(buffer.ToArray());
        var user = await profile.ReadAsync(cancellationToken);
        var key = $"avatars/{user.UserId:D}/{Guid.NewGuid():N}.png";
        buffer.Position = 0;
        await storage.SaveBinaryDirectAsync(key, buffer, cancellationToken);
        string? old;
        try { old = await profile.SetAvatarKeyAsync(key, cancellationToken); }
        catch { await DeleteUnusedAsync(key); throw; }
        await DeleteUnusedAsync(old);
        return NoContent();
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        var old = await profile.SetAvatarKeyAsync(null, cancellationToken);
        await DeleteUnusedAsync(old);
        return NoContent();
    }

    private async Task DeleteUnusedAsync(string? key)
    {
        if (key is null) return;
        try { await storage.DeleteFileAsync(key, CancellationToken.None); }
        catch { logger.LogWarning("An unused avatar object could not be removed."); }
    }

    // The browser crops/resizes supported images to a plain PNG. Independently validate its
    // structure, CRCs and bounded decompressed pixels before accepting untrusted upload bytes.
    private static void ValidatePng(byte[] bytes)
    {
        try
        {
            if (bytes.Length < 45 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw InvalidImage();
            using var data = new MemoryStream();
            int offset = 8, width = 0, height = 0, channels = 0;
            bool ended = false;
            while (offset + 12 <= bytes.Length)
            {
                int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
                if (size > bytes.Length - offset - 12) throw InvalidImage();
                var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                var chunk = bytes.AsSpan(offset + 4, size + 4);
                uint crc = 0xffffffff;
                foreach (var b in chunk) { crc ^= b; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u); }
                if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + size, 4))) throw InvalidImage();
                if (offset == 8 && type != "IHDR") throw InvalidImage();
                if (type == "IHDR")
                {
                    if (offset != 8 || size != 13) throw InvalidImage();
                    width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8, 4)));
                    height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 12, 4)));
                    var color = bytes[offset + 17];
                    if (width is < 1 or > 512 || height is < 1 or > 512 || bytes[offset + 16] != 8 || color is not (2 or 6) ||
                        bytes[offset + 18] != 0 || bytes[offset + 19] != 0 || bytes[offset + 20] != 0) throw InvalidImage();
                    channels = color == 6 ? 4 : 3;
                }
                else if (type == "IDAT") data.Write(bytes, offset + 8, size);
                else if (type == "IEND") { if (size != 0) throw InvalidImage(); ended = true; offset += 12; break; }
                offset += size + 12;
            }
            if (!ended || offset != bytes.Length || data.Length == 0) throw InvalidImage();
            data.Position = 0;
            using var zlib = new ZLibStream(data, CompressionMode.Decompress);
            var row = new byte[width * channels + 1];
            for (int i = 0; i < height; i++) { zlib.ReadExactly(row); if (row[0] > 4) throw InvalidImage(); }
            if (zlib.ReadByte() != -1) throw InvalidImage();
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or OverflowException or ArgumentException) { throw InvalidImage(); }
    }

    private static ApplicationFault InvalidImage() => new(FaultKind.Validation, "PROFILE.INVALID_AVATAR", "Ảnh không hợp lệ. Vui lòng chọn lại ảnh PNG, JPEG hoặc WebP.");
}

public sealed record UpdatePersonalProfileInput([Required, StringLength(200)] string Nickname);
public sealed record ChangePersonalPasswordInput([Required, StringLength(1024)] string CurrentPassword,
    [Required, StringLength(1024)] string NewPassword)
{
    public override string ToString() => "[REDACTED PASSWORD CHANGE]";
}
