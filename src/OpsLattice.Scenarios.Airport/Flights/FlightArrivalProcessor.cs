using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightArrivalProcessor
{
    private static readonly TimeSpan ApproachDuration =
        TimeSpan.FromMinutes(10);

    private readonly ISimulationClock _clock;

    public FlightArrivalProcessor(ISimulationClock clock)
    {
        _clock = clock;
    }

    public void Process(Flight flight)
    {
        if (flight.Type != FlightType.Arrival)
        {
            return;
        }

        if (flight.Status == FlightStatus.Scheduled &&
            _clock.CurrentTime >= flight.ScheduledTime)
        {
            flight.BeginApproach(_clock.CurrentTime);
            return;
        }

        if (flight.Status == FlightStatus.Approaching &&
            flight.ApproachStartedAt.HasValue &&
            _clock.CurrentTime >=
            flight.ApproachStartedAt.Value + ApproachDuration)
        {
            flight.Land(_clock.CurrentTime);
        }
    }
}