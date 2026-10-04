using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class InMemoryFlightProviderTests
{
    [Fact]
    public void GetFlights_ShouldReturnFlights()
    {
        var provider = new InMemoryFlightProvider();

        var flights = provider.GetFlights();

        Assert.NotEmpty(flights);
    }

    [Fact]
    public void GetFlights_ShouldReturnScheduledFlights()
    {
        var provider = new InMemoryFlightProvider();

        var flights = provider.GetFlights();

        Assert.All(
            flights,
            flight => Assert.Equal(
                FlightStatus.Scheduled,
                flight.Status));
    }
}