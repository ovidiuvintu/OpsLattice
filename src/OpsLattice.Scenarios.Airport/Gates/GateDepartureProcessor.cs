using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateDepartureProcessor
{
    private static readonly TimeSpan _turnaroundDuration =
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

    public DateTimeOffset? GetNextExecutionTime(Flight flight)
    {
        if (flight.Type != FlightType.Arrival ||
            flight.Status != FlightStatus.AtGate ||
            !flight.ArrivedAtGateAt.HasValue)
        {
            return null;
        }

        return flight.ArrivedAtGateAt.Value +
               _turnaroundDuration;
    }

    public void Process(Flight flight)
    {
        if (flight.Type != FlightType.Arrival ||
            flight.Status != FlightStatus.AtGate)
        {
            return;
        }

        if (!flight.ArrivedAtGateAt.HasValue)
        {
            return;
        }

        if (_clock.CurrentTime <
            flight.ArrivedAtGateAt.Value + _turnaroundDuration)
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

        if (gate.AssignedFlight != flight ||  gate.OccupyingFlight != flight)
        {
            throw new InvalidOperationException(
                $"Inconsistent gate state for flight {flight.FlightNumber}.");
        }

        flight.Depart();
        gate.Release(flight);
    }
}
