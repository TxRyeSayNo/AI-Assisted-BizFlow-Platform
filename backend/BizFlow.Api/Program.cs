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
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskExecutionCommand>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskSubmissionCommand>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskConfirmationCommand>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskExecutionStore, BizFlow.Infrastructure.Tasks.TaskExecutionStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskSubmissionStore, BizFlow.Infrastructure.Tasks.TaskSubmissionStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskConfirmationStore, BizFlow.Infrastructure.Tasks.TaskConfirmationStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskAcceptanceStore, BizFlow.Infrastructure.Tasks.TaskAcceptanceStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskAssignmentStore, BizFlow.Infrastructure.Tasks.TaskAssignmentStore>();
builder.Services.AddScoped<BizFlow.Domain.Workflows.IWorkflowEngine, BizFlow.Domain.Workflows.TaskWorkflowEngine>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskCreationStore, BizFlow.Infrastructure.Tasks.TaskCreationStore>();
builder.Services.AddScoped<BizFlow.Application.Tasks.TaskDetailQuery>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskDetailReader, BizFlow.Infrastructure.Tasks.TaskDetailReader>();
builder.Services.AddScoped<BizFlow.Application.Tasks.ITaskListReader, BizFlow.Infrastructure.Tasks.TaskListReader>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestListQuery>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestCreation>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestSubmissionCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestRoutingCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestReceiptCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestProcessingCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestRejectionCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestCancellationCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestResolutionCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestConfirmationCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestReviseCommand>();
builder.Services.AddScoped<BizFlow.Application.Requests.RequestDetailQuery>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestCreationStore, BizFlow.Infrastructure.Requests.RequestCreationStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestSubmissionStore, BizFlow.Infrastructure.Requests.RequestSubmissionStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestRoutingStore, BizFlow.Infrastructure.Requests.RequestRoutingStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestReceiptStore, BizFlow.Infrastructure.Requests.RequestReceiptStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestProcessingStore, BizFlow.Infrastructure.Requests.RequestProcessingStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestRejectionStore, BizFlow.Infrastructure.Requests.RequestRejectionStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestCancellationStore, BizFlow.Infrastructure.Requests.RequestCancellationStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestResolutionStore, BizFlow.Infrastructure.Requests.RequestResolutionStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestConfirmationStore, BizFlow.Infrastructure.Requests.RequestConfirmationStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestReviseStore, BizFlow.Infrastructure.Requests.RequestReviseStore>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestListReader, BizFlow.Infrastructure.Requests.RequestListReader>();
builder.Services.AddScoped<BizFlow.Application.Requests.IRequestDetailReader, BizFlow.Infrastructure.Requests.RequestDetailReader>();
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
builder.Services.AddScoped<BizFlow.Application.Collaboration.CommentService>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.ICommentStore, BizFlow.Infrastructure.Collaboration.CommentStore>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.ICommentReader, BizFlow.Infrastructure.Collaboration.CommentReader>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.IObjectStorageProvider, BizFlow.Infrastructure.Collaboration.LocalStorageProvider>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.IAttachmentStore, BizFlow.Infrastructure.Collaboration.AttachmentStore>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.IAttachmentReader, BizFlow.Infrastructure.Collaboration.AttachmentReader>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.AttachmentService>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.IRecordArchivalStore, BizFlow.Infrastructure.Collaboration.RecordArchivalStore>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.IRecordSearchReader, BizFlow.Infrastructure.Collaboration.RecordSearchReader>();
builder.Services.AddScoped<BizFlow.Application.Collaboration.RecordService>();
builder.Services.AddScoped<BizFlow.Application.Reports.IReportingReader, BizFlow.Infrastructure.Reports.ReportingReader>();
builder.Services.AddScoped<BizFlow.Application.Reports.ReportingService>();
builder.Services.AddScoped<BizFlow.Application.AI.IAiStore, BizFlow.Infrastructure.AI.AiStore>();
builder.Services.AddScoped<BizFlow.Application.AI.IAiContextReader, BizFlow.Infrastructure.AI.AiContextReader>();
builder.Services.AddSingleton<BizFlow.Application.AI.IAiToolRegistry, BizFlow.Infrastructure.AI.AiToolRegistry>();
builder.Services.AddScoped<BizFlow.Application.AI.IRequestSplitStore, BizFlow.Infrastructure.AI.RequestSplitStore>();
builder.Services.AddScoped<BizFlow.Application.AI.IAiActionDispatcher, BizFlow.Infrastructure.AI.AiActionDispatcher>();
builder.Services.AddScoped<BizFlow.Application.AI.AiAssistanceService>();
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
builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);
builder.Services.AddSingleton<BizFlow.Api.Realtime.NotificationConnections>();
builder.Services.AddScoped<BizFlow.Application.Notifications.INotificationUpdates, BizFlow.Api.Realtime.SignalRNotificationUpdates>();
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
app.MapHub<BizFlow.Api.Realtime.NotificationHub>(BizFlow.Api.Realtime.NotificationHub.Path,
    options => options.CloseOnAuthenticationExpiration = true);
app.Run();

public partial class Program;
