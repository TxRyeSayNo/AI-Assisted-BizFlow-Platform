using BizFlow.Infrastructure.Authentication;
using BizFlow.Infrastructure.Persistence;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Authentication;

[Collection("Postgres")]
public sealed class AuthenticationStoreTests(PostgresFixture database)
{
    [Fact]
    public async Task Lookup_and_user_lock_are_translatable_by_PostgreSQL_provider()
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var tenant = await db.Tenants.SingleAsync();
        var store = new AuthenticationStore(new DbContextOptionsBuilder<BizFlowDbContext>()
            .UseBizFlowPostgres(database.ConnectionString).Options, TimeProvider.System);
        var candidate = Assert.Single(await store.FindCandidatesAsync("EMP001", tenant.TenantKey, default));
        Assert.Equal(seed.UserId, candidate.UserId);
        await using var transaction = await store.BeginAsync(candidate, default);
        Assert.NotNull(transaction);
        Assert.Equal(seed.UserId, transaction.User.Id);
        Assert.NotNull(await transaction.ResolveAccessAsync(default));
    }
}
