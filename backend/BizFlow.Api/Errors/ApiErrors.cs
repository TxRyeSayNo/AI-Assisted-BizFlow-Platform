using System.Diagnostics;
using BizFlow.Application.Common;

namespace BizFlow.Api.Errors;

public sealed record ApiError(string Code, string Message, object? Details, string TraceId, DateTimeOffset Timestamp);

public static class ApiErrors
{
    public static ApiError Create(HttpContext context, string code, string message, object? details = null) =>
        new(code, message, details, Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier, DateTimeOffset.UtcNow);

    public static int Status(FaultKind kind) => kind switch
    {
        FaultKind.Validation => StatusCodes.Status422UnprocessableEntity,
        FaultKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        FaultKind.Forbidden => StatusCodes.Status403Forbidden,
        FaultKind.NotFound => StatusCodes.Status404NotFound,
        FaultKind.Conflict => StatusCodes.Status409Conflict,
        FaultKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        FaultKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };

    public static ApiError ForStatus(HttpContext context) => context.Response.StatusCode switch
    {
        400 => Create(context, "REQUEST.INVALID", "The request is invalid."),
        401 => Create(context, "AUTH.REQUIRED", "Authentication is required."),
        403 => Create(context, "ACCESS.DENIED", "You do not have access to this operation."),
        404 => Create(context, "RESOURCE.NOT_FOUND", "The requested resource was not found."),
        405 => Create(context, "REQUEST.METHOD_NOT_ALLOWED", "The HTTP method is not supported."),
        429 => Create(context, "RATE_LIMIT.EXCEEDED", "Too many requests. Please try again later."),
        _ => Create(context, "REQUEST.FAILED", "The request could not be completed.")
    };
}
