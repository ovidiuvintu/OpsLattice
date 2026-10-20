namespace OpsLattice.Agents.Planning;

public interface IAirportQueryPlanner
{
    Task<AirportPlannerOutput> BuildPlanAsync(
        string question,
        CancellationToken cancellationToken = default);
}
