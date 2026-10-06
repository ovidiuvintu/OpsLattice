using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateDepartureSimulationStepTests
{
    [Fact]
    public void Execute_WhenFlightIsReady_ShouldDepartAndReleaseGate()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 20, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var flight = CreateFlight();

        flight.BeginApproach(startTime);
        flight.Land(startTime.AddMinutes(10));

        var gateProvider = new InMemoryGateProvider();

        var gate = gateProvider
            .GetGates()
            .First();

        gate.Assign(flight);
        gate.Occupy(flight);

        flight.ArriveAtGate(
            startTime.AddMinutes(20));

        clock.Advance(
            TimeSpan.FromMinutes(50));

        var flightProvider =
            new TestFlightProvider([flight]);

        var processor =
            new GateDepartureProcessor(
                clock,
                gateProvider);

        var step =
            new GateDepartureSimulationStep(
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
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 20, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var firstFlight =
            CreateFlight("UA123");

        var secondFlight =
            CreateFlight("DL456");

        firstFlight.BeginApproach(startTime);
        firstFlight.Land(startTime.AddMinutes(10));
        firstFlight.ArriveAtGate(
            startTime.AddMinutes(20));

        secondFlight.BeginApproach(startTime);
        secondFlight.Land(startTime.AddMinutes(10));
        secondFlight.ArriveAtGate(
            startTime.AddMinutes(20));

        var gateProvider =
            new InMemoryGateProvider();

        var gates =
            gateProvider.GetGates().ToArray();

        gates[0].Assign(firstFlight);
        gates[0].Occupy(firstFlight);

        gates[1].Assign(secondFlight);
        gates[1].Occupy(secondFlight);

        clock.Advance(
            TimeSpan.FromMinutes(50));

        var flightProvider =
            new TestFlightProvider(
                [firstFlight, secondFlight]);

        var processor =
            new GateDepartureProcessor(
                clock,
                gateProvider);

        var step =
            new GateDepartureSimulationStep(
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

    [Fact]
    public void GetNextExecutionTime_WhenFlightIsAtGate_ShouldReturnTurnaroundCompletionTime()
    {
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 15, 15, 0, TimeSpan.Zero);

        var landedAt = new DateTimeOffset(
            2026, 10, 4, 15, 25, 0, TimeSpan.Zero);

        var arrivedAtGateAt = new DateTimeOffset(
            2026, 10, 4, 15, 35, 0, TimeSpan.Zero);

        var flight = new Flight
        {
            FlightNumber = "DL456",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };

        flight.BeginApproach(scheduledTime);
        flight.Land(landedAt);
        flight.ArriveAtGate(arrivedAtGateAt);

        var flightProvider =
            new TestFlightProvider([flight]);

        var gateProvider =
            new InMemoryGateProvider();

        var clock =
            new SimulationClock(arrivedAtGateAt);

        var processor =
            new GateDepartureProcessor(
                clock,
                gateProvider);

        var step =
            new GateDepartureSimulationStep(
                flightProvider,
                processor);

        var targetTime =
            new DateTimeOffset(
                2026, 10, 4, 16, 30, 0, TimeSpan.Zero);

        var nextExecutionTime =
            step.GetNextExecutionTime(
                clock.CurrentTime,
                targetTime);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 16, 5, 0, TimeSpan.Zero),
            nextExecutionTime);
    }

    private static Flight CreateFlight(
        string flightNumber = "UA123")
    {
        return new Flight
        {
            FlightNumber = flightNumber,
            Airline = "Test Airline",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = new DateTimeOffset(
                2026, 10, 4, 14, 30, 0, TimeSpan.Zero)
        };
    }

    private sealed class TestFlightProvider
        : IFlightProvider
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