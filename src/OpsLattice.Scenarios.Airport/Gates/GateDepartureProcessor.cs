using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateDepartureProcessor
{
    private static readonly TimeSpan TurnaroundDuration =
        TimeSpan.FromMinutes(30);

    private readonly ISimulationClock _clock;
    private readonly IGateProvider _gateProvider;

    public GateDepartureProcessor(
        ISimulationClock clock,
        IGateProvider gateProvider)
    {
        _clock = clock;
        _gateProvider = gateProvider;
    }

    public void Process(Flight flight)
    {
        if (flight.Status != FlightStatus.AtGate)
        {
            return;
        }

        if (!flight.ArrivedAtGateAt.HasValue)
        {
            return;
        }

        if (_clock.CurrentTime <
            flight.ArrivedAtGateAt.Value + TurnaroundDuration)
        {
            return;
        }

        var gate = _gateProvider
            .GetGates()
            .FirstOrDefault(
                gate => gate.OccupyingFlight == flight);

        if (gate is null)
        {
            return;
        }

        flight.Depart();
        gate.Release(flight);
    }
}
