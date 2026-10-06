using BizFlow.Domain.Sla;

namespace BizFlow.UnitTests;

public sealed class SlaProfileTests
{
    [Fact]
    public void Named_profile_starts_as_draft_without_invented_version_or_clock_fields()
    {
        var tenant = Guid.NewGuid(); var profile = SlaProfile.CreateDraft(tenant, "  Standard response  ");
        Assert.Equal(tenant, profile.TenantId); Assert.Equal("Standard response", profile.Name);
        Assert.Equal(SlaProfileStatus.Draft, profile.Status); Assert.NotEqual(Guid.Empty, profile.Id);
    }
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_name_is_rejected(string name) => Assert.Throws<ArgumentException>(() => SlaProfile.CreateDraft(Guid.NewGuid(), name));
    [Fact]
    public void Tenant_and_dictionary_name_length_are_required()
    {
        Assert.Throws<ArgumentException>(() => SlaProfile.CreateDraft(Guid.Empty, "Response"));
        Assert.Throws<ArgumentException>(() => SlaProfile.CreateDraft(Guid.NewGuid(), new string('x', 201)));
    }
}
