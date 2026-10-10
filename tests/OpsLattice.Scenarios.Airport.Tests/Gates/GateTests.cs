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
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
            Type = FlightType.Arrival
        };

        gate.Assign(firstFlight);

        Assert.Throws<InvalidOperationException>(
            () => gate.Assign(secondFlight));
    }

    [Fact]
    public void Release_WhenFlightOccupiesGate_ShouldClearGate()
    {
        var gate = new Gate("B12");
        var flight = CreateFlight();

        gate.Assign(flight);
        gate.Occupy(flight);

        gate.Release(flight);

        Assert.False(gate.IsAssigned);
        Assert.False(gate.IsOccupied);
        Assert.Null(gate.AssignedFlight);
        Assert.Null(gate.OccupyingFlight);
    }

    [Fact]
    public void Release_WhenDifferentFlightOccupiesGate_ShouldThrow()
    {
        var gate = new Gate("B12");

        var assignedFlight = CreateFlight();

        var otherFlight = new Flight
        {
            FlightNumber = "DL456",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
            Type = FlightType.Arrival
        };

        gate.Assign(assignedFlight);
        gate.Occupy(assignedFlight);

        Assert.Throws<InvalidOperationException>(
            () => gate.Release(otherFlight));
    }

    private static Flight CreateLandedFlight()
    {
        var flight = CreateFlight();

        flight.BeginApproach(flight.ScheduledTime);
        flight.Land(flight.ScheduledTime.AddMinutes(10));

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
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero),
            Type = FlightType.Arrival
        };
    }
}
