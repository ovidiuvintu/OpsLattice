using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateTests
{
    [Fact]
    public void Constructor_WithValidCode_ShouldCreateGate()
    {
        var gate = new Gate("B12");

        Assert.Equal("B12", gate.Code);
    }

    [Fact]
    public void Constructor_WithEmptyCode_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => new Gate(""));
    }

    [Fact]
    public void Constructor_WithWhitespaceCode_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => new Gate("   "));
    }

    [Fact]
    public void Assign_WhenGateIsUnassigned_ShouldAssignFlight()
    {
        var gate = new Gate("B12");
        var flight = CreateFlight();

        gate.Assign(flight);

        Assert.True(gate.IsAssigned);
        Assert.Same(flight, gate.AssignedFlight);
    }

    [Fact]
    public void Assign_WhenGateIsAlreadyAssigned_ShouldThrow()
    {
        var gate = new Gate("B12");

        var firstFlight = CreateFlight();

        var secondFlight = new Flight
        {
            FlightNumber = "DL456",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero)
        };

        gate.Assign(firstFlight);

        Assert.Throws<InvalidOperationException>(
            () => gate.Assign(secondFlight));
    }

    private static Flight CreateLandedFlight()
    {
        var flight = CreateFlight();

        flight.BeginApproach(flight.ScheduledTime);
        flight.Land();

        return flight;
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
