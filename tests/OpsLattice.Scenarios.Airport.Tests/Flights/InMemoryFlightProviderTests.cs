using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class InMemoryFlightProviderTests
{
    [Fact]
    public void GetFlights_ShouldReturnFlights()
    {
        var provider = new InMemoryFlightProvider(new[]
        {
            new Flight
            {
                FlightNumber = "DL456",
                Airline = "Delta Air Lines",
                Origin = "ATL",
                Destination = "IAH",
                ScheduledTime = new DateTimeOffset(
                    2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
                Type = FlightType.Arrival
            }
        });

        var flights = provider.GetFlights();

        Assert.NotEmpty(flights);
    }

    [Fact]
    public void GetFlights_ShouldReturnScheduledFlights()
    {
        var provider = new InMemoryFlightProvider(new[]
        {
            new Flight
            {
                FlightNumber = "DL456",
                Airline = "Delta Air Lines",
                Origin = "ATL",
                Destination = "IAH",
                ScheduledTime = new DateTimeOffset(
                    2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
                Type = FlightType.Arrival
            }
        });

        var flights = provider.GetFlights();

        Assert.All(
            flights,
            flight => Assert.Equal(
                FlightStatus.Scheduled,
                flight.Status));
    }
}