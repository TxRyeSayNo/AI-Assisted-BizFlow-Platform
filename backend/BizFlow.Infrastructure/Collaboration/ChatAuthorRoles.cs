using BizFlow.Domain.Organization;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Collaboration;

internal static class ChatAuthorRoles
{
    public static async Task<Dictionary<Guid, string[]>> ReadAsync(BizFlowDbContext db, Guid[] userIds, CancellationToken cancellationToken)
    {
        var rows = await (from ur in db.UserRoles.AsNoTracking()
                          join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id
                          where userIds.Contains(ur.UserId) && role.Status == RecordStatus.Active
                          select new { ur.UserId, role.Name }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.Select(r => r.Name).Distinct().Order().ToArray());
    }
}
