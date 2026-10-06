using BizFlow.Domain.Common;

namespace BizFlow.Domain.Requests;

// FR-REQ-007/008: append a new resolution after rework; never overwrite prior evidence.
// The Application workflow use case supplies the revision and authorizes the resolver.
public sealed class RequestResolution
{
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ResolverId { get; private set; }
    public string Content { get; private set; } = "";
    public int RevisionNo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private RequestResolution() { }

    public static RequestResolution Create(Guid requestId, Guid resolverId, string content,
        int revisionNo, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        var text = content.Trim();
        if (text.Length == 0 || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("A plain-text resolution is required.", nameof(content));
        if (revisionNo < 1) throw new ArgumentOutOfRangeException(nameof(revisionNo), "Revision must be positive.");
        return new()
        {
            Id = Guid.CreateVersion7(), RequestId = EntityRules.Id(requestId, nameof(requestId)),
            ResolverId = EntityRules.Id(resolverId, nameof(resolverId)), Content = text,
            RevisionNo = revisionNo, CreatedAt = now.ToUniversalTime()
        };
    }
}
