using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateAssignmentProcessor
{
    private static readonly TimeSpan AssignmentHorizon =
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
        // For now, gate planning applies only to flights that
        // have not yet started their arrival lifecycle.
        if (flight.Status != FlightStatus.Scheduled)
        {
            return;
        }

        var timeUntilArrival =
            flight.ScheduledTime - _clock.CurrentTime;

        // Do not assign a gate until the flight enters
        // the 60-minute gate-planning horizon.
        if (timeUntilArrival > AssignmentHorizon)
        {
            return;
        }

        // Find the first gate that has not already
        // been assigned to another flight.
        var gate = _gateProvider
            .GetGates()
            .FirstOrDefault(gate => !gate.IsAssigned);

        // No gate is currently available for assignment.
        // We'll handle conflicts explicitly later.
        if (gate is null)
        {
            return;
        }

        gate.Assign(flight);
    }
}
