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
}
