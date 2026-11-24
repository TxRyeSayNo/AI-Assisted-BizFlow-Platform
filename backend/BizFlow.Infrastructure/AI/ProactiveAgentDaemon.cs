using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.AI;

public sealed class ProactiveAgentDaemon : BackgroundService
{
    private sealed record DaemonContext(Guid? TenantId) : BizFlow.Application.Security.ITenantContext
    {
        public Guid? UserId => new Guid("00000000-0000-0000-0000-000000000001");
    }
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProactiveAgentDaemon> _logger;
    private readonly TimeSpan _interval;

    public ProactiveAgentDaemon(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ProactiveAgentDaemon> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;

        var intervalMinutes = int.TryParse(_configuration["AI:ProactiveDaemonIntervalMinutes"], out var mins) && mins > 0
            ? mins
            : 5;
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ProactiveAgentDaemon] Service started. Scan interval: {IntervalMinutes} minutes.", _interval.TotalMinutes);

        // Initial delay to let the app fully initialize
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAutonomousScanCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "[ProactiveAgentDaemon] Error during autonomous proactive scan cycle.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("[ProactiveAgentDaemon] Service stopping.");
    }

    public async Task RunAutonomousScanCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BizFlowDbContext>();

        // Query active tenants (bypass tenant query filters since daemon runs at system level)
        var tenants = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Status == Domain.Tenancy.TenantStatus.Active && dbContext.Companies.IgnoreQueryFilters().Any(c => c.Id == t.CompanyId && c.Status == Domain.Tenancy.CompanyStatus.Active))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("[ProactiveAgentDaemon] Running proactive autonomous scan across {TenantCount} active tenants.", tenants.Count);

        foreach (var tenantId in tenants)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await RunTenantScanAsync(tenantId, cancellationToken);
                if (result.RecommendationsCreated > 0)
                {
                    _logger.LogInformation("[ProactiveAgentDaemon] Tenant {TenantId}: Generated {Count} proactive recommendations.", tenantId, result.RecommendationsCreated);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[ProactiveAgentDaemon] Failed scanning tenant {TenantId}.", tenantId);
            }
        }
    }

    public async Task<ProactiveScanResult> RunTenantScanAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var db = new BizFlowDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<BizFlowDbContext>>(), new DaemonContext(tenantId));
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId && t.Status == Domain.Tenancy.TenantStatus.Active &&
            db.Companies.Any(c => c.Id == t.CompanyId && c.Status == Domain.Tenancy.CompanyStatus.Active), ct))
            return new(0, 0, 0, DateTimeOffset.UtcNow, "Doanh nghiệp không hoạt động.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // The background scope has no HTTP claims. Pin both scanner and store to this tenant,
        // and serialize instances so a multi-replica deployment does not duplicate recommendations.
        var key = $"proactive-agent:{tenantId:N}";
        var acquired = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({key},0)) AS \"Value\"").SingleAsync(ct);
        if (!acquired) return new(0, 0, 0, DateTimeOffset.UtcNow, "Đã có một lượt quét đang chạy.");
        var scanner = new ProactiveAgentScanner(db, new AiStore(db), scope.ServiceProvider.GetRequiredService<TimeProvider>(), scope.ServiceProvider.GetRequiredService<ILogger<ProactiveAgentScanner>>());
        var result = await scanner.RunProactiveScanAsync(tenantId, ct);
        await tx.CommitAsync(ct);
        return result;
    }
}

