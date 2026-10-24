namespace OpsLattice.Agents.Planning;

public interface IAirportQueryExecutor
{
    Task<AirportQueryExecutionResult> ExecuteAsync(
        AirportQueryPlan plan,
        CancellationToken cancellationToken = default);
}
