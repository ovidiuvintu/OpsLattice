namespace OpsLattice.Agents.Planning;

public interface IAirportQueryValidator
{
    Task<AirportQueryValidationResult> ValidateAsync(
        string question,
        AirportQueryPlanDraft? draft,
        CancellationToken cancellationToken = default);
}
