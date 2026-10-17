namespace OpsLattice.Scenarios.Airport.Flights;

public sealed record FlightStatusDto(
    string FlightNumber,
    string Status,
    string? AssignedGate,
    string? OccupiedGate);