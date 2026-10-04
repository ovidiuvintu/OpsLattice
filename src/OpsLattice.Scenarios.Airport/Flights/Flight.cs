namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class Flight
{
    public required string FlightNumber { get; init; }

    public required string Airline { get; init; }

    public required string Origin { get; init; }

    public required string Destination { get; init; }

    public DateTimeOffset ScheduledTime { get; init; }

    public FlightStatus Status { get; private set; } = FlightStatus.Scheduled;
}
