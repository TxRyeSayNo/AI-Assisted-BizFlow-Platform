using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Notifications;

public sealed record NotificationPreferencesView(bool EmailEnabled=true,string DeadlineEmailMode="AwayOnly",
    bool DailyDigestEnabled=false,int DigestHour=8,int DigestDays=1,bool DigestEmail=true);

public static class NotificationPreferenceStore
{
    public static async Task<NotificationPreferencesView> ReadAsync(BizFlowDbContext db,Guid tenant,Guid user,CancellationToken ct)
    {
        var rows=await db.Database.SqlQuery<NotificationPreferencesView>($"""
            SELECT "EmailEnabled","DeadlineEmailMode","DailyDigestEnabled","DigestHour","DigestDays","DigestEmail"
            FROM "NotificationPreference" WHERE "TenantId"={tenant} AND "UserId"={user}
            """).ToArrayAsync(ct);
        return rows.SingleOrDefault()??new();
    }
    public static Task WriteAsync(BizFlowDbContext db,Guid tenant,Guid user,NotificationPreferencesView input,CancellationToken ct)=>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "NotificationPreference" ("TenantId","UserId","EmailEnabled","DeadlineEmailMode","DailyDigestEnabled","DigestHour","DigestDays","DigestEmail")
            VALUES ({tenant},{user},{input.EmailEnabled},{input.DeadlineEmailMode},{input.DailyDigestEnabled},{input.DigestHour},{input.DigestDays},{input.DigestEmail})
            ON CONFLICT ("UserId") DO UPDATE SET "EmailEnabled"=EXCLUDED."EmailEnabled","DeadlineEmailMode"=EXCLUDED."DeadlineEmailMode",
              "DailyDigestEnabled"=EXCLUDED."DailyDigestEnabled","DigestHour"=EXCLUDED."DigestHour","DigestDays"=EXCLUDED."DigestDays","DigestEmail"=EXCLUDED."DigestEmail"
            WHERE "NotificationPreference"."TenantId"={tenant}
            """,ct);
}
