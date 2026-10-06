using BizFlow.Application.Security;
using System.Security.Claims;

namespace BizFlow.Api.Security;

public sealed class ClaimsTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? UserId => ReadSingleId("sub");
    public Guid? TenantId => ReadSingleId("tenant_id");

    private Guid? ReadSingleId(string claimType)
    {
        var identity = accessor.HttpContext?.User.Identity as ClaimsIdentity;
        if (identity?.IsAuthenticated != true) return null;
        var values = identity.FindAll(claimType).Select(c => c.Value).ToArray();
        return values.Length == 1 && Guid.TryParse(values[0], out var id) && id != Guid.Empty ? id : null;
    }
}
