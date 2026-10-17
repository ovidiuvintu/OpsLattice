using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightQueryService(
    IFlightProvider flightProvider,
    IGateProvider gateProvider)
{
    public IReadOnlyCollection<Flight> GetFlights()
    {
        return flightProvider.GetFlights();
    }

    public IReadOnlyCollection<FlightStatusDto> GetFlightStatuses()
    {
        var gates = gateProvider.GetGates();

        return flightProvider.GetFlights()
            .Select(flight =>
            {
                var assignedGate = gates.FirstOrDefault(
                    gate => ReferenceEquals(
                        gate.AssignedFlight, flight));

                var occupiedGate = gates.FirstOrDefault(
                    gate => ReferenceEquals(
                        gate.OccupyingFlight, flight));

                return new FlightStatusDto(
                    flight.FlightNumber,
                    flight.Status.ToString(),
                    assignedGate?.Code,
                    occupiedGate?.Code);
            })
            .ToArray();
    }
}