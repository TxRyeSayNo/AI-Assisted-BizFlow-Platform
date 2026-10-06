using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BizFlow.Infrastructure.Authentication;

public sealed class PasswordResetQueue(Func<IBackgroundJobClient> client, ResetEmailSettings settings) : IPasswordResetQueue
{
    public void Enqueue(string normalizedIdentifier, string? tenantKey, DateTimeOffset requestedAt)
    {
        if (!settings.Enabled) throw new ApplicationFault(FaultKind.Unavailable, "AUTH.RESET_UNAVAILABLE", "Password reset is temporarily unavailable. Contact your administrator.");
        // No password, reset token or email message body is serialized into Hangfire storage.
        try { client().Enqueue<PasswordResetEmailJob>(job => job.DeliverAsync(normalizedIdentifier, tenantKey, requestedAt, CancellationToken.None)); }
        catch (Exception error) when (error is not ApplicationFault)
        {
            throw new ApplicationFault(FaultKind.Unavailable, "AUTH.RESET_UNAVAILABLE", "Password reset is temporarily unavailable. Please try again later.");
        }
    }
}

public sealed class PasswordResetEmailJob(PasswordResetDeliveryService delivery)
{
    [Queue("email")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [15, 60, 300], LogEvents = false)]
    public Task DeliverAsync(string normalizedIdentifier, string? tenantKey, DateTimeOffset requestedAt, CancellationToken cancellationToken) =>
        delivery.DeliverAsync(normalizedIdentifier, tenantKey, requestedAt, cancellationToken);
}

public sealed class PasswordResetJobServer(IServiceProvider services, ResetEmailSettings settings) : IHostedService, IDisposable
{
    private BackgroundJobServer? server;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (settings.Enabled)
        {
            // Accessing the configured client initializes Hangfire's DI job activator first.
            _ = services.GetRequiredService<IBackgroundJobClient>();
            server = new BackgroundJobServer(new BackgroundJobServerOptions { Queues = ["email"], WorkerCount = 2 },
                services.GetRequiredService<JobStorage>());
        }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        server?.SendStop();
        return Task.CompletedTask;
    }
    public void Dispose() => server?.Dispose();
}
