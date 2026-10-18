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


    public int CountFlights(
        string? origin = null,
        string? destination = null,
        string? flightType = null,
        DateOnly? date = null)
    {
        IEnumerable<FlightSnapshot> flights = manager.GetFlights();

        if (!string.IsNullOrWhiteSpace(origin))
        {
            flights = flights.Where(f =>
                string.Equals(
                    f.Origin,
                    origin,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(destination))
        {
            flights = flights.Where(f =>
                string.Equals(
                    f.Destination,
                    destination,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(flightType))
        {
            flights = flights.Where(f =>
                string.Equals(
                    f.Type,
                    flightType,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (date.HasValue)
        {
            flights = flights.Where(f =>
                DateOnly.FromDateTime(f.ScheduledTime.UtcDateTime)
                    == date.Value);
        }

        return flights.Count();
    }

}
