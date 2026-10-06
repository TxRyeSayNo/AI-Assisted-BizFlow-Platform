using BizFlow.Domain.Services;

namespace BizFlow.UnitTests;

public sealed class ServiceCatalogTests
{
    [Fact]
    public void Metadata_updates_validate_atomically_and_preserve_identity_and_configuration()
    {
        var now = DateTimeOffset.UtcNow; var service = InternalService.Create(Guid.NewGuid(), "IT", "Help", "Before", true, now);
        var id = service.Id; var tenant = service.TenantId;
        var workflow = Guid.NewGuid(); var sla = Guid.NewGuid();
        typeof(InternalService).GetProperty(nameof(InternalService.ActiveWorkflowVersionId))!.SetValue(service, workflow);
        typeof(InternalService).GetProperty(nameof(InternalService.ActiveSlaVersionId))!.SetValue(service, sla);
        var before = service.Metadata();
        Assert.Throws<ArgumentException>(() => service.UpdateMetadata("CHANGED", " ", "After", false, now.AddHours(1)));
        Assert.Equal(before, service.Metadata()); Assert.Equal(now, service.UpdatedAt);
        Assert.Throws<ArgumentException>(() => service.UpdateMetadata("CHANGED", "New", "bad\0text", false, now.AddHours(1)));
        Assert.Equal(before, service.Metadata());
        service.UpdateMetadata(" NEW ", " New name ", " <script>literal</script> ", false, now.AddHours(1));
        Assert.Equal(new ServiceMetadata("NEW", "New name", "<script>literal</script>", ServiceStatus.Inactive), service.Metadata());
        Assert.Equal(id, service.Id); Assert.Equal(tenant, service.TenantId); Assert.Equal(now, service.CreatedAt);
        Assert.Equal(now.AddHours(1), service.UpdatedAt); Assert.Equal(workflow, service.ActiveWorkflowVersionId); Assert.Equal(sla, service.ActiveSlaVersionId);
        service.UpdateMetadata("NEW", "New name", " ", true, now.AddHours(2));
        Assert.Null(service.Description); Assert.Equal(ServiceStatus.Active, service.Status);
    }
    [Fact]
    public void Adding_a_category_touches_only_its_own_service_without_changing_metadata_or_bindings()
    {
        var created = DateTimeOffset.UtcNow; var service = InternalService.Create(Guid.NewGuid(), "IT", "Help", "Description", false, created);
        var category = ServiceCategory.Create(service.Id, "NEW", "New category");
        service.RecordCategoryAdded(category, created.AddMinutes(1));
        Assert.Equal(created.AddMinutes(1), service.UpdatedAt); Assert.Equal(created, service.CreatedAt);
        Assert.Equal("IT", service.Code); Assert.Equal("Help", service.Name); Assert.Equal("Description", service.Description);
        Assert.Equal(ServiceStatus.Inactive, service.Status); Assert.Null(service.ActiveWorkflowVersionId); Assert.Null(service.ActiveSlaVersionId);
        Assert.Throws<ArgumentException>(() => service.RecordCategoryAdded(ServiceCategory.Create(Guid.NewGuid(), "FOREIGN", "Foreign"), created.AddMinutes(2)));
        Assert.Equal(created.AddMinutes(1), service.UpdatedAt);
    }
    [Fact]
    public void Service_preserves_dictionary_fields_and_starts_active_without_runtime_bindings()
    {
        var tenant = Guid.NewGuid(); var now = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.FromHours(7));
        var service = InternalService.Create(tenant, "  IT  ", "  IT help  ", "  Plain text  ", true, now);
        Assert.NotEqual(Guid.Empty, service.Id); Assert.Equal(tenant, service.TenantId);
        Assert.Equal("IT", service.Code); Assert.Equal("IT help", service.Name); Assert.Equal("Plain text", service.Description);
        Assert.Equal(ServiceStatus.Active, service.Status); Assert.Null(service.ActiveWorkflowVersionId); Assert.Null(service.ActiveSlaVersionId);
        Assert.Equal(now.ToUniversalTime(), service.CreatedAt); Assert.Equal(service.CreatedAt, service.UpdatedAt);
    }
    [Fact]
    public void Inactive_service_and_category_preserve_requested_flags_and_plain_text()
    {
        var service = InternalService.Create(Guid.NewGuid(), "Case-Sensitive", "Help", "<script>literal text</script>", false, DateTimeOffset.UtcNow);
        var category = ServiceCategory.Create(service.Id, " Desktop ", " Devices ", false);
        Assert.Equal(ServiceStatus.Inactive, service.Status); Assert.Equal("Case-Sensitive", service.Code);
        Assert.Equal("<script>literal text</script>", service.Description); Assert.Equal(service.Id, category.ServiceId);
        Assert.Equal("Desktop", category.Code); Assert.Equal("Devices", category.Name); Assert.Equal(ServiceCategoryStatus.Inactive, category.Status);
    }
    [Theory]
    [InlineData("", "Name")]
    [InlineData(" ", "Name")]
    [InlineData("IT", "")]
    [InlineData("IT", " ")]
    public void Blank_metadata_is_rejected(string code, string name)
    {
        Assert.Throws<ArgumentException>(() => InternalService.Create(Guid.NewGuid(), code, name, null, true, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => ServiceCategory.Create(Guid.NewGuid(), code, name, true));
    }
    [Fact]
    public void Required_ownership_and_dictionary_lengths_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => InternalService.Create(Guid.Empty, "IT", "Help", null, true, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => ServiceCategory.Create(Guid.Empty, "IT", "Help", true));
        foreach (var (code, name) in new[] { (new string('a', 81), "Help"), ("IT", new string('a', 201)) })
        {
            Assert.Throws<ArgumentException>(() => InternalService.Create(Guid.NewGuid(), code, name, null, true, DateTimeOffset.UtcNow));
            Assert.Throws<ArgumentException>(() => ServiceCategory.Create(Guid.NewGuid(), code, name, true));
        }
    }
    [Fact]
    public void Description_is_optional_and_control_characters_cannot_be_stored()
    {
        Assert.Null(InternalService.Create(Guid.NewGuid(), "IT", "Help", "  ", true, DateTimeOffset.UtcNow).Description);
        Assert.Throws<ArgumentException>(() => InternalService.Create(Guid.NewGuid(), "IT", "Help", "bad\0text", true, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => ServiceCategory.Create(Guid.NewGuid(), "bad\u0001code", "Help", true));
        Assert.Equal("line 1\nline 2", InternalService.Create(Guid.NewGuid(), "IT", "Help", "line 1\nline 2", true, DateTimeOffset.UtcNow).Description);
    }
}
