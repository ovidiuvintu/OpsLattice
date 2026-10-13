using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateAssignmentSimulationStep
    : IScheduledSimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly GateAssignmentProcessor _processor;

    public GateAssignmentSimulationStep(
        IFlightProvider flightProvider,
        GateAssignmentProcessor processor)
    {
        _flightProvider = flightProvider;
        _processor = processor;
    }

    public DateTimeOffset? GetNextExecutionTime(
        DateTimeOffset currentTime,
        DateTimeOffset targetTime)
    {
        DateTimeOffset? nextExecutionTime = null;

        foreach (var flight in _flightProvider.GetFlights())
        {
            var candidate =
                _processor.GetNextExecutionTime(flight);

            if (!candidate.HasValue)
            {
                continue;
            }

            if (candidate.Value <= currentTime ||
                candidate.Value > targetTime)
            {
                continue;
            }

            if (!nextExecutionTime.HasValue ||
                candidate.Value < nextExecutionTime.Value)
            {
                nextExecutionTime = candidate.Value;
            }
        }

        return nextExecutionTime;
    }

    public void Execute()
    {
        var flights = _flightProvider.GetFlights();

        // First priority: aircraft that have already landed.
        var waitingFlights = flights
            .Where(f =>
                f.Type == FlightType.Arrival &&
                f.Status == FlightStatus.Landed &&
                f.LandedAt.HasValue)
            .OrderBy(f => f.LandedAt!.Value)
            .ThenBy(f => f.FlightNumber, StringComparer.Ordinal);

        foreach (var flight in waitingFlights)
        {
            _processor.Process(flight);
        }

        // Preserve advance assignment for other arriving flights.
        foreach (var flight in flights.Where(f =>
            f.Type == FlightType.Arrival &&
            (f.Status == FlightStatus.Scheduled ||
             f.Status == FlightStatus.Approaching)))
        {
            _processor.Process(flight);
        }
    }
}