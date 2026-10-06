namespace BizFlow.Application.Common;

public enum FaultKind { Validation, Unauthenticated, Forbidden, NotFound, Conflict, Unavailable, TooManyRequests }

public sealed class ApplicationFault(
    FaultKind kind, string code, string message,
    IReadOnlyDictionary<string, string[]>? details = null) : Exception(message)
{
    public FaultKind Kind { get; } = kind;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string[]>? Details { get; } = details;

    public static ApplicationFault NotFound() =>
        new(FaultKind.NotFound, "RESOURCE.NOT_FOUND", "The requested resource was not found.");
}
