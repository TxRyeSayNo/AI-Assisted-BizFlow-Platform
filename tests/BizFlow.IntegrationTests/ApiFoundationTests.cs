using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BizFlow.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace BizFlow.IntegrationTests;

public sealed class FoundationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "bizflow-tests", ["Jwt:Audience"] = "bizflow-tests",
            ["Jwt:SigningKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["ConnectionStrings:BizFlow"] = "Host=localhost;Database=unused"
        }));
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(FixtureController).Assembly));
    }
}

public sealed class ApiFoundationTests(FoundationFactory factory) : IClassFixture<FoundationFactory>
{
    [Fact]
    public async Task Liveness_is_reachable_without_a_session()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/does-not-exist", 404, "RESOURCE.NOT_FOUND")]
    [InlineData("/_fixture/conflict", 409, "TEST.CONFLICT")]
    [InlineData("/_fixture/failure", 500, "INTERNAL.ERROR")]
    public async Task Errors_use_safe_canonical_envelope(string path, int status, string code)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(status, (int)response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("traceId").GetString()));
        Assert.Equal(TimeSpan.Zero, json.GetProperty("timestamp").GetDateTimeOffset().Offset);
        Assert.True(json.TryGetProperty("details", out _));
        Assert.DoesNotContain("sensitive-provider-content", json.ToString());
    }

    [Fact]
    public async Task Validation_returns_422_without_echoing_submitted_values()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/_fixture/validation", new { count = "sensitive-provider-content" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("VALIDATION.FAILED", content);
        Assert.DoesNotContain("sensitive-provider-content", content);
    }
}

// Test-only application part; never registered by the production executable.
[ApiController]
[Route("_fixture")]
public sealed class FixtureController : ControllerBase
{
    [HttpGet("conflict")]
    public IActionResult ConflictFixture() => throw new ApplicationFault(FaultKind.Conflict, "TEST.CONFLICT", "The resource changed.");

    [HttpGet("failure")]
    public IActionResult Failure() => throw new InvalidOperationException("sensitive-provider-content");

    [HttpPost("validation")]
    public IActionResult Validate(ValidationInput input) => Ok(input);
}

public sealed record ValidationInput([Range(1, 100)] int Count);
