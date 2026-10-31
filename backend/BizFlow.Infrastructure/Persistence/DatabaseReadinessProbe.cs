using System.Data.Common;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BizFlow.Infrastructure.Persistence;

public sealed class DatabaseReadinessProbe : IReadinessProbe
{
    private readonly DbContextOptions<BizFlowDbContext> options;
    public DatabaseReadinessProbe(string connectionString)
    {
        // Build from operator configuration, not a previously opened connection's redacted
        // connection string. A separate context preserves business connection/pool settings.
        var connection = new NpgsqlConnectionStringBuilder(connectionString)
        { Timeout = 2, CommandTimeout = 2, CancellationTimeout = 1000, Pooling = false };
        options = new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(connection.ConnectionString).Options;
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        await using var db = new BizFlowDbContext(options, new ProbeContext());
        try
        {
            var expected = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
            return expected.Count > 0 && expected.SetEquals(applied);
        }
        catch (DbException)
        {
            return false;
        }
    }
    private sealed class ProbeContext : ITenantContext
    { public Guid? UserId => null; public Guid? TenantId => null; }
}
