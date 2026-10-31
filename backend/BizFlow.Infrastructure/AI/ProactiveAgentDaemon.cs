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
        var scanner = scope.ServiceProvider.GetRequiredService<IProactiveAgentScanner>();

        // Query active tenants (bypass tenant query filters since daemon runs at system level)
        var tenants = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Status == Domain.Tenancy.TenantStatus.Active)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("[ProactiveAgentDaemon] Running proactive autonomous scan across {TenantCount} active tenants.", tenants.Count);

        foreach (var tenantId in tenants)
        {
            try
            {
                var result = await scanner.RunProactiveScanAsync(tenantId, cancellationToken);
                if (result.RecommendationsCreated > 0)
                {
                    _logger.LogInformation("[ProactiveAgentDaemon] Tenant {TenantId}: Generated {Count} proactive recommendations.", tenantId, result.RecommendationsCreated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ProactiveAgentDaemon] Failed scanning tenant {TenantId}.", tenantId);
            }
        }
    }
}

