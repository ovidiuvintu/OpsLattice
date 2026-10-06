namespace OpsLattice.Api.Models;

public sealed record FlightResponse(
    string FlightNumber,
    string Airline,
    string Origin,
    string Destination,
    DateTimeOffset ScheduledTime,
    string Status,
    DateTimeOffset? ApproachStartedAt,
    DateTimeOffset? LandedAt,
    DateTimeOffset? ArrivedAtGateAt,
    string? Gate);
