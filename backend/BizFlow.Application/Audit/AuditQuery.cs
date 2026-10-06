using System.Globalization;
using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Audit;

public sealed record AuditFilter(int Page = 1, int PageSize = 25, Guid? ActorId = null, string? ActorType = null,
    string? Action = null, string? ObjectType = null, Guid? ObjectId = null, string? From = null, string? Until = null);
public sealed record AuditCriteria(int Page, int PageSize, Guid? ActorId, string? ActorType, string? Action,
    string? ObjectType, Guid? ObjectId, DateTimeOffset? From, DateTimeOffset? Until);
public sealed record AuditRow(Guid AuditLogId, string ActorType, Guid? ActorId, string Action, string? ObjectType,
    Guid? ObjectId, JsonElement? Before, JsonElement? After, JsonElement? Metadata, DateTimeOffset CreatedAt);
public sealed record AuditPage(IReadOnlyList<AuditRow> Items, int Page, int PageSize, long Total);
public interface IAuditReader
{
    Task<AuditPage> ListAsync(Guid? tenantId, AuditCriteria criteria, CancellationToken cancellationToken);
}

public sealed class AuditQuery(ITenantContext context, IResourceAuthorizer authorizer, IAuditReader reader)
{
    public const string TenantReadPermission = "audit.read";

    public async Task<AuditPage> ListAsync(AuditFilter filter, CancellationToken cancellationToken)
    {
        var permission = context.TenantId is null ? PlatformPermissions.ReadAudit : TenantReadPermission;
        await authorizer.AuthorizeAsync(permission, new(context.TenantId), cancellationToken: cancellationToken);
        var actorType = Trim(filter.ActorType); var action = Trim(filter.Action); var objectType = Trim(filter.ObjectType);
        var from = Timestamp(filter.From); var until = Timestamp(filter.Until);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 100 || (long)(filter.Page - 1) * filter.PageSize > int.MaxValue ||
            filter.ActorId == Guid.Empty || filter.ObjectId == Guid.Empty || action?.Length > 100 || objectType?.Length > 60 ||
            actorType is not (null or "USER" or "SYSTEM" or "AI_AGENT") || (from is not null && until is not null && from >= until)) throw Invalid();
        return await reader.ListAsync(context.TenantId, new(filter.Page, filter.PageSize, filter.ActorId, actorType,
            action, objectType, filter.ObjectId, from, until), cancellationToken);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset? Timestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // An explicit offset avoids machine-local interpretations of audit boundaries.
        var input = value.Trim();
        if (input.Length > 40 || !System.Text.RegularExpressions.Regex.IsMatch(input,
                @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw Invalid();
        return parsed.ToUniversalTime();
    }

    private static ApplicationFault Invalid() => new(FaultKind.Validation, "VALIDATION.FAILED",
        "Use valid audit filters, a page size from 1 to 100 and ISO timestamps with offsets; from must precede until.");
}
