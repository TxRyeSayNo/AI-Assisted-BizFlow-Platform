using BizFlow.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Application.Security;

namespace BizFlow.Infrastructure.Notifications;

public sealed class EmailNotificationDispatcher(
    ILogger<EmailNotificationDispatcher> logger,
    DbContextOptions<BizFlowDbContext> options,
    IConfiguration configuration) : IEmailNotificationDispatcher
{
    public async Task<bool> SendDeadlineEmailAsync(
        Guid tenantId,
        Guid userId,
        string taskTitle,
        DateTimeOffset? deadline,
        string triggerType,
        CancellationToken cancellationToken)
    {
        return await SendAsync(tenantId, userId, "Nhắc hạn công việc",
            $"Công việc: {taskTitle}\nMốc nhắc: {triggerType}\nHạn xử lý (UTC): {deadline:dd/MM/yyyy HH:mm}",
            "/notifications", null, cancellationToken);
    }

    public async Task<bool> SendAsync(Guid tenantId, Guid userId, string title, string content,
        string targetPath, Guid? notificationId, CancellationToken cancellationToken)
    {
        var settings = NotificationEmailConfiguration.Resolve(configuration);
        if (settings is not { Enabled: true, IsValid: true }) return false;
        // The background sweep has no HTTP tenant. Pin a fresh read context to this delivery's recipient.
        await using var db = new BizFlowDbContext(options, new RecipientContext(tenantId, userId));
        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
            db.Users.Where(u => u.TenantId == tenantId && u.Id == userId && u.Status == BizFlow.Domain.Organization.UserStatus.Active && u.DeletedAt == null), cancellationToken);
        if (user is null) return false;
        return await SendToAddressAsync(user.Email,title,content,targetPath,notificationId,cancellationToken);
    }

    internal async Task<bool> SendToAddressAsync(string address,string title,string content,string targetPath,Guid? notificationId,CancellationToken cancellationToken)
    {
        var settings=NotificationEmailConfiguration.Resolve(configuration);
        if (!settings.Enabled || !settings.IsValid) return false;
        try
        {
            using var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress(settings.FromName, settings.FromAddress));
            message.To.Add(MimeKit.MailboxAddress.Parse(address));
            message.Subject = "BizFlow: " + title.Replace('\r', ' ').Replace('\n', ' ');
            if (notificationId.HasValue) message.MessageId = $"notification-{notificationId.Value:N}@bizflow.local";
            var target = new Uri(new Uri(settings.FrontendOrigin), targetPath);
            if (target.GetLeftPart(UriPartial.Authority) != new Uri(settings.FrontendOrigin).GetLeftPart(UriPartial.Authority))
                target = new Uri(new Uri(settings.FrontendOrigin), "/notifications");
            message.Body = new MimeKit.TextPart("plain") { Text = content[..Math.Min(content.Length, 12000)] +
                "\n\nMở BizFlow để xem chi tiết:\n" + target.AbsoluteUri };
            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 15000 };
            var security = settings.AllowInsecureLoopback ? MailKit.Security.SecureSocketOptions.None :
                settings.SslOnConnect ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.Username)) await client.AuthenticateAsync(settings.Username, settings.Password!, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            logger.LogWarning("Notification email delivery failed; delivery can be retried.");
            return false;
        }
    }
    private sealed record RecipientContext(Guid Tenant, Guid User) : ITenantContext
    {
        public Guid? TenantId => Tenant;
        public Guid? UserId => User;
    }
}
