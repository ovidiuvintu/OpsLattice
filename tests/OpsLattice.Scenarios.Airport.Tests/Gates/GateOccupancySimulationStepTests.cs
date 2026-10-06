using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateOccupancySimulationStepTests
{
    [Fact]
    public void Execute_WhenFlightIsReady_ShouldOccupyAssignedGate()
    {
        var flight = CreateLandedFlight();

        var flightProvider =
            new TestFlightProvider([flight]);

        var gateProvider =
            new InMemoryGateProvider();

        var gate = gateProvider
            .GetGates()
            .First();

        gate.Assign(flight);

        var clock = new SimulationClock(
            flight.LandedAt!.Value.AddMinutes(10));

        var processor = new GateOccupancyProcessor(
            clock,
            gateProvider);

        var step = new GateOccupancySimulationStep(
            flightProvider,
            processor);

        step.Execute();

        Assert.Equal(
            FlightStatus.AtGate,
            flight.Status);

        Assert.True(gate.IsOccupied);

        Assert.Same(
            flight,
            gate.OccupyingFlight);

        Assert.Equal(
            clock.CurrentTime,
            flight.ArrivedAtGateAt);
    }

    [Fact]
    public void Execute_WhenMultipleFlightsAreReady_ShouldProcessAllFlights()
    {
        var firstFlight = CreateLandedFlight(
            "UA123",
            new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero));

        var secondFlight = CreateLandedFlight(
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
        gates[1].Assign(secondFlight);

        var clock = new SimulationClock(
            secondFlight.LandedAt!.Value.AddMinutes(10));

        var processor = new GateOccupancyProcessor(
            clock,
            gateProvider);

        var step = new GateOccupancySimulationStep(
            flightProvider,
            processor);

        step.Execute();

        Assert.Equal(
            FlightStatus.AtGate,
            firstFlight.Status);

        Assert.Equal(
            FlightStatus.AtGate,
            secondFlight.Status);

        Assert.Same(
            firstFlight,
            gates[0].OccupyingFlight);

        Assert.Same(
            secondFlight,
            gates[1].OccupyingFlight);
    }

    private static Flight CreateLandedFlight()
    {
        return CreateLandedFlight(
            "UA123",
            new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero));
    }

    private static Flight CreateLandedFlight(
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