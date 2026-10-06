using System.Net.Mail;

namespace BizFlow.Domain.Common;

internal static class EntityRules
{
    internal static string Text(string value, int maximum, string field)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum)
            throw new ArgumentException($"{field} must contain 1 to {maximum} characters.", field);
        return normalized;
    }

    internal static Guid Id(Guid value, string field) => value != Guid.Empty
        ? value : throw new ArgumentException("An identifier is required.", field);

    internal static string Email(string value)
    {
        var email = Text(value, 320, nameof(value));
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new ArgumentException("A valid email address is required.", nameof(value));
        return email;
    }
}
