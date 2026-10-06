using BizFlow.Domain.Notifications;

namespace BizFlow.UnitTests.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public void First_read_is_UTC_and_repeated_reads_preserve_the_original_receipt()
    {
        var notification = Create(); var first = new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.FromHours(7));
        Assert.True(notification.MarkRead(notification.TenantId, notification.RecipientId, first.AddTicks(7)));
        Assert.False(notification.MarkRead(notification.TenantId, notification.RecipientId, first.AddHours(1)));
        Assert.Equal(first.ToUniversalTime(), notification.ReadAt); Assert.Equal(TimeSpan.Zero, notification.ReadAt!.Value.Offset);
        Assert.Null(notification.SentAt);
    }
    [Fact]
    public void Recipient_and_tenant_ownership_are_both_required()
    {
        var notification = Create();
        Assert.Throws<InvalidOperationException>(() => notification.MarkRead(Guid.NewGuid(), notification.RecipientId, DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(() => notification.MarkRead(notification.TenantId, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Null(notification.ReadAt);
    }
    [Fact]
    public void Event_catalog_and_identifiers_fail_closed()
    {
        Assert.Equal(16, Enum.GetValues<NotificationEvent>().Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => Notification.Create(Guid.NewGuid(), Guid.NewGuid(), (NotificationEvent)999, "Title", "Content", "event"));
        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.Empty, Guid.NewGuid(), NotificationEvent.Overdue, "Title", "Content", "event"));
        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.NewGuid(), Guid.Empty, NotificationEvent.Overdue, "Title", "Content", "event"));
        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationEvent.Overdue, "Title", "Content", "event", objectId: Guid.NewGuid()));
    }
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Required_text_cannot_be_empty(string value)
    {
        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationEvent.TaskAssigned, value, "Content", "event"));
        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationEvent.TaskAssigned, "Title", value, "event"));
        Assert.Throws<ArgumentException>(() => Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationEvent.TaskAssigned, "Title", "Content", value));
    }
    private static Notification Create() => Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationEvent.TaskAssigned, "Assigned", "Plain text content", "event/recipient");
}
