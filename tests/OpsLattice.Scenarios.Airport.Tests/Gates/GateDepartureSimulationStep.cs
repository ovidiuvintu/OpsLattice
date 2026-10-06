using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateDepartureSimulationStepTests
{
    [Fact]
    public void Execute_WhenFlightIsReady_ShouldDepartAndReleaseGate()
    {
        var flight = CreateFlightAtGate();

        var flightProvider =
            new TestFlightProvider([flight]);

        var gateProvider =
            new InMemoryGateProvider();

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

        var step = new GateDepartureSimulationStep(
            flightProvider,
            processor);

        step.Execute();

        Assert.Equal(
            FlightStatus.Departed,
            flight.Status);

        Assert.False(gate.IsAssigned);
        Assert.False(gate.IsOccupied);

        Assert.Null(gate.AssignedFlight);
        Assert.Null(gate.OccupyingFlight);
    }

    [Fact]
    public void Execute_WhenMultipleFlightsAreReady_ShouldProcessAllFlights()
    {
        var firstFlight = CreateFlightAtGate(
            "UA123",
            new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero));

        var secondFlight = CreateFlightAtGate(
            "DL456",
            new DateTimeOffset(
                2026, 10, 4, 14, 35, 0, TimeSpan.Zero));

        var flightProvider =
            new TestFlightProvider(
                [firstFlight, secondFlight]);

        var gateProvider =
            new InMemoryGateProvider();

        var gates = gateProvider
            .GetGates()
            .Take(2)
            .ToArray();

        gates[0].Assign(firstFlight);
        gates[0].Occupy(firstFlight);

        gates[1].Assign(secondFlight);
        gates[1].Occupy(secondFlight);

        var clock = new SimulationClock(
            secondFlight.ArrivedAtGateAt!.Value.AddMinutes(30));

        var processor = new GateDepartureProcessor(
            clock,
            gateProvider);

        var step = new GateDepartureSimulationStep(
            flightProvider,
            processor);

        step.Execute();

        Assert.Equal(
            FlightStatus.Departed,
            firstFlight.Status);

        Assert.Equal(
            FlightStatus.Departed,
            secondFlight.Status);

        Assert.False(gates[0].IsAssigned);
        Assert.False(gates[0].IsOccupied);

        Assert.False(gates[1].IsAssigned);
        Assert.False(gates[1].IsOccupied);
    }

    private static Flight CreateFlightAtGate()
    {
        return CreateFlightAtGate(
            "UA123",
            new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero));
    }

    private static Flight CreateFlightAtGate(
        string flightNumber,
        DateTimeOffset scheduledTime)
    {
        var flight = new Flight
        {
            FlightNumber = flightNumber,
            Airline = "Test Airline",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };

        flight.BeginApproach(
            flight.ScheduledTime);

        flight.Land(
            flight.ScheduledTime.AddMinutes(10));

        flight.ArriveAtGate(
            flight.ScheduledTime.AddMinutes(20));

        return flight;
    }

    private sealed class TestFlightProvider : IFlightProvider
    {
        private readonly IReadOnlyCollection<Flight> _flights;

        public TestFlightProvider(
            IReadOnlyCollection<Flight> flights)
        {
            _flights = flights;
        }

        public IReadOnlyCollection<Flight> GetFlights()
        {
            return _flights;
        }
    }
}