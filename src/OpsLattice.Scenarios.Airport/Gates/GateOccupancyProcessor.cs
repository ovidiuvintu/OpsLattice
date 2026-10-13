using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateOccupancyProcessor
{
    private static readonly TimeSpan TaxiDuration =
        TimeSpan.FromMinutes(10);

    private readonly ISimulationClock _clock;
    private readonly IGateProvider _gateProvider;

    public GateOccupancyProcessor(
        ISimulationClock clock,
        IGateProvider gateProvider)
    {
        _clock = clock;
        _gateProvider = gateProvider;
    }

    public DateTimeOffset? GetNextExecutionTime(Flight flight)
    {
        if (flight.Type != FlightType.Arrival ||
            flight.Status != FlightStatus.Landed ||
            !flight.LandedAt.HasValue)
        {
            return null;
        }

        var gate = _gateProvider
            .GetGates()
            .FirstOrDefault(g => g.AssignedFlight == flight);

        if (gate is null || gate.IsOccupied)
        {
            return null;
        }

        var taxiCompletedAt =
            flight.LandedAt.Value + TaxiDuration;

        return taxiCompletedAt > _clock.CurrentTime
            ? taxiCompletedAt
            : null;
    }

    public void Process(Flight flight)
    {
        if (flight.Status != FlightStatus.Landed)
        {
            return;
        }

        if (!flight.LandedAt.HasValue)
        {
            return;
        }

        if (_clock.CurrentTime <
            flight.LandedAt.Value + TaxiDuration)
        {
            return;
        }

        var gate = _gateProvider
            .GetGates()
            .FirstOrDefault(
                gate => gate.AssignedFlight == flight);

        if (gate is null)
        {
            return;
        }

        if (gate.IsOccupied)
        {
            return;
        }

        gate.Occupy(flight);
        flight.ArriveAtGate(_clock.CurrentTime);
    }
}