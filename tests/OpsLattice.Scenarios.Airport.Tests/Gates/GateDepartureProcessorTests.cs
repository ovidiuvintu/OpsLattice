using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateDepartureProcessorTests
{
    [Fact]
    public void Process_BeforeTurnaroundCompletes_ShouldNotDepart()
    {
        var flight = CreateFlightAtGate();

        var gateProvider = new InMemoryGateProvider();

        var gate = gateProvider
            .GetGates()
            .First();

        gate.Assign(flight);
        gate.Occupy(flight);

        var clock = new SimulationClock(
            flight.ArrivedAtGateAt!.Value.AddMinutes(29));

        var processor = new GateDepartureProcessor(
            clock,
            gateProvider);

        processor.Process(flight);

        Assert.Equal(
            FlightStatus.AtGate,
            flight.Status);

        Assert.True(gate.IsAssigned);
        Assert.True(gate.IsOccupied);
        Assert.Same(flight, gate.AssignedFlight);
        Assert.Same(flight, gate.OccupyingFlight);
    }

    [Fact]
    public void Process_WhenTurnaroundCompletes_ShouldDepartAndReleaseGate()
    {
        var flight = CreateFlightAtGate();

        var gateProvider = new InMemoryGateProvider();

        var gate = gateProvider
            .GetGates()
            .First();

        gate.Assign(flight);
        gate.Occupy(flight);

        var clock = new SimulationClock(
            flight.ArrivedAtGateAt!.Value.AddMinutes(30));

        var processor = new GateDepartureProcessor(
            clock,
            gateProvider);

        processor.Process(flight);

        Assert.Equal(
            FlightStatus.Departed,
            flight.Status);

        Assert.False(gate.IsAssigned);
        Assert.False(gate.IsOccupied);
        Assert.Null(gate.AssignedFlight);
        Assert.Null(gate.OccupyingFlight);
    }

    private static Flight CreateFlightAtGate()
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

        flight.BeginApproach(
            flight.ScheduledTime);

        flight.Land(
            flight.ScheduledTime.AddMinutes(10));

        flight.ArriveAtGate(
            flight.ScheduledTime.AddMinutes(20));

        return flight;
    }
}