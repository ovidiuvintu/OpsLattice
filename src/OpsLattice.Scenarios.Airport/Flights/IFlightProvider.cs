namespace OpsLattice.Scenarios.Airport.Flights;

public interface IFlightProvider
{
    IReadOnlyCollection<Flight> GetFlights();
}
