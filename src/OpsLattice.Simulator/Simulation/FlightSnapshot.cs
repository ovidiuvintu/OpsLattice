namespace OpsLattice.Scenarios.Airport.Simulation;

public sealed record FlightSnapshot(
    string FlightNumber,
    string Airline,
    string Type,
    string Origin,
    string Destination,
    DateTimeOffset ScheduledTime,
    string Status,
    DateTimeOffset? ApproachStartedAt,
    DateTimeOffset? LandedAt,
    DateTimeOffset? ArrivedAtGateAt,
    string? Gate,
    string? OccupiedGate);
