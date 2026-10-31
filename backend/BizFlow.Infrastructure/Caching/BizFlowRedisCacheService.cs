using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Caching;

public sealed class BizFlowRedisCacheService : IBizFlowCacheService
{
    private sealed record CacheEntry(string Json, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> _memoryFallback = new();
    private readonly string _host;
    private readonly int _port;
    private readonly string? _password;
    private readonly bool _enabled;
    private readonly ILogger<BizFlowRedisCacheService>? _logger;
    private volatile bool _redisHealthy = true;
    private DateTimeOffset _lastHealthCheck = DateTimeOffset.MinValue;

    public bool IsRedisAvailable => _enabled && _redisHealthy;

    public BizFlowRedisCacheService(
        IConfiguration configuration,
        ILogger<BizFlowRedisCacheService>? logger = null)
    {
        _logger = logger;

        var connectionString = configuration["Redis:ConnectionString"]
            ?? configuration["Redis__ConnectionString"]
            ?? configuration["REDIS_CONNECTION"]
            ?? configuration["REDIS_HOST"];

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            _enabled = false;
            _host = "localhost";
            _port = 6379;
            _redisHealthy = false;
        }
        else
        {
            _enabled = true;
            ParseRedisConnection(connectionString, out _host, out _port, out _password);
        }
    }

    private static void ParseRedisConnection(string raw, out string host, out int port, out string? password)
    {
        host = "localhost";
        port = 6379;
        password = null;

        if (raw.Contains(';') || raw.Contains('='))
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var k = kv[0].Trim();
                var v = kv[1].Trim();
                if (k.Equals("Host", StringComparison.OrdinalIgnoreCase) || k.Equals("Server", StringComparison.OrdinalIgnoreCase))
                    host = v;
                else if (k.Equals("Port", StringComparison.OrdinalIgnoreCase) && int.TryParse(v, out var p))
                    port = p;
                else if (k.Equals("Password", StringComparison.OrdinalIgnoreCase))
                    password = v;
            }
        }
        else
        {
            var parts = raw.Split(':', 2);
            host = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var p))
                port = p;
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return default;

        if (ShouldAttemptRedis())
        {
            try
            {
                var redisValue = await ExecuteRedisCommandAsync(["GET", key], cancellationToken);
                if (redisValue is not null)
                {
                    return JsonSerializer.Deserialize<T>(redisValue);
                }
            }
            catch (Exception ex)
            {
                MarkRedisUnhealthy(ex);
            }
        }

        // Memory fallback
        if (_memoryFallback.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return JsonSerializer.Deserialize<T>(entry.Json);
            }
            _memoryFallback.TryRemove(key, out _);
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value is null) return;

        var json = JsonSerializer.Serialize(value);
        var ttl = expiration ?? TimeSpan.FromMinutes(10);
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        // Always update memory fallback for seamless hot-cache and fallback
        _memoryFallback[key] = new CacheEntry(json, expiresAt);

        if (ShouldAttemptRedis())
        {
            try
            {
                var seconds = Math.Max(1, (int)ttl.TotalSeconds);
                await ExecuteRedisCommandAsync(["SET", key, json, "EX", seconds.ToString()], cancellationToken);
            }
            catch (Exception ex)
            {
                MarkRedisUnhealthy(ex);
            }
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        _memoryFallback.TryRemove(key, out _);

        if (ShouldAttemptRedis())
        {
            try
            {
                await ExecuteRedisCommandAsync(["DEL", key], cancellationToken);
            }
            catch (Exception ex)
            {
                MarkRedisUnhealthy(ex);
            }
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return;

        // Invalidate memory fallback keys matching prefix
        foreach (var k in _memoryFallback.Keys)
        {
            if (k.StartsWith(prefix, StringComparison.Ordinal))
            {
                _memoryFallback.TryRemove(k, out _);
            }
        }

        if (ShouldAttemptRedis())
        {
            try
            {
                var pattern = $"{prefix}*";
                var keysResponse = await ExecuteRedisCommandAsync(["KEYS", pattern], cancellationToken);
                if (!string.IsNullOrEmpty(keysResponse))
                {
                    var lines = keysResponse.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var k in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(k))
                            await ExecuteRedisCommandAsync(["DEL", k], cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                MarkRedisUnhealthy(ex);
            }
        }
    }

    private bool ShouldAttemptRedis()
    {
        if (!_enabled) return false;
        if (_redisHealthy) return true;

        // Auto-reconnect probe every 30 seconds if unhealthy
        if (DateTimeOffset.UtcNow - _lastHealthCheck > TimeSpan.FromSeconds(30))
        {
            _lastHealthCheck = DateTimeOffset.UtcNow;
            return true;
        }

        return false;
    }

    private void MarkRedisUnhealthy(Exception ex)
    {
        if (_redisHealthy)
        {
            _logger?.LogWarning(ex, "Redis connection at {Host}:{Port} became unavailable. Operating with local memory cache fallback.", _host, _port);
            _redisHealthy = false;
        }
        _lastHealthCheck = DateTimeOffset.UtcNow;
    }

    private async Task<string?> ExecuteRedisCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        await client.ConnectAsync(_host, _port, timeoutCts.Token);
        using var stream = client.GetStream();

        if (!string.IsNullOrEmpty(_password))
        {
            await SendRespArrayAsync(stream, ["AUTH", _password], timeoutCts.Token);
            _ = await ReadRespResponseAsync(stream, timeoutCts.Token);
        }

        await SendRespArrayAsync(stream, args, timeoutCts.Token);
        var result = await ReadRespResponseAsync(stream, timeoutCts.Token);

        _redisHealthy = true;
        return result;
    }

    private static async Task SendRespArrayAsync(NetworkStream stream, string[] args, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append($"*{args.Length}\r\n");
        foreach (var arg in args)
        {
            var bytes = Encoding.UTF8.GetByteCount(arg);
            sb.Append($"${bytes}\r\n{arg}\r\n");
        }
        var encoded = Encoding.UTF8.GetBytes(sb.ToString());
        await stream.WriteAsync(encoded, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<string?> ReadRespResponseAsync(NetworkStream stream, CancellationToken ct)
    {
        var reader = new StreamReader(stream, Encoding.UTF8);
        var line = await reader.ReadLineAsync(ct);
        if (line is null) return null;

        if (line.StartsWith('+') || line.StartsWith(':'))
        {
            return line[1..];
        }

        if (line.StartsWith('-'))
        {
            throw new InvalidOperationException($"Redis error: {line[1..]}");
        }

        if (line.StartsWith('$'))
        {
            if (int.TryParse(line[1..], out var length))
            {
                if (length < 0) return null;
                var buffer = new char[length];
                var read = await reader.ReadBlockAsync(buffer, 0, length);
                // Consume trailing \r\n
                await reader.ReadLineAsync(ct);
                return new string(buffer, 0, read);
            }
        }

        if (line.StartsWith('*'))
        {
            if (int.TryParse(line[1..], out var count))
            {
                if (count <= 0) return null;
                var items = new List<string>();
                for (var i = 0; i < count; i++)
                {
                    var itemLine = await reader.ReadLineAsync(ct);
                    if (itemLine is not null && itemLine.StartsWith('$') && int.TryParse(itemLine[1..], out var itemLen))
                    {
                        if (itemLen < 0) continue;
                        var buf = new char[itemLen];
                        var r = await reader.ReadBlockAsync(buf, 0, itemLen);
                        await reader.ReadLineAsync(ct);
                        items.Add(new string(buf, 0, r));
                    }
                }
                return string.Join("\n", items);
            }
        }

        return null;
    }
}
