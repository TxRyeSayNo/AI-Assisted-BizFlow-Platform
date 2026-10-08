using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;

namespace BizFlow.Application.Requests;

public sealed record CreateRequestCommand(
    Guid ServiceId,
    Guid CategoryId,
    string Title,
    string Description,
    string? Priority = null,
    Guid? ParentRequestId = null,
    bool SubmitImmediately = false);

public sealed record RequestCreatedView(
    Guid RequestId,
    string Title,
    string Status,
    string Priority,
    Guid ServiceId,
    Guid CategoryId,
    DateTimeOffset CreatedAt);

public sealed record StoredRequestCreation(string Fingerprint, RequestCreatedView Result);

public interface IRequestCreationStore
{
    Task<IRequestCreationTransaction> BeginAsync(Guid tenantId, Guid actorId, string? keyHash, CancellationToken cancellationToken);
}

public interface IRequestCreationTransaction : IAsyncDisposable
{
    Task<StoredRequestCreation?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(WorkRequest request, AuditLog audit, CancellationToken cancellationToken);
}

public static class RequestCreationRules
{
    public static DateTimeOffset DatabaseTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string? KeyHash(string? key)
    {
        if (key is null) return null;
        if (key.Length is < 1 or > 128 || key.Any(c => c is < '!' or > '~'))
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Use one opaque key of 1 to 128 visible ASCII characters.");
        return Hash(key);
    }
}

public sealed class RequestCreation(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IRequestCreationStore store,
    TimeProvider clock)
{
    private Task AuthorizeCreateAsync(CancellationToken cancellationToken) =>
        authorizer.AuthorizeAsync("requests.create", new(context.TenantId), cancellationToken: cancellationToken);

    private Task AuthorizeSubmitAsync(CancellationToken cancellationToken) =>
        authorizer.AuthorizeAsync("requests.submit", new(context.TenantId, context.UserId), cancellationToken: cancellationToken);

    public async Task<RequestCreatedView> CreateAsync(CreateRequestCommand input, string? key, CancellationToken cancellationToken)
    {
        await AuthorizeCreateAsync(cancellationToken);
        if (input.SubmitImmediately)
        {
            await AuthorizeSubmitAsync(cancellationToken);
        }

        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Request creation requires a tenant workspace.");

        var keyHash = RequestCreationRules.KeyHash(key);
        var priorityCode = input.Priority?.Trim() ?? "MEDIUM";
        if (!Enum.GetValues<RequestPriority>().Any(p => RequestListCodes.Priority(p) == priorityCode))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use a canonical request priority.");
        var priority = Enum.GetValues<RequestPriority>().Single(p => RequestListCodes.Priority(p) == priorityCode);

        WorkRequest prototype;
        try
        {
            prototype = WorkRequest.CreateDraft(tenantId, actorId, input.ServiceId, input.CategoryId,
                input.Title, input.Description, clock.GetUtcNow(), priority, input.ParentRequestId);
        }
        catch (ArgumentException ex)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", ex.Message);
        }

        var fingerprint = RequestCreationRules.Hash(JsonSerializer.Serialize(new
        {
            version = 1,
            serviceId = prototype.ServiceId,
            categoryId = prototype.CategoryId,
            title = prototype.Title,
            description = prototype.Description,
            priority = priorityCode,
            parentRequestId = prototype.ParentRequestId,
            submitImmediately = input.SubmitImmediately
        }));

        await using var transaction = await store.BeginAsync(tenantId, actorId, keyHash, cancellationToken);
        await AuthorizeCreateAsync(cancellationToken);
        if (input.SubmitImmediately)
        {
            await AuthorizeSubmitAsync(cancellationToken);
        }

        if (keyHash is not null && await transaction.FindReplayAsync(cancellationToken) is { } replay)
        {
            if (replay.Fingerprint != fingerprint)
                throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used with different request input.");
            return replay.Result;
        }

        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        WorkRequest request;
        if (input.SubmitImmediately)
        {
            request = WorkRequest.CreateSubmitted(tenantId, actorId, prototype.ServiceId, prototype.CategoryId,
                prototype.Title, prototype.Description, now, priority, prototype.ParentRequestId);
        }
        else
        {
            request = WorkRequest.CreateDraft(tenantId, actorId, prototype.ServiceId, prototype.CategoryId,
                prototype.Title, prototype.Description, now, priority, prototype.ParentRequestId);
        }

        var audit = AuditLog.RequestCreated(request, now, keyHash, keyHash is null ? null : fingerprint);
        await transaction.CommitAsync(request, audit, cancellationToken);

        return new(request.Id, request.Title, RequestListCodes.State(request.Status),
            RequestListCodes.Priority(request.Priority), request.ServiceId, request.CategoryId, request.CreatedAt);
    }
}
