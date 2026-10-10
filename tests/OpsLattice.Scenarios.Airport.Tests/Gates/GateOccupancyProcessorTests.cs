using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateOccupancyProcessorTests
{
    [Fact]
    public void Process_WhenLandedFlightHasAssignedGate_ShouldOccupyGate()
    {
        var flight = CreateLandedFlight();

        var gateProvider = new InMemoryGateProvider();

        var gate = gateProvider
            .GetGates()
            .First();

        gate.Assign(flight);

        var clock = new SimulationClock(
            flight.LandedAt!.Value.AddMinutes(10));

        var processor =
            new GateOccupancyProcessor(
                clock,
                gateProvider);

        processor.Process(flight);

        Assert.True(gate.IsOccupied);
        Assert.Same(flight, gate.OccupyingFlight);
        Assert.Equal(
            FlightStatus.AtGate,
            flight.Status);

        Assert.Equal(
            clock.CurrentTime,
            flight.ArrivedAtGateAt);
    }

    private static Flight CreateLandedFlight()
    {
        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero),
            Type = FlightType.Arrival
        };

        flight.BeginApproach(flight.ScheduledTime);

        flight.Land(
            flight.ScheduledTime.AddMinutes(10));

        return flight;
    }
}