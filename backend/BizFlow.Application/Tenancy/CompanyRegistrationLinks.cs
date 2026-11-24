namespace BizFlow.Application.Tenancy;

public sealed record RegistrationResubmissionTicket(Guid CompanyId, string Email, long UpdatedTicks, Guid DecisionId);
public interface ICompanyRegistrationLinks
{
    string Issue(RegistrationResubmissionTicket ticket);
    Task<RegistrationResubmissionTicket?> ReadAsync(string token, CancellationToken ct);
}
