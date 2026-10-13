using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class InMemoryGateProviderTests
{
    [Fact]
    public void GetGates_ShouldReturnGates()
    {
        var provider = new InMemoryGateProvider();

        var gates = provider.GetGates();

        Assert.NotEmpty(gates);
    }

    [Fact]
    public void GetGates_ShouldReturnExpectedGates()
    {
        var provider = new InMemoryGateProvider();

        var gates = provider.GetGates();

        Assert.Contains(gates, gate => gate.Code == "B10");
        Assert.Contains(gates, gate => gate.Code == "B11");
        Assert.Contains(gates, gate => gate.Code == "B12");
    }

    [Fact]
    public void GetGates_ShouldReturnUnassignedGates()
    {
        var provider = new InMemoryGateProvider();

        var gates = provider.GetGates();

        Assert.All(
            gates,
            gate => Assert.False(gate.IsAssigned));
    }

    [Fact]
    public void WaitingArrival_ShouldOccupyGate_WhenGateIsReleased()
    {
        // Arrange
        var startTime = new DateTimeOffset(
            2026, 10, 6, 14, 0, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var gate = new Gate("B10");
        var gateProvider = new InMemoryGateProvider([gate]);

        // Flight A has already arrived at the gate.
        var flightA = CreateArrival(
            "DL100",
            startTime.AddMinutes(-20));

        flightA.BeginApproach(startTime.AddMinutes(-20));
        flightA.Land(startTime.AddMinutes(-10));

        gate.Assign(flightA);
        gate.Occupy(flightA);
        flightA.ArriveAtGate(startTime);

        // Flight B has landed but has no gate.
        var flightB = CreateArrival(
            "DL200",
            startTime.AddMinutes(-15));

        flightB.BeginApproach(startTime.AddMinutes(-15));
        flightB.Land(startTime.AddMinutes(-5));

        var flightProvider = new InMemoryFlightProvider(
            [flightA, flightB]);

        var departureStep = new GateDepartureSimulationStep(
            flightProvider,
            new GateDepartureProcessor(clock, gateProvider));

        var assignmentStep = new GateAssignmentSimulationStep(
            flightProvider,
            new GateAssignmentProcessor(clock, gateProvider));

        var occupancyStep = new GateOccupancySimulationStep(
            flightProvider,
            new GateOccupancyProcessor(clock, gateProvider));

        var runner = new SimulationRunner(
            clock,
            [
                departureStep,
                assignmentStep,
                occupancyStep
            ]);

        // Act: Flight A completes its 30-minute turnaround.
        runner.Advance(TimeSpan.FromMinutes(30));

        // Assert
        Assert.Equal(
            startTime.AddMinutes(30),
            clock.CurrentTime);

        Assert.Equal(
            FlightStatus.Departed,
            flightA.Status);

        Assert.Equal(
            FlightStatus.AtGate,
            flightB.Status);

        Assert.Same(flightB, gate.AssignedFlight);
        Assert.Same(flightB, gate.OccupyingFlight);
    }

    private static Flight CreateArrival(
        string flightNumber,
        DateTimeOffset scheduledTime)
    {
        return new Flight
        {
            FlightNumber = flightNumber,
            Airline = "Delta",
            Origin = "ORD",
            Destination = "ATL",
            Type = FlightType.Arrival,
            ScheduledTime = scheduledTime
        };
    }
}