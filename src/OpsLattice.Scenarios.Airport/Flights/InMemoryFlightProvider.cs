namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class InMemoryFlightProvider : IFlightProvider
{
    private readonly List<Flight> _flights =
    [
        new()
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero)
        },
        new()
        {
            FlightNumber = "DL456",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero)
        },
        new()
        {
            FlightNumber = "AA789",
            Airline = "American Airlines",
            Origin = "DFW",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 16, 0, 0, TimeSpan.Zero)
        }
    ];

    public IReadOnlyCollection<Flight> GetFlights()
    {
        return _flights.AsReadOnly();
    }
}
