using BizFlow.Api.Errors;
using BizFlow.Api;
using BizFlow.Api.Security;
using BizFlow.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
builder.Services.AddBizFlowAuthentication(builder.Configuration);
builder.Services.AddBizFlowPasswordReset(builder.Configuration);
builder.Services.AddSingleton<BizFlow.Application.Common.IReadinessProbe>(_ => new BizFlow.Infrastructure.Persistence.DatabaseReadinessProbe(
    builder.Configuration.GetConnectionString("BizFlow") ?? throw new InvalidOperationException("ConnectionStrings:BizFlow is required.")));
builder.Services.AddScoped<BizFlow.Application.Tenancy.CompanyRegistrationQuery>();
builder.Services.AddScoped<BizFlow.Application.Tenancy.ICompanyRegistrationReader, BizFlow.Infrastructure.Tenancy.CompanyRegistrationReader>();
builder.Services.AddScoped<TenantMembershipAuthorizer>();
builder.Services.AddScoped<BizFlow.Application.Organization.DepartmentQuery>();
builder.Services.AddScoped<BizFlow.Application.Organization.IDepartmentReader, BizFlow.Infrastructure.Organization.DepartmentReader>();
builder.Services.AddScoped<BizFlow.Application.Organization.RoleManagement>();
builder.Services.AddScoped<BizFlow.Application.Organization.IRoleStore, BizFlow.Infrastructure.Organization.RoleStore>();
builder.Services.AddScoped<BizFlow.Application.Organization.UserDirectoryQuery>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskListQuery>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskCreation>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskAssignmentCommand>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskAcceptanceCommand>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskAcceptanceStore, BizFlow.Infrastructure.Tasks.TaskAcceptanceStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskAssignmentStore, BizFlow.Infrastructure.Tasks.TaskAssignmentStore>();
builder.Services.AddScoped<BizFlow.Domain.Workflows.IWorkflowEngine, BizFlow.Domain.Workflows.TaskWorkflowEngine>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskCreationStore, BizFlow.Infrastructure.Tasks.TaskCreationStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskDetailQuery>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskDetailReader, BizFlow.Infrastructure.Tasks.TaskDetailReader>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskListReader, BizFlow.Infrastructure.Tasks.TaskListReader>();
builder.Services.AddScoped<BizFlow.Application.Organization.IUserDirectoryReader, BizFlow.Infrastructure.Organization.UserDirectoryReader>();
builder.Services.AddScoped<BizFlow.Application.Workflows.WorkflowLibrary>();
builder.Services.AddScoped<BizFlow.Application.Workflows.IWorkflowLibraryStore, BizFlow.Infrastructure.Workflows.WorkflowLibraryStore>();
builder.Services.AddScoped<BizFlow.Application.Audit.AuditQuery>();
builder.Services.AddScoped<BizFlow.Application.Sla.SlaProfileCatalog>();
builder.Services.AddScoped<BizFlow.Application.Sla.ISlaProfileStore, BizFlow.Infrastructure.Sla.SlaProfileStore>();
builder.Services.AddScoped<BizFlow.Application.Sla.SlaVersionCreation>();
builder.Services.AddScoped<BizFlow.Application.Services.ServiceCatalog>();
builder.Services.AddScoped<BizFlow.Application.Services.ServiceCategoryCreation>();
builder.Services.AddScoped<BizFlow.Application.Services.ServiceMetadataUpdate>();
builder.Services.AddScoped<BizFlow.Application.Services.IServiceMutationStore, BizFlow.Infrastructure.Services.ServiceMutationStore>();
builder.Services.AddScoped<BizFlow.Application.Services.IServiceCatalogStore, BizFlow.Infrastructure.Services.ServiceCatalogStore>();
builder.Services.AddScoped<BizFlow.Application.Sla.SlaVersionHistory>();
builder.Services.AddScoped<BizFlow.Application.Sla.ISlaVersionHistoryStore, BizFlow.Infrastructure.Sla.SlaVersionHistoryStore>();
builder.Services.AddScoped<BizFlow.Application.Sla.ISlaVersionStore, BizFlow.Infrastructure.Sla.SlaVersionStore>();
builder.Services.AddScoped<BizFlow.Application.Notifications.NotificationInbox>();
builder.Services.AddScoped<BizFlow.Application.Notifications.INotificationInboxStore, BizFlow.Infrastructure.Notifications.NotificationInboxStore>();
builder.Services.AddScoped<BizFlow.Application.Audit.IAuditReader, BizFlow.Infrastructure.Audit.AuditReader>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new UnprocessableEntityObjectResult(
        ApiErrors.Create(context.HttpContext, "VALIDATION.FAILED", "Please correct the invalid fields.",
            context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
                // Model-binding exceptions can echo submitted secrets. Expose field names only.
                .ToDictionary(entry => entry.Key, _ => new[] { "The supplied value is invalid." })));
});
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostics, context) =>
    diagnostics.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier));
// Log the final, sanitized response status, not the exception before Application faults are mapped.
app.UseExceptionHandler();
app.UseStatusCodePages(async status =>
    await status.HttpContext.Response.WriteAsJsonAsync(ApiErrors.ForStatus(status.HttpContext)));
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// Operational probes do not authenticate or expose tenant/business/dependency details.
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/health/ready", HealthEndpoints.ReadyAsync).AllowAnonymous();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.Run();

public partial class Program;
