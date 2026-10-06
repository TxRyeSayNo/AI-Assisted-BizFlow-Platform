using BizFlow.Application.Common;

namespace BizFlow.UnitTests.Common;

public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Rejects_invalid_or_overflowing_pagination(int page, int size)
    {
        Assert.Throws<ApplicationFault>(() => new PageRequest(page, size));
    }

    [Fact]
    public void Computes_bounded_pagination()
    {
        var page = new PageRequest(3, 25);
        Assert.Equal(50, page.Offset);
        Assert.Equal(25, page.PageSize);
    }
}
