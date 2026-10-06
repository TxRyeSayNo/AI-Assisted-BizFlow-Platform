using System.Globalization;
using BizFlow.Application.Common;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Services;

namespace BizFlow.Application.Services;

public interface IServiceMutationStore
{
    Task<IServiceMutationTransaction?> BeginAsync(Guid tenantId, Guid serviceId, CancellationToken cancellationToken);
}
public interface IServiceMutationTransaction : IAsyncDisposable
{
    InternalService Service { get; }
    uint Version { get; }
    IReadOnlyList<ServiceCategory> Categories { get; }
    Task<ServiceRow> CommitAsync(ServiceCategory category, AuditLog audit, CancellationToken cancellationToken);
    Task<ServiceRow> CommitMetadataAsync(AuditLog audit, CancellationToken cancellationToken);
}
public static class ServiceETag
{
    public static string Format(uint version) => "\"" + version.ToString(CultureInfo.InvariantCulture) + "\"";
    public static void RequireCurrent(string? ifMatch, IServiceMutationTransaction transaction)
    {
        if (ifMatch is null || ifMatch.Length < 3 || ifMatch[0] != '"' || ifMatch[^1] != '"' ||
            !uint.TryParse(ifMatch.AsSpan(1, ifMatch.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var expected))
            throw new ApplicationFault(FaultKind.Validation, "SERVICE.ETAG_REQUIRED", "Send the service's current ETag in If-Match.");
        if (expected != transaction.Version)
            throw new ApplicationFault(FaultKind.Conflict, "SERVICE.VERSION_CONFLICT", "This service changed. Reload it before saving.",
                new Dictionary<string, string[]> {
                    ["etag"] = [Format(transaction.Version)], ["code"] = [transaction.Service.Code], ["name"] = [transaction.Service.Name],
                    ["description"] = [transaction.Service.Description ?? ""], ["status"] = [transaction.Service.Status.ToString().ToUpperInvariant()],
                    ["categoryCodes"] = transaction.Categories.Select(c => c.Code).Order(StringComparer.Ordinal).ToArray() });
    }
}
