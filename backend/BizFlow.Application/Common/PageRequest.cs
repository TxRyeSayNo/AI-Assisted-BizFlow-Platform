namespace BizFlow.Application.Common;

public sealed record PageRequest
{
    public int Page { get; }
    public int PageSize { get; }
    public int Offset { get; }

    public PageRequest(int page = 1, int pageSize = 25)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApplicationFault(FaultKind.Validation, "PAGINATION.INVALID",
                "Page must be positive and page size must be between 1 and 100.");
        Page = page;
        PageSize = pageSize;
        Offset = (page - 1) * pageSize;
    }
}

// Query implementations must order by (CreatedAt, Id) before applying Offset/Take.
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
