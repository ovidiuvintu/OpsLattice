namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class InMemoryFlightProvider : IFlightProvider
{
    private readonly IReadOnlyCollection<Flight> _flights;

    public InMemoryFlightProvider(
        IEnumerable<Flight> flights)
    {
        ArgumentNullException.ThrowIfNull(flights);

        _flights = Array.AsReadOnly(flights.ToArray());
    }

    public IReadOnlyCollection<Flight> GetFlights()
    {
        return _flights;
    }
}