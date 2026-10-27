using BizFlow.Api.Controllers;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.UnitTests.Tenancy;

public sealed class TenantCodeAndDocumentControllerTests
{
    private sealed class StubStore : ICompanyProvisioningStore
    {
        public HashSet<string> ExistingCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingTenantKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingCodes.Contains(code));

        public Task<bool> CompanyEmailExistsAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> TenantKeyExistsAsync(string tenantKey, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingTenantKeys.Contains(tenantKey));

        public Task<SubmitCompanyRegistrationResult> SubmitRegistrationAsync(Company company, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ApproveCompanyRegistrationResult> ApproveAsync(Guid companyId, string tenantKey, string adminEmployeeCode, string adminEmail, string adminFullName, string passwordHash, string timeZone, Guid platformAdminId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<RejectCompanyRegistrationResult> RejectAsync(Guid companyId, string reason, Guid platformAdminId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UpdateCompanyStatusResult> UpdateStatusAsync(Guid companyId, CompanyStatus status, Guid platformAdminId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task CheckCode_returns_available_for_new_slug()
    {
        var store = new StubStore();
        var controller = new TenantCodeAndDocumentController(store);

        var response = await controller.CheckCode("acme-logistics", default);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<CheckCodeResponse>(okResult.Value);

        Assert.True(body.Available);
        Assert.Equal("acme-logistics", body.Code);
    }

    [Fact]
    public async Task CheckCode_returns_unavailable_when_code_exists_in_tenants_or_companies()
    {
        var store = new StubStore();
        store.ExistingTenantKeys.Add("acme-existing");
        var controller = new TenantCodeAndDocumentController(store);

        var response = await controller.CheckCode("acme-existing", default);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<CheckCodeResponse>(okResult.Value);

        Assert.False(body.Available);
        Assert.Contains("already in use", body.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("invalid_slug_with_underscores")]
    [InlineData("UPPERCASE-SLUG")]
    public async Task CheckCode_returns_unavailable_for_invalid_slug_format(string code)
    {
        var store = new StubStore();
        var controller = new TenantCodeAndDocumentController(store);

        var response = await controller.CheckCode(code, default);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<CheckCodeResponse>(okResult.Value);

        Assert.False(body.Available);
    }

    [Fact]
    public async Task GenerateCode_returns_valid_unique_slug()
    {
        var store = new StubStore();
        var controller = new TenantCodeAndDocumentController(store);

        var response = await controller.GenerateCode(new GenerateCodeRequest("Acme Global"), default);
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var body = Assert.IsType<GenerateCodeResponse>(okResult.Value);

        Assert.StartsWith("acme-global-", body.Code);
        Assert.Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", body.Code);
    }
}
