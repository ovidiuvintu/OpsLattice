using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightArrivalProcessor
{
    private readonly ISimulationClock _clock;

    public FlightArrivalProcessor(ISimulationClock clock)
    {
        _clock = clock;
    }

    public void Process(Flight flight)
    {
        if (flight.Status == FlightStatus.Scheduled &&
            _clock.CurrentTime >= flight.ScheduledTime)
        {
            flight.BeginApproach();
        }
    }
}