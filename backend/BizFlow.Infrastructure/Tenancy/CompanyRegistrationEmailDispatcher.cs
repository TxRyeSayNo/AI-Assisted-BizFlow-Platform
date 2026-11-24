using System.Text.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Security;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Notifications;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BizFlow.Infrastructure.Tenancy;

public sealed class CompanyRegistrationEmailDispatcher(DbContextOptions<BizFlowDbContext> options,EmailNotificationDispatcher sender,
    ICompanyRegistrationLinks links,IPasswordResetTokenProvider passwords,PasswordResetPolicy policy,TimeProvider clock,IConfiguration configuration)
{
    private sealed record SystemContext() : ITenantContext {public Guid? UserId=>null;public Guid? TenantId=>null;}
    public sealed class Delivery
    {
        public Guid Id {get;set;}
        public Guid CompanyId {get;set;}
        public string Payload {get;set;}="";
        public int Attempts {get;set;}
        public DateTimeOffset? ConsumedAt {get;set;}
    }
    public async Task<int> SweepAsync(CancellationToken ct, Guid? companyId = null)
    {
        var settings=NotificationEmailConfiguration.Resolve(configuration);
        if(!settings.Enabled||!settings.IsValid)return 0;
        var sent=0;
        for(var i=0;i<20;i++)
        {
            await using var db=new BizFlowDbContext(options,new SystemContext());
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            var now=clock.GetUtcNow();
            var rows=await db.Database.SqlQuery<Delivery>($"""
                SELECT "Id","CompanyId","Payload"::text AS "Payload","Attempts","ConsumedAt" FROM "CompanyRegistrationEmail"
                WHERE "Status"='PENDING' AND "NextAttemptAt"<={now} AND ({companyId}::uuid IS NULL OR "CompanyId"={companyId}) ORDER BY "NextAttemptAt","Id" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).ToArrayAsync(ct);
            if(rows.Length==0)break;
            var row=rows[0];var payload=JsonSerializer.Deserialize<RegistrationEmailPayload>(row.Payload)!;
            // This platform queue predates tenant creation; all resource reads are bound to its company/user IDs.
            var company=await db.Companies.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c=>c.Id==row.CompanyId,ct);
            var status="SKIPPED";var attempts=row.Attempts;DateTimeOffset? sentAt=null;
            if(company is not null && (payload.Kind=="RECEIVED" || company.UpdatedAt.UtcTicks/10==payload.Version &&
                (payload.Kind=="REJECTED"&&row.ConsumedAt is null&&company.Status==CompanyStatus.Inactive || payload.Kind is "APPROVED" or "APPROVED_CONTACT" && company.Status==CompanyStatus.Active)))
            {
                var address=payload.ContactEmail;var title="Đã tiếp nhận đăng ký doanh nghiệp";
                var content=$"Doanh nghiệp: {payload.Name}\nMã hồ sơ: {payload.Code}\nChúng tôi đã tiếp nhận hồ sơ. Kết quả xét duyệt và thông tin workspace sẽ được gửi trong email riêng.";
                var path="/register-company";
                var eligible=true;
                if(payload.Kind=="REJECTED")
                {
                    title="Hồ sơ đăng ký cần bổ sung";
                    var token=links.Issue(new(company.Id,company.ContactEmail.ToUpperInvariant(),company.UpdatedAt.UtcTicks,row.Id));
                    path="/register-company#resubmit="+Uri.EscapeDataString(token);
                    content=$"Doanh nghiệp: {payload.Name}\nMã hồ sơ: {payload.Code}\nHồ sơ chưa được phê duyệt.\nLý do từ chối / nội dung cần bổ sung:\n{payload.Reason}\n\nDùng liên kết bên dưới để bổ sung hồ sơ. Liên kết có hạn 7 ngày và chỉ dùng được cho lần từ chối này.";
                }
                if(payload.Kind is "APPROVED" or "APPROVED_CONTACT")
                {
                    var tenant=await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t=>t.Id==payload.TenantId&&t.CompanyId==company.Id&&t.Status==TenantStatus.Active,ct);
                    var user=await db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u=>u.Id==payload.AdminUserId&&u.TenantId==payload.TenantId&&u.Status==UserStatus.Active&&u.DeletedAt==null,ct);
                    eligible=tenant is not null&&user is not null;
                    if(tenant is not null && user is not null)
                    {
                        address=payload.Kind=="APPROVED_CONTACT"?payload.ContactEmail:user!.Email;title="Đăng ký doanh nghiệp đã được phê duyệt";
                        content=$"Doanh nghiệp: {payload.Name}\nWorkspace-key: {tenant!.TenantKey}\nQuản trị viên: {user.FullName}\nMã tài khoản: {user.EmployeeCode}\nEmail đăng nhập: {user.Email}\n";
                        path="/login";
                        if(user.MustChangePassword && payload.Kind=="APPROVED")
                        {
                            var token=passwords.Issue(new(user.Id,user.TenantId,user.SecurityStamp,user.NormalizedEmail,now.AddMinutes(policy.TokenLifetimeMinutes)),now);
                            path="/reset-password#token="+Uri.EscapeDataString(token)+"&tenantKey="+Uri.EscapeDataString(tenant.TenantKey)+"&identifier="+Uri.EscapeDataString(user.EmployeeCode);
                            content+=$"\nMở liên kết để tự đặt mật khẩu. Liên kết có hạn {policy.TokenLifetimeMinutes} phút và chỉ dùng một lần. Nếu hết hạn, dùng Quên mật khẩu với workspace-key và tài khoản trên. Mật khẩu không được gửi qua email.";
                        }
                    }
                }
                if(eligible)
                {
                    attempts++;var delivered=await sender.SendToAddressAsync(address,title,content,path,row.Id,ct);
                    status=delivered?"SENT":attempts>=5?"FAILED":"PENDING";
                    if(delivered){sent++;sentAt=clock.GetUtcNow();}
                }
            }
            var next=now.AddMinutes(attempts switch{1=>1,2=>5,3=>15,_=>60});
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CompanyRegistrationEmail\" SET \"Status\"={status},\"Attempts\"={attempts},\"NextAttemptAt\"={next},\"SentAt\"={sentAt} WHERE \"Id\"={row.Id}",ct);
            await tx.CommitAsync(ct);
        }
        return sent;
    }
}
