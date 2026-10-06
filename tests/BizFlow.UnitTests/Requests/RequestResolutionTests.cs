using BizFlow.Domain.Requests;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestResolutionTests
{
    [Fact]
    public void Resolution_preserves_reference_revision_plain_text_and_utc_without_mutating_prior_evidence()
    {
        var request = Guid.NewGuid(); var resolver = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.FromHours(7));
        var first = RequestResolution.Create(request, resolver, "  <b>Literal</b>\nDetails\tverified  ", 1, now);
        var second = RequestResolution.Create(request, resolver, "Corrected after rework", 2, now.AddHours(1));
        Assert.NotEqual(Guid.Empty, first.Id); Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(request, first.RequestId); Assert.Equal(resolver, first.ResolverId);
        Assert.Equal("<b>Literal</b>\nDetails\tverified", first.Content); Assert.Equal(1, first.RevisionNo);
        Assert.Equal(2, second.RevisionNo); Assert.Equal(TimeSpan.Zero, first.CreatedAt.Offset);
        Assert.Equal(now.ToUniversalTime(), first.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    [InlineData("Invalid\u0001text")]
    [InlineData("Invalid\u007ftext")]
    public void Missing_or_control_character_content_is_rejected(string? content) =>
        Assert.ThrowsAny<ArgumentException>(() => RequestResolution.Create(Guid.NewGuid(), Guid.NewGuid(), content!, 1, DateTimeOffset.UtcNow));

    [Fact]
    public void Empty_references_and_nonpositive_revision_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => RequestResolution.Create(Guid.Empty, Guid.NewGuid(), "Details", 1, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => RequestResolution.Create(Guid.NewGuid(), Guid.Empty, "Details", 1, DateTimeOffset.UtcNow));
        foreach (var revision in new[] { 0, -1, int.MinValue })
            Assert.Throws<ArgumentOutOfRangeException>(() => RequestResolution.Create(Guid.NewGuid(), Guid.NewGuid(), "Details", revision, DateTimeOffset.UtcNow));
    }
}
