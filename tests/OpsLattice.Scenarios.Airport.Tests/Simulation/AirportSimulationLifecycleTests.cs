using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Simulation;

public sealed class AirportSimulationLifecycleTests
{
    [Fact]
    public void Advance_ShouldProcessCompleteFlightLifecycleAtCorrectEventTimes()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 15, 0, 0, TimeSpan.Zero);

        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 15, 15, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var flight = new Flight
        {
            FlightNumber = "DL456",
            Airline = "Delta Air Lines",
            Origin = "ATL",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };

        var flightProvider =
            new TestFlightProvider([flight]);

        var gateProvider =
            new InMemoryGateProvider();

        var gateAssignmentProcessor =
            new GateAssignmentProcessor(
                clock,
                gateProvider);

        var flightArrivalProcessor =
            new FlightArrivalProcessor(clock);

        var gateOccupancyProcessor =
            new GateOccupancyProcessor(
                clock,
                gateProvider);

        var gateDepartureProcessor =
            new GateDepartureProcessor(
                clock,
                gateProvider);

        var gateAssignmentStep =
            new GateAssignmentSimulationStep(
                flightProvider,
                gateAssignmentProcessor);

        var flightArrivalStep =
            new FlightArrivalSimulationStep(
                flightProvider,
                flightArrivalProcessor);

        var gateOccupancyStep =
            new GateOccupancySimulationStep(
                flightProvider,
                gateOccupancyProcessor);

        var gateDepartureStep =
            new GateDepartureSimulationStep(
                flightProvider,
                gateDepartureProcessor);

        var runner = new SimulationRunner(
            clock,
            [
                gateAssignmentStep,
                flightArrivalStep,
                gateOccupancyStep,
                gateDepartureStep
            ]);

        runner.Advance(
            TimeSpan.FromMinutes(70));

        Assert.Equal(
            FlightStatus.Departed,
            flight.Status);

        Assert.Equal(
            scheduledTime,
            flight.ApproachStartedAt);

        Assert.Equal(
            scheduledTime.AddMinutes(10),
            flight.LandedAt);

        Assert.Equal(
            scheduledTime.AddMinutes(20),
            flight.ArrivedAtGateAt);

        Assert.Equal(
            startTime.AddMinutes(70),
            clock.CurrentTime);

        var gate = gateProvider
            .GetGates()
            .First();

        Assert.False(gate.IsAssigned);
        Assert.False(gate.IsOccupied);
        Assert.Null(gate.AssignedFlight);
        Assert.Null(gate.OccupyingFlight);
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