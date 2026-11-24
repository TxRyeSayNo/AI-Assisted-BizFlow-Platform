using BizFlow.Application.Tenancy;
using BizFlow.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BizFlow.Infrastructure.Tenancy;

public sealed class CompanyRegistrationLinks(JwtSettings settings, TimeProvider clock) : ICompanyRegistrationLinks
{
    public string Issue(RegistrationResubmissionTicket ticket)
    {
        var now=clock.GetUtcNow();
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor {
            Issuer=settings.Issuer,Audience=settings.Audience+":registration-resubmit",TokenType="registration-resubmit+jwt",
            IssuedAt=now.UtcDateTime,NotBefore=now.UtcDateTime,Expires=now.AddDays(7).UtcDateTime,
            SigningCredentials=new(settings.SigningKey(),SecurityAlgorithms.HmacSha256),
            Claims=new Dictionary<string,object>{{"sub",ticket.CompanyId.ToString()},{"email",ticket.Email},{"version",ticket.UpdatedTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"decision",ticket.DecisionId.ToString()}}
        });
    }
    public async Task<RegistrationResubmissionTicket?> ReadAsync(string token,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(string.IsNullOrEmpty(token)||token.Length>4096)return null;
        var parameters=settings.ValidationParameters();parameters.ValidAudience=settings.Audience+":registration-resubmit";
        parameters.ValidTypes=["registration-resubmit+jwt"];parameters.ClockSkew=TimeSpan.Zero;
        parameters.LifetimeValidator=(start,end,_,_)=>start.HasValue&&end.HasValue&&start<=clock.GetUtcNow().UtcDateTime&&end>clock.GetUtcNow().UtcDateTime;
        var result=await new JsonWebTokenHandler().ValidateTokenAsync(token,parameters);
        string? Claim(string name)=>result.ClaimsIdentity?.FindFirst(name)?.Value;
        return result.IsValid&&Guid.TryParse(Claim("sub"),out var id)&&id!=Guid.Empty&&Guid.TryParse(Claim("decision"),out var decision)&&decision!=Guid.Empty&&long.TryParse(Claim("version"),out var ticks)&&Claim("email") is {Length:>0} email
            ?new(id,email,ticks,decision):null;
    }
}
