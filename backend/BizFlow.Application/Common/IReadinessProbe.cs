namespace BizFlow.Application.Common;

// Operational dependency check only; never migrates, seeds or reads tenant business data.
public interface IReadinessProbe
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
