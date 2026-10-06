using BizFlow.Application.Common;
using BizFlow.Application.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskCreationRulesTests
{
    [Theory]
    [InlineData("2026-10-07")]
    [InlineData("2026-10-07T08:00")]
    [InlineData("not a datetime")]
    [InlineData("2026-10-07T08:00:00+20:00")]
    public void Deadline_requires_an_absolute_explicit_offset(string value) =>
        Assert.Throws<ApplicationFault>(() => TaskCreationRules.ParseDeadline(value));

    [Fact]
    public void Deadline_normalizes_equivalent_offsets_and_preserves_omission()
    {
        Assert.Null(TaskCreationRules.ParseDeadline(null));
        Assert.Equal(TaskCreationRules.ParseDeadline("2026-10-07T01:00:00Z"),
            TaskCreationRules.ParseDeadline("2026-10-07T08:00:00+07:00"));
        Assert.Equal(TimeSpan.Zero, TaskCreationRules.ParseDeadline("2026-10-07T08:00+07:00")!.Value.Offset);
    }

    [Fact]
    public void Key_hashes_are_case_sensitive_and_do_not_retain_the_key()
    {
        Assert.Null(TaskCreationRules.KeyHash(null));
        Assert.NotEqual(TaskCreationRules.KeyHash("Request-A"), TaskCreationRules.KeyHash("request-a"));
        Assert.Equal(64, TaskCreationRules.KeyHash("Request-A")!.Length);
        Assert.Throws<ApplicationFault>(() => TaskCreationRules.KeyHash(""));
        Assert.Throws<ApplicationFault>(() => TaskCreationRules.KeyHash("contains space"));
        Assert.Throws<ApplicationFault>(() => TaskCreationRules.KeyHash(new string('a', 129)));
    }
}
