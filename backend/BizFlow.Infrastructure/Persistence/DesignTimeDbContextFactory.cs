using BizFlow.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BizFlow.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BizFlowDbContext>
{
    public BizFlowDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__BizFlow") ??
            "Host=localhost;Database=bizflow;Username=bizflow";
        return new BizFlowDbContext(new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(connection).Options, new DesignTimeContext());
    }

    private sealed class DesignTimeContext : ITenantContext
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
    }
}
