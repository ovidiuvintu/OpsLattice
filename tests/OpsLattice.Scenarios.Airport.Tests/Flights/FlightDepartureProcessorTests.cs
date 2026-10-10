using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightDepartureProcessorTests
{
    private static readonly DateTimeOffset DepartureTime =
        new(2026, 10, 6, 15, 0, 0, TimeSpan.Zero);

    private static Flight CreateDeparture()
    {
        return new Flight
        {
            FlightNumber = "DL100",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "JFK",
            ScheduledTime = DepartureTime,
            Type = FlightType.Departure
        };
    }

    [Fact]
    public void ScheduledFlight_ShouldBeginBoarding40MinutesBeforeDeparture()
    {
        var clock = new SimulationClock(
            DepartureTime.AddMinutes(-40));

        var processor = new FlightDepartureProcessor(clock);
        var flight = CreateDeparture();

        processor.Process(flight);

        Assert.Equal(FlightStatus.Boarding, flight.Status);
    }

    [Fact]
    public void BoardingFlight_ShouldBecomeReady10MinutesBeforeDeparture()
    {
        var clock = new SimulationClock(
            DepartureTime.AddMinutes(-40));

        var processor = new FlightDepartureProcessor(clock);
        var flight = CreateDeparture();

        processor.Process(flight);
        clock.Advance(TimeSpan.FromMinutes(30));
        processor.Process(flight);

        Assert.Equal(
            FlightStatus.ReadyForDeparture,
            flight.Status);
    }

    [Fact]
    public void ReadyFlight_ShouldDepartAtScheduledTime()
    {
        var clock = new SimulationClock(
            DepartureTime.AddMinutes(-40));

        var processor = new FlightDepartureProcessor(clock);
        var flight = CreateDeparture();

        processor.Process(flight);

        clock.Advance(TimeSpan.FromMinutes(30));
        processor.Process(flight);

        clock.Advance(TimeSpan.FromMinutes(10));
        processor.Process(flight);

        Assert.Equal(
            FlightStatus.Departed,
            flight.Status);
    }

    [Fact]
    public void ScheduledFlight_ShouldReturnBoardingTime()
    {
        var clock = new SimulationClock(
            DepartureTime.AddHours(-1));

        var processor = new FlightDepartureProcessor(clock);
        var flight = CreateDeparture();

        Assert.Equal(
            DepartureTime.AddMinutes(-40),
            processor.GetNextExecutionTime(flight));
    }

    [Fact]
    public void ArrivalFlight_ShouldNotBeProcessed()
    {
        var clock = new SimulationClock(
            DepartureTime);

        var processor = new FlightDepartureProcessor(clock);

        var flight = new Flight
        {
            FlightNumber = "DL200",
            Airline = "Delta Air Lines",
            Origin = "JFK",
            Destination = "ATL",
            ScheduledTime = DepartureTime,
            Type = FlightType.Arrival
        };

        processor.Process(flight);

        Assert.Equal(
            FlightStatus.Scheduled,
            flight.Status);

        Assert.Null(
            processor.GetNextExecutionTime(flight));
    }
}