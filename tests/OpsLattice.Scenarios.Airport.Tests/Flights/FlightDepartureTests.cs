using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightDepartureTests
{
    private static Flight CreateDeparture()
    {
        return new Flight
        {
            FlightNumber = "DL100",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "JFK",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 6, 14, 30, 0, TimeSpan.Zero),
            Type = FlightType.Departure
        };
    }

    [Fact]
    public void BeginBoarding_ShouldChangeStatusToBoarding()
    {
        var flight = CreateDeparture();

        flight.BeginBoarding();

        Assert.Equal(
            FlightStatus.Boarding,
            flight.Status);
    }

    [Fact]
    public void CompleteBoarding_ShouldChangeStatusToReady()
    {
        var flight = CreateDeparture();

        flight.BeginBoarding();
        flight.CompleteBoarding();

        Assert.Equal(
            FlightStatus.ReadyForDeparture,
            flight.Status);
    }

    [Fact]
    public void Depart_ShouldChangeStatusToDeparted()
    {
        var flight = CreateDeparture();

        flight.BeginBoarding();
        flight.CompleteBoarding();
        flight.Depart();

        Assert.Equal(
            FlightStatus.Departed,
            flight.Status);
    }

    [Fact]
    public void CannotDepartBeforeBoarding()
    {
        var flight = CreateDeparture();

        Assert.Throws<InvalidOperationException>(
            () => flight.Depart());
    }

    [Fact]
    public void CannotCompleteBoardingBeforeItStarts()
    {
        var flight = CreateDeparture();

        Assert.Throws<InvalidOperationException>(
            () => flight.CompleteBoarding());
    }
}