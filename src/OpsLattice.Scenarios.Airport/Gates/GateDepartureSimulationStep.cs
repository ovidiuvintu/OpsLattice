using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateDepartureSimulationStep
    : IScheduledSimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly GateDepartureProcessor _processor;

    public GateDepartureSimulationStep(
        IFlightProvider flightProvider,
        GateDepartureProcessor processor)
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
        foreach (var flight in _flightProvider.GetFlights())
        {
            _processor.Process(flight);
        }
    }
}