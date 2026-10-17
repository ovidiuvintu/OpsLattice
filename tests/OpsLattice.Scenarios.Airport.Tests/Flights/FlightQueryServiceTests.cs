using Moq;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightQueryServiceTests
{
    [Fact]
    public void GetFlightStatuses_WhenFlightHasAssignedGate_ReturnsGateCode()
    {
        // Arrange
        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 6, 14, 30, 0, TimeSpan.Zero),
            Type = FlightType.Arrival
        };

        var gate = new Gate("B10");
        gate.Assign(flight);

        var flightProvider = new Mock<IFlightProvider>();

        flightProvider
            .Setup(x => x.GetFlights())
            .Returns([flight]);

        var gateProvider = new Mock<IGateProvider>();

        gateProvider
            .Setup(x => x.GetGates())
            .Returns([gate]);

        var service = new FlightQueryService(
            flightProvider.Object,
            gateProvider.Object);

        // Act
        var result = Assert.Single(
            service.GetFlightStatuses());

        // Assert
        Assert.Equal("UA123", result.FlightNumber);
        Assert.Equal("Scheduled", result.Status);
        Assert.Equal("B10", result.AssignedGate);
        Assert.Null(result.OccupiedGate);
    }
}
