namespace OpsLattice.Scenarios.Airport.Simulation;

public sealed record GateSnapshot(
    string Code,
    string? TerminalCode,
    bool IsAssigned,
    bool IsOccupied,
    bool IsAvailable,
    string? AssignedFlightNumber,
    string? OccupyingFlightNumber);
