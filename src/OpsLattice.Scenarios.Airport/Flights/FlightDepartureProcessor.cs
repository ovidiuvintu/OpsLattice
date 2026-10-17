using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightDepartureProcessor
{
    private static readonly TimeSpan _boardingLeadTime =
        TimeSpan.FromMinutes(40);

    private static readonly TimeSpan _boardingCompletionLeadTime =
        TimeSpan.FromMinutes(10);

    private readonly ISimulationClock _clock;

    public FlightDepartureProcessor(ISimulationClock clock)
    {
        _clock = clock;
    }

    public DateTimeOffset? GetNextExecutionTime(Flight flight)
    {
        if (flight.Type != FlightType.Departure)
        {
            return null;
        }

        return flight.Status switch
        {
            FlightStatus.Scheduled =>
                flight.ScheduledTime - _boardingLeadTime,

            FlightStatus.Boarding =>
                flight.ScheduledTime - _boardingCompletionLeadTime,

            FlightStatus.ReadyForDeparture =>
                flight.ScheduledTime,

            _ => null
        };
    }

    public void Process(Flight flight)
    {
        if (flight.Type != FlightType.Departure)
        {
            return;
        }

        switch (flight.Status)
        {
            case FlightStatus.Scheduled
                when _clock.CurrentTime >=
                     flight.ScheduledTime - _boardingLeadTime:

                flight.BeginBoarding();
                break;

            case FlightStatus.Boarding
                when _clock.CurrentTime >=
                     flight.ScheduledTime - _boardingCompletionLeadTime:

                flight.CompleteBoarding();
                break;

            case FlightStatus.ReadyForDeparture
                when _clock.CurrentTime >= flight.ScheduledTime:

                flight.Depart();
                break;
        }
    }
}
