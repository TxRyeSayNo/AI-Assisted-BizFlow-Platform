using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Services;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;

namespace BizFlow.Infrastructure.Persistence;

public static class PostgresOptions
{
    public static DbContextOptionsBuilder<BizFlowDbContext> UseBizFlowPostgres(
        this DbContextOptionsBuilder<BizFlowDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, options =>
        {
            options.MapEnum<CompanyStatus>("company_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<ServiceStatus>("service_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<RequestState>("request_state", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<RequestPriority>("request_priority", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<TaskState>("task_state", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<TaskPriority>("task_priority", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<ServiceCategoryStatus>("service_category_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<SlaProfileStatus>("sla_profile_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<TenantStatus>("tenant_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<UserStatus>("user_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<RecordStatus>("record_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<PermissionScope>("permission_scope", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<AuditActorType>("audit_actor_type", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<WorkflowBusinessType>("workflow_business_type", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<WorkflowStatus>("workflow_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<WorkflowVersionStatus>("workflow_version_status", nameTranslator: UpperCaseEnumTranslator.Instance);
            options.MapEnum<WorkflowStepType>("workflow_step_type", nameTranslator: UpperCaseEnumTranslator.Instance);
        });
}

internal sealed class UpperCaseEnumTranslator : INpgsqlNameTranslator
{
    internal static readonly UpperCaseEnumTranslator Instance = new();
    private readonly NpgsqlSnakeCaseNameTranslator snakeCase = new();
    public string TranslateTypeName(string clrName) => snakeCase.TranslateTypeName(clrName);
    public string TranslateMemberName(string clrName) => snakeCase.TranslateMemberName(clrName).ToUpperInvariant();
}
