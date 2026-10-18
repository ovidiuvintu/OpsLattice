namespace OpsLattice.Scenarios.Airport.Gates;

public sealed record GateSummaryDto(
    int Total,
    int Available,
    int Assigned,
    int Occupied);
