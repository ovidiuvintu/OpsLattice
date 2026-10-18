using OpsLattice.Scenarios.Airport.Simulation;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightQueryService(
    AirportSimulationManager manager)
{
    public IReadOnlyCollection<FlightSnapshot> GetFlights()
    {
        return manager.GetFlights();
    }

    public IReadOnlyCollection<FlightStatusDto> GetFlightStatuses()
    {
        return manager.GetFlights()
            .Select(flight => new FlightStatusDto(
                flight.FlightNumber,
                flight.Status,
                flight.Gate,
                flight.OccupiedGate))
            .ToArray();
    }
}
