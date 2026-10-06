using System.Net;
using BizFlow.Application.Authentication;
using MailKit.Security;
using MimeKit;

namespace BizFlow.Infrastructure.Authentication;

public sealed class ResetEmailSettings
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;
    public bool SslOnConnect { get; init; }
    public bool AllowInsecureLoopback { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = "";
    public string FrontendOrigin { get; init; } = "";
    public bool IsValid => !Enabled || (!string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 &&
        MailboxAddress.TryParse(FromAddress, out _) && Uri.TryCreate(FrontendOrigin, UriKind.Absolute, out var origin) &&
        string.IsNullOrEmpty(origin.UserInfo) && string.IsNullOrEmpty(origin.Query) && string.IsNullOrEmpty(origin.Fragment) &&
        origin.AbsolutePath == "/" && (origin.Scheme == "https" || origin.Scheme == "http" && origin.IsLoopback) &&
        (string.IsNullOrWhiteSpace(Username) || !string.IsNullOrEmpty(Password)) &&
        (!AllowInsecureLoopback || Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(Host, out var address) && IPAddress.IsLoopback(address)));
}

public sealed class PasswordResetEmailSender(ResetEmailSettings settings) : IPasswordResetEmailSender
{
    public async Task SendAsync(string address, string token, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.IsValid) throw new InvalidOperationException("Password reset email is not configured.");
        var link = new Uri(new Uri(settings.FrontendOrigin), "/reset-password").AbsoluteUri + "#token=" + Uri.EscapeDataString(token);
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(address));
        message.Subject = "Reset your BizFlow password";
        message.Body = new TextPart("plain")
        {
            Text = "A password reset was requested for your BizFlow account.\n\nOpen this single-use link to choose a new password:\n" +
                link + "\n\nIf you did not request this, ignore this message. Your password has not changed."
        };
        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
            var security = settings.AllowInsecureLoopback ? SecureSocketOptions.None :
                settings.SslOnConnect ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.Username)) await client.AuthenticateAsync(settings.Username, settings.Password!, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            // SMTP exceptions may contain addresses or provider content. Hangfire stores failures.
            throw new InvalidOperationException("Password reset email delivery failed; retry is permitted.");
        }
    }
}
