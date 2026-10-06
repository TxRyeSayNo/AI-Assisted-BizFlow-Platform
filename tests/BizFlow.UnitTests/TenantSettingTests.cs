using BizFlow.Domain.Tenancy;

namespace BizFlow.UnitTests;

public sealed class TenantSettingTests
{
    [Theory]
    [InlineData("ai.enabled", "true")]
    [InlineData("ai.auto_action_enabled", "false")]
    [InlineData("ai.auto_action_tools", "[]")]
    [InlineData("ai.confidence_threshold", "0.6")]
    [InlineData("ai.monthly_call_limit", "100")]
    [InlineData("sla.pause_on_waiting_for_information", "true")]
    [InlineData("request.generic_service_allowed", "false")]
    [InlineData("attachment.allowed_content_types", "[\"application/pdf\"]")]
    [InlineData("attachment.max_size_bytes", "524288000")]
    [InlineData("report.max_range_days", "366")]
    [InlineData("notification.email_enabled", "true")]
    public void Approved_keys_accept_typed_values(string key, string json)
    {
        var setting = TenantSetting.Create(Guid.NewGuid(), key, json, Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Equal(key, setting.Key); Assert.NotEqual(Guid.Empty, setting.Id);
        Assert.Equal(TimeSpan.Zero, setting.UpdatedAt.Offset);
    }

    [Theory]
    [InlineData("arbitrary.policy", "true")]
    [InlineData("AI.ENABLED", "true")]
    [InlineData("ai.enabled", "\"true\"")]
    [InlineData("ai.enabled", "null")]
    [InlineData("ai.auto_action_tools", "[1]")]
    [InlineData("ai.auto_action_tools", "[\" \" ]")]
    [InlineData("attachment.allowed_content_types", "{}")]
    [InlineData("attachment.max_size_bytes", "524288001")]
    [InlineData("attachment.max_size_bytes", "-1")]
    [InlineData("attachment.max_size_bytes", "1.5")]
    [InlineData("ai.confidence_threshold", "1.01")]
    [InlineData("ai.confidence_threshold", "-0.01")]
    [InlineData("ai.monthly_call_limit", "-1")]
    [InlineData("report.max_range_days", "0")]
    [InlineData("report.max_range_days", "1e100")]
    [InlineData("ai.enabled", "{malformed}")]
    public void Unknown_keys_wrong_types_and_invalid_ranges_are_rejected(string key, string json) =>
        Assert.Throws<ArgumentException>(() => TenantSetting.Create(Guid.NewGuid(), key, json, Guid.NewGuid(), DateTimeOffset.UtcNow));

    [Fact]
    public void Update_preserves_identity_and_requires_tenant_actor_and_valid_value_before_mutating()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var first = DateTimeOffset.UtcNow;
        var setting = TenantSetting.Create(tenant, "attachment.max_size_bytes", "524288000", actor, first);
        var id = setting.Id;
        Assert.Throws<InvalidOperationException>(() => setting.SetValue(Guid.NewGuid(), "1", actor, first));
        Assert.Throws<ArgumentException>(() => setting.SetValue(tenant, "1", Guid.Empty, first));
        Assert.Throws<ArgumentException>(() => setting.SetValue(tenant, "524288001", actor, first));
        Assert.Equal("524288000", setting.ValueJson); Assert.Equal(actor, setting.UpdatedBy); Assert.Equal(first, setting.UpdatedAt);
        var editor = Guid.NewGuid(); var changed = first.ToOffset(TimeSpan.FromHours(7)).AddMinutes(1);
        setting.SetValue(tenant, "1024", editor, changed);
        Assert.Equal(id, setting.Id); Assert.Equal(tenant, setting.TenantId); Assert.Equal("attachment.max_size_bytes", setting.Key);
        Assert.Equal("1024", setting.ValueJson); Assert.Equal(editor, setting.UpdatedBy); Assert.Equal(changed.ToUniversalTime(), setting.UpdatedAt);
    }
}
