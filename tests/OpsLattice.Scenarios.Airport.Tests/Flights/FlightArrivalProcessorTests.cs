using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightArrivalProcessorTests
{
    [Fact]
    public void Process_BeforeScheduledTime_ShouldRemainScheduled()
    {
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 14, 30, 0, TimeSpan.Zero);

        var clock = new SimulationClock(
            scheduledTime.AddMinutes(-10));

        var flight = CreateFlight(scheduledTime);
        var processor = new FlightArrivalProcessor(clock);

        processor.Process(flight);

        Assert.Equal(FlightStatus.Scheduled, flight.Status);
    }

    [Fact]
    public void Process_AtScheduledTime_ShouldBeginApproach()
    {
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 14, 30, 0, TimeSpan.Zero);

        var clock = new SimulationClock(scheduledTime);

        var flight = CreateFlight(scheduledTime);
        var processor = new FlightArrivalProcessor(clock);

        processor.Process(flight);

        Assert.Equal(FlightStatus.Approaching, flight.Status);
    }

    private static Flight CreateFlight(DateTimeOffset scheduledTime)
    {
        return new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };
    }
}
