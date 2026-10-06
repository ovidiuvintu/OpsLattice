using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightTests
{
    [Fact]
    public void NewFlight_ShouldHaveScheduledStatus()
    {
        // Arrange
        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero)
        };

        // Assert
        Assert.Equal(FlightStatus.Scheduled, flight.Status);
    }

    [Fact]
    public void Flight_ShouldRetainProvidedInformation()
    {
        // Arrange
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 14, 30, 0, TimeSpan.Zero);

        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };

        // Assert
        Assert.Equal("UA123", flight.FlightNumber);
        Assert.Equal("United Airlines", flight.Airline);
        Assert.Equal("ORD", flight.Origin);
        Assert.Equal("IAH", flight.Destination);
        Assert.Equal(scheduledTime, flight.ScheduledTime);
    }

    [Fact]
    public void BeginApproach_ShouldChangeStatusToApproaching()
    {
        var flight = CreateFlight();

        flight.BeginApproach(flight.ScheduledTime);

        Assert.Equal(FlightStatus.Approaching, flight.Status);
    }

    [Fact]
    public void BeginApproach_ShouldRecordApproachStartTime()
    {
        var flight = CreateFlight();

        var approachTime = flight.ScheduledTime;

        flight.BeginApproach(approachTime);

        Assert.Equal(approachTime, flight.ApproachStartedAt);
    }

    [Fact]
    public void Land_WhenApproaching_ShouldChangeStatusToLanded()
    {
        var flight = CreateFlight();

        flight.BeginApproach(flight.ScheduledTime);

        var landedAt =
            flight.ScheduledTime.AddMinutes(10);

        flight.Land(landedAt);

        Assert.Equal(FlightStatus.Landed, flight.Status);
    }

    [Fact]
    public void Land_WhenNotApproaching_ShouldThrow()
    {
        var flight = CreateFlight();

        var landedAt =
            flight.ScheduledTime.AddMinutes(10);

        var action = () => flight.Land(landedAt);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Land_WhenApproaching_ShouldRecordLandingTime()
    {
        var flight = CreateFlight();

        flight.BeginApproach(flight.ScheduledTime);

        var landedAt =
            flight.ScheduledTime.AddMinutes(10);

        flight.Land(landedAt);

        Assert.Equal(landedAt, flight.LandedAt);
        Assert.Equal(FlightStatus.Landed, flight.Status);
    }

    [Fact]
    public void Depart_WhenAtGate_ShouldChangeStatusToDeparted()
    {
        var flight = CreateFlight();

        flight.BeginApproach(flight.ScheduledTime);
        flight.Land(flight.ScheduledTime.AddMinutes(10));
        flight.ArriveAtGate(flight.ScheduledTime.AddMinutes(20));

        flight.Depart();

        Assert.Equal(
            FlightStatus.Departed,
            flight.Status);
    }

    [Fact]
    public void Depart_WhenNotAtGate_ShouldThrow()
    {
        var flight = CreateFlight();

        Assert.Throws<InvalidOperationException>(
            () => flight.Depart());
    }

    private static Flight CreateFlight()
    {
        return new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero)
        };
    }
}
