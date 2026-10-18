using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateAssignmentProcessor
{
    private static readonly TimeSpan _assignmentHorizon =
        TimeSpan.FromMinutes(60);

    private readonly ISimulationClock _clock;
    private readonly IGateProvider _gateProvider;

    public GateAssignmentProcessor(
        ISimulationClock clock,
        IGateProvider gateProvider)
    {
        _clock = clock;
        _gateProvider = gateProvider;
    }

    public void Process(Flight flight)
    {
        if (flight.Type != FlightType.Arrival)
        {
            return;
        }

        if (flight.Status != FlightStatus.Scheduled &&
            flight.Status != FlightStatus.Approaching &&
            flight.Status != FlightStatus.Landed)
        {
            return;
        }

        var timeUntilArrival =
            flight.ScheduledTime - _clock.CurrentTime;

        // Do not assign a gate before the flight
        // enters the 60-minute planning horizon.
        if (timeUntilArrival > _assignmentHorizon)
        {
            return;
        }

        var gates = _gateProvider.GetGates();

        // A flight must not be assigned to more
        // than one gate.
        if (gates.Any(gate =>
            gate.AssignedFlight == flight))
        {
            return;
        }

        // Find the first gate that is not assigned
        // to another flight.
        var gate = gates.FirstOrDefault(
            gate => !gate.IsAssigned);

        // No available gate.
        // Retry behavior will be implemented separately.
        if (gate is null)
        {
            return;
        }

        // Assign the arrival flight to the gate.
        gate.Assign(flight);
    }

    public DateTimeOffset? GetNextExecutionTime(
        Flight flight)
    {
        // Only scheduled arrivals generate
        // gate-assignment scheduling events.
        if (flight.Type != FlightType.Arrival ||
            flight.Status != FlightStatus.Scheduled)
        {
            return null;
        }

        return flight.ScheduledTime - _assignmentHorizon;
    }
}
