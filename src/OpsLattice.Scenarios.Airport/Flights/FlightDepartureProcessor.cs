using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightDepartureProcessor
{
    private static readonly TimeSpan BoardingLeadTime =
        TimeSpan.FromMinutes(40);

    private static readonly TimeSpan BoardingCompletionLeadTime =
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
                flight.ScheduledTime - BoardingLeadTime,

            FlightStatus.Boarding =>
                flight.ScheduledTime - BoardingCompletionLeadTime,

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
                     flight.ScheduledTime - BoardingLeadTime:

                flight.BeginBoarding();
                break;

            case FlightStatus.Boarding
                when _clock.CurrentTime >=
                     flight.ScheduledTime - BoardingCompletionLeadTime:

                flight.CompleteBoarding();
                break;

            case FlightStatus.ReadyForDeparture
                when _clock.CurrentTime >= flight.ScheduledTime:

                flight.Depart();
                break;
        }
    }
}
