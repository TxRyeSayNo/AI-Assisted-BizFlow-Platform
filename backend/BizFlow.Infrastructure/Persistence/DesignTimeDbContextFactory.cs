using System.IO;
using BizFlow.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BizFlow.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BizFlowDbContext>
{
    public BizFlowDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__BizFlow");
        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = TryReadConnectionFromEnv() ??
                "Host=localhost;Port=5433;Database=bizflow;Username=bizflow;Password=bizflow_secret_123";
        }
        return new BizFlowDbContext(new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(connection).Options, new DesignTimeContext());
    }

    private static string? TryReadConnectionFromEnv()
    {
        try
        {
            var current = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(current, "deploy", ".env"),
                Path.Combine(current, "..", "deploy", ".env"),
                Path.Combine(current, "..", "..", "deploy", ".env"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "deploy", ".env")
            };
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    foreach (var line in File.ReadAllLines(candidate))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("ConnectionStrings__BizFlow="))
                        {
                            return trimmed["ConnectionStrings__BizFlow=".Length..].Trim();
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private sealed class DesignTimeContext : ITenantContext
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
    }
}
