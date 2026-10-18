namespace OpsLattice.Scenarios.Airport.Gates;

public sealed record GateStatusDto(
    string Code,
    bool IsAssigned,
    bool IsOccupied,
    bool IsAvailable,
    string? AssignedFlightNumber,
    string? OccupyingFlightNumber);
