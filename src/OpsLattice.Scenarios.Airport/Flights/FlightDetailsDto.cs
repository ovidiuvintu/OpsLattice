namespace OpsLattice.Scenarios.Airport.Flights;

public sealed record FlightDetailsDto(
    string FlightNumber,
    string Airline,
    string Type,
    string Origin,
    string Destination,
    DateTimeOffset ScheduledTime,
    string Status,
    string? Gate);
