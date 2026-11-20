using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.Requests;

// Level of authority used to enforce "no level-skipping" when a requester picks a recipient:
//   0 = employee, 1 = manager (requests.route), 2 = director/admin (requests.approve.elevated).
public sealed record RecipientUserOption(Guid UserId, string FullName, string EmployeeCode, int Level);
public sealed record RecipientDepartmentOption(Guid DepartmentId, string Name);
public sealed record RecipientOptions(
    int MyLevel,
    IReadOnlyList<RecipientDepartmentOption> Departments,
    IReadOnlyList<RecipientUserOption> Users);

public interface IRequestRecipientStore
{
    Task<int> GetUserLevelAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<RecipientOptions> ListAllowedAsync(Guid tenantId, int myLevel, Guid excludeUserId, CancellationToken cancellationToken);
}

public sealed class RequestRecipientQuery(ITenantContext context, IRequestRecipientStore store)
{
    public async Task<RecipientOptions> ListAsync(CancellationToken cancellationToken)
    {
        if (context.TenantId is not { } tenantId || context.UserId is not { } userId)
            throw ApplicationFault.NotFound();
        var myLevel = await store.GetUserLevelAsync(tenantId, userId, cancellationToken);
        return await store.ListAllowedAsync(tenantId, myLevel, userId, cancellationToken);
    }
}
