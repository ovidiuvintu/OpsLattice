using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateCapacityIntegrationTests
{
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

    [Fact]
    public void WaitingArrivals_ShouldReceiveGates_InLandingOrder()
    {
        // Arrange
        var startTime = new DateTimeOffset(
            2026, 10, 6, 14, 0, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var gate = new Gate("B10");
        var gateProvider = new InMemoryGateProvider([gate]);

        // Flight A occupies the only available gate.
        var flightA = CreateArrival(
            "DL100", startTime.AddMinutes(-30));

        flightA.BeginApproach(startTime.AddMinutes(-30));
        flightA.Land(startTime.AddMinutes(-20));

        gate.Assign(flightA);
        gate.Occupy(flightA);
        flightA.ArriveAtGate(startTime);

        // Flight B landed at 13:45.
        var flightB = CreateArrival(
            "DL200", startTime.AddMinutes(-25));

        flightB.BeginApproach(startTime.AddMinutes(-25));
        flightB.Land(startTime.AddMinutes(-15));

        // Flight C landed at 13:50.
        var flightC = CreateArrival(
            "UA300", startTime.AddMinutes(-20));

        flightC.BeginApproach(startTime.AddMinutes(-20));
        flightC.Land(startTime.AddMinutes(-10));

        // Intentionally put Flight C before Flight B.
        var flightProvider = new InMemoryFlightProvider(
            [flightA, flightC, flightB]);

        var runner = new SimulationRunner(
            clock,
            [
                new GateDepartureSimulationStep(
                flightProvider,
                new GateDepartureProcessor(clock, gateProvider)),

            new GateAssignmentSimulationStep(
                flightProvider,
                new GateAssignmentProcessor(clock, gateProvider)),

            new GateOccupancySimulationStep(
                flightProvider,
                new GateOccupancyProcessor(clock, gateProvider))
            ]);

        // Act: Flight A releases B10 at 14:30.
        runner.Advance(TimeSpan.FromMinutes(30));

        // Assert: Flight B must receive B10.
        Assert.Equal(FlightStatus.Departed, flightA.Status);
        Assert.Equal(FlightStatus.AtGate, flightB.Status);
        Assert.Equal(FlightStatus.Landed, flightC.Status);

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