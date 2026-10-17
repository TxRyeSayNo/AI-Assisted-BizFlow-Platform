using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BizFlow.IntegrationTests.Common;

public sealed class BizFlowRedisCacheServiceTests
{
    private sealed record TestItem(string Name, int Value);

    [Fact]
    public async Task Cache_FallsBackToMemoryGracefully_WhenRedisUnconfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var cache = new BizFlowRedisCacheService(config);

        Assert.False(cache.IsRedisAvailable);

        var key = "test:unconfigured:item";
        var item = new TestItem("Item 1", 42);

        await cache.SetAsync(key, item, TimeSpan.FromMinutes(5));

        var retrieved = await cache.GetAsync<TestItem>(key);
        Assert.NotNull(retrieved);
        Assert.Equal("Item 1", retrieved.Name);
        Assert.Equal(42, retrieved.Value);

        await cache.RemoveAsync(key);
        var afterRemove = await cache.GetAsync<TestItem>(key);
        Assert.Null(afterRemove);
    }

    [Fact]
    public async Task Cache_HandlesPrefixRemoval_InFallbackMode()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var cache = new BizFlowRedisCacheService(config);

        var prefix = "tenant-123:catalog:";
        await cache.SetAsync($"{prefix}item1", new TestItem("Catalog 1", 10));
        await cache.SetAsync($"{prefix}item2", new TestItem("Catalog 2", 20));
        await cache.SetAsync("tenant-456:catalog:item3", new TestItem("Other Tenant", 30));

        await cache.RemoveByPrefixAsync(prefix);

        Assert.Null(await cache.GetAsync<TestItem>($"{prefix}item1"));
        Assert.Null(await cache.GetAsync<TestItem>($"{prefix}item2"));

        // Other tenant's cache entry must remain untouched!
        var otherTenantItem = await cache.GetAsync<TestItem>("tenant-456:catalog:item3");
        Assert.NotNull(otherTenantItem);
        Assert.Equal("Other Tenant", otherTenantItem.Name);
    }

    [Fact]
    public async Task Cache_GracefullyDegrades_WhenRedisHostUnreachable()
    {
        var settings = new Dictionary<string, string?>
        {
            // Point to a non-existent port on localhost where no Redis is listening
            ["Redis:ConnectionString"] = "127.0.0.1:59999"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var cache = new BizFlowRedisCacheService(config);

        var key = "test:unreachable:item";
        var item = new TestItem("Resilient Item", 99);

        // Must not throw an unhandled exception when Redis is unreachable
        var exception = await Record.ExceptionAsync(async () =>
        {
            await cache.SetAsync(key, item, TimeSpan.FromMinutes(2));
            var read = await cache.GetAsync<TestItem>(key);
            Assert.NotNull(read);
            Assert.Equal(99, read.Value);
        });

        Assert.Null(exception);
    }
}
