using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Infrastructure.Notifications;
using BizFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController,Authorize,Route("api/v1/notifications")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NotificationPreferencesController(BizFlowDbContext db,ITenantContext context,TenantMembershipAuthorizer membership,TaskDigestService digest):ControllerBase
{
    [HttpGet("preferences")]
    public async Task<NotificationPreferencesView> Get(CancellationToken ct)
    {
        var tenant=await membership.RequireAsync("notifications.preferences",ct);
        return await NotificationPreferenceStore.ReadAsync(db,tenant,context.UserId!.Value,ct);
    }
    [HttpPut("preferences")]
    public async Task<NotificationPreferencesView> Save(NotificationPreferencesView input,CancellationToken ct)
    {
        var tenant=await membership.RequireAsync("notifications.preferences",ct);
        if(input.DeadlineEmailMode is not ("Always" or "AwayOnly" or "Off") || input.DigestHour is <0 or >23 || input.DigestDays is <1 or >30)
            throw new ApplicationFault(FaultKind.Validation,"NOTIFICATION.INVALID_PREFERENCES","Chế độ mail, giờ tổng hợp hoặc số ngày không hợp lệ.");
        await NotificationPreferenceStore.WriteAsync(db,tenant,context.UserId!.Value,input,ct);
        return input;
    }
    [HttpPost("digests")]
    public async Task<object> Create(TaskDigestInput input,CancellationToken ct)
    {
        var tenant=await membership.RequireAsync("notifications.digest",ct);
        var result=await digest.CreateAsync(tenant,context.UserId!.Value,input,false,ct);
        return new{created=result is not null,result};
    }
}
