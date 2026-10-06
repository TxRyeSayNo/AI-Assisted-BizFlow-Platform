using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BizFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class ReadinessTests(PostgresFixture database)
{
    [Fact]
    public async Task Current_database_is_ready_without_identity_or_business_data_access()
    {
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await AssertReadinessAsync(client, HttpStatusCode.OK, "ready");
        // Caller-supplied scope cannot affect or expand the operational probe.
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        await AssertReadinessAsync(client, HttpStatusCode.OK, "ready");
    }

    [Fact]
    public async Task Missing_and_unknown_migration_history_fail_closed_without_applying_migrations()
    {
        // Isolated schema inside the disposable test container; never modify shared history.
        const string schema = "readiness_probe_fixture";
        await using var db = database.Create(null, null);
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA readiness_probe_fixture");
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString) { SearchPath = schema };
        await using var factory = new AuthenticationFactory(database, new Dictionary<string, string?>
        { ["ConnectionStrings:BizFlow"] = connection.ConnectionString });
        using var client = factory.CreateClient();
        await AssertReadinessAsync(client, HttpStatusCode.ServiceUnavailable, "not_ready");
        var tableCount = await db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema={schema}").SingleAsync();
        Assert.Equal(0, tableCount); // Probe must not create even the migration history table.

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE readiness_probe_fixture."__EFMigrationsHistory" AS TABLE public."__EFMigrationsHistory";
            INSERT INTO readiness_probe_fixture."__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('29990101000000_UnknownDeployment', '10.0.12');
            """);
        await AssertReadinessAsync(client, HttpStatusCode.ServiceUnavailable, "not_ready");
        // Remove only the synthetic history entry in this isolated schema and prove recovery.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM readiness_probe_fixture.\"__EFMigrationsHistory\" WHERE \"MigrationId\"='29990101000000_UnknownDeployment'");
        await AssertReadinessAsync(client, HttpStatusCode.OK, "ready");
    }

    [Fact]
    public async Task Nonresponsive_database_is_bounded_and_does_not_break_liveness_or_leak_details()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); // Accept TCP handshakes but never answer the PostgreSQL handshake.
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        { Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port, Timeout = 30, Pooling = false };
        await using var factory = new AuthenticationFactory(database, new Dictionary<string, string?>
        { ["ConnectionStrings:BizFlow"] = connection.ConnectionString });
        using var client = factory.CreateClient();
        var elapsed = Stopwatch.StartNew();
        await AssertReadinessAsync(client, HttpStatusCode.ServiceUnavailable, "not_ready");
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), "Readiness exceeded its bounded database deadline.");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Readiness_deadline_cancels_the_probe_and_returns_a_sanitized_failure()
    {
        var probe = new WaitingProbe();
        await using var factory = new AuthenticationFactory(database, configureServices: services => services.AddScoped<IReadinessProbe>(_ => probe));
        using var client = factory.CreateClient();
        await AssertReadinessAsync(client, HttpStatusCode.ServiceUnavailable, "not_ready");
        Assert.True(probe.Cancelled);
    }

    private static async Task AssertReadinessAsync(HttpClient client, HttpStatusCode expected, string status)
    {
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (expected == HttpStatusCode.OK)
        {
            Assert.Single(json.RootElement.EnumerateObject());
            Assert.Equal(status, json.RootElement.GetProperty("status").GetString());
        }
        else
        {
            Assert.Equal(5, json.RootElement.EnumerateObject().Count());
            Assert.Equal("SERVICE.NOT_READY", json.RootElement.GetProperty("code").GetString());
            Assert.Equal("The service is not ready. Please try again later.", json.RootElement.GetProperty("message").GetString());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("details").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
            Assert.Equal(TimeSpan.Zero, json.RootElement.GetProperty("timestamp").GetDateTimeOffset().Offset);
        }
    }
    private sealed class WaitingProbe : IReadinessProbe
    {
        public bool Cancelled { get; private set; }
        public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return true; }
            finally { Cancelled = cancellationToken.IsCancellationRequested; }
        }
    }
}
