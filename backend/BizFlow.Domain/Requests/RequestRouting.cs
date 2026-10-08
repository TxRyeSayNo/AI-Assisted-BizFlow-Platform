using BizFlow.Domain.Common;

namespace BizFlow.Domain.Requests;

public enum RequestRoutingSource
{
    Manual,
    Ai,
    Rule
}

public sealed class RequestRouting
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid? FromDepartmentId { get; private set; }
    public Guid? ToDepartmentId { get; private set; }
    public Guid? FromUserId { get; private set; }
    public Guid? ToUserId { get; private set; }
    public Guid RoutedBy { get; private set; }
    public DateTimeOffset RoutedAt { get; private set; }
    public string? Reason { get; private set; }
    public RequestRoutingSource Source { get; private set; }

    private RequestRouting() { }

    public static RequestRouting Create(
        Guid tenantId,
        Guid requestId,
        Guid routedBy,
        DateTimeOffset routedAt,
        Guid? toDepartmentId,
        Guid? toUserId = null,
        Guid? fromDepartmentId = null,
        Guid? fromUserId = null,
        string? reason = null,
        RequestRoutingSource source = RequestRoutingSource.Manual)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (requestId == Guid.Empty) throw new ArgumentException("RequestId is required.", nameof(requestId));
        if (routedBy == Guid.Empty) throw new ArgumentException("RoutedBy is required.", nameof(routedBy));
        if (toDepartmentId is null && toUserId is null)
            throw new ArgumentException("At least one target (Department or User) is required.");
        if (reason?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Reason must be plain text.", nameof(reason));

        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            RequestId = EntityRules.Id(requestId, nameof(requestId)),
            FromDepartmentId = fromDepartmentId is { } fd ? EntityRules.Id(fd, nameof(fromDepartmentId)) : null,
            ToDepartmentId = toDepartmentId is { } td ? EntityRules.Id(td, nameof(toDepartmentId)) : null,
            FromUserId = fromUserId is { } fu ? EntityRules.Id(fu, nameof(fromUserId)) : null,
            ToUserId = toUserId is { } tu ? EntityRules.Id(tu, nameof(toUserId)) : null,
            RoutedBy = EntityRules.Id(routedBy, nameof(routedBy)),
            RoutedAt = routedAt.ToUniversalTime(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Source = source
        };
    }
}
