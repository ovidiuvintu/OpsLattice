using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightArrivalSimulationStep
    : IScheduledSimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly FlightArrivalProcessor _arrivalProcessor;

    public FlightArrivalSimulationStep(
        IFlightProvider flightProvider,
        FlightArrivalProcessor arrivalProcessor)
    {
        _flightProvider = flightProvider;
        _arrivalProcessor = arrivalProcessor;
    }

    public DateTimeOffset? GetNextExecutionTime(
        DateTimeOffset currentTime,
        DateTimeOffset targetTime)
    {
        DateTimeOffset? nextExecutionTime = null;

        foreach (var flight in _flightProvider.GetFlights())
        {
            var candidate =
                GetNextExecutionTime(flight);

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

    private static DateTimeOffset? GetNextExecutionTime(
        Flight flight)
    {
        return flight.Status switch
        {
            FlightStatus.Scheduled =>
                flight.ScheduledTime,

            FlightStatus.Approaching
                when flight.ApproachStartedAt.HasValue =>
                    flight.ApproachStartedAt.Value
                        .AddMinutes(10),

            _ => null
        };
    }

    public void Execute()
    {
        foreach (var flight in _flightProvider.GetFlights())
        {
            _arrivalProcessor.Process(flight);
        }
    }
}