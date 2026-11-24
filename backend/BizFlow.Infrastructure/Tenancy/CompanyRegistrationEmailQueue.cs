using System.Text.Json;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tenancy;

public sealed record RegistrationEmailPayload(string Kind, long Version, string Code, string Name, string ContactEmail,
    Guid? TenantId,Guid? AdminUserId,string? Reason);

internal static class CompanyRegistrationEmailQueue
{
    public static Task EnqueueAsync(BizFlowDbContext db,Company company,string kind,Guid? tenant,Guid? admin,string? reason,CancellationToken ct)
    {
        var payload=JsonSerializer.Serialize(new RegistrationEmailPayload(kind,company.UpdatedAt.UtcTicks/10,company.Code,company.Name,company.ContactEmail,tenant,admin,reason));
        var id=Guid.CreateVersion7();
        return db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CompanyRegistrationEmail\" (\"Id\",\"CompanyId\",\"Payload\") VALUES ({id},{company.Id},{payload}::jsonb)",ct);
    }
}
