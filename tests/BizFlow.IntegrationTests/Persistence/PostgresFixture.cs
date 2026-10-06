using BizFlow.Application.Security;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BizFlow.IntegrationTests.Persistence;

[CollectionDefinition("Postgres", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18.3-alpine")
        .WithDatabase("bizflow_tests").WithUsername("bizflow_tests")
        .WithPassword(Guid.NewGuid().ToString("N")).Build();
    public Guid PlatformUserId { get; } = Guid.CreateVersion7();
    public string ConnectionString => database.GetConnectionString();

    public BizFlowDbContext Create(Guid? userId, Guid? tenantId) => new(
        new DbContextOptionsBuilder<BizFlowDbContext>().UseBizFlowPostgres(database.GetConnectionString()).Options,
        new TestContext(userId, tenantId));

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using var db = Create(null, null);
        await db.Database.MigrateAsync();
        // Test-only bootstrap inside a disposable database. Never runs against application data.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode",
                "Email", "NormalizedEmail", "FullName", "PasswordHash", "SecurityStamp", "Status", "AccessFailedCount",
                "MustChangePassword", "CreatedAt", "UpdatedAt")
            VALUES ({PlatformUserId}, NULL, TRUE, 'PLATFORM', 'PLATFORM', 'platform@example.test', 'PLATFORM@EXAMPLE.TEST',
                'Platform test account', 'not-a-real-password-hash', 'test-stamp', 'ACTIVE', 0, FALSE, now(), now())
            """);
    }

    public async Task<(Guid TenantId, Guid UserId)> SeedTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var platform = Create(PlatformUserId, null);
        var company = Company.Register(suffix, "Test Company", "admin@example.test", DateTimeOffset.UtcNow);
        platform.Companies.Add(company);
        await platform.SaveChangesAsync();
        var tenant = Tenant.Create(company.Id, "test-" + suffix, company.Name, "Asia/Ho_Chi_Minh", DateTimeOffset.UtcNow);
        platform.Tenants.Add(tenant);
        await platform.SaveChangesAsync();

        var user = UserAccount.CreateTenantUser(tenant.Id, "EMP001", "employee@example.test", "Test Employee",
            "not-a-real-password-hash", DateTimeOffset.UtcNow);
        await using var tenantDb = Create(user.Id, tenant.Id);
        tenantDb.Users.Add(user);
        await tenantDb.SaveChangesAsync();
        return (tenant.Id, user.Id);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();
    private sealed record TestContext(Guid? UserId, Guid? TenantId) : ITenantContext;
}
