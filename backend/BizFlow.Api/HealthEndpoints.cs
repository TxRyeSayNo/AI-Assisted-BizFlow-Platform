using BizFlow.Application.Common;
using BizFlow.Api.Errors;

namespace BizFlow.Api;

internal static class HealthEndpoints
{
    public static async Task<IResult> ReadyAsync(HttpContext context, IReadinessProbe probe)
    {
        context.Response.Headers.CacheControl = "no-store";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        var ready = false;
        try { ready = await probe.IsReadyAsync(deadline.Token); }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { }
        // Deliberately no host, database name, migration IDs, exception or connection details.
        return ready ? Results.Ok(new { status = "ready" }) : Results.Json(
            ApiErrors.Create(context, "SERVICE.NOT_READY", "The service is not ready. Please try again later."), statusCode: 503);
    }
}
