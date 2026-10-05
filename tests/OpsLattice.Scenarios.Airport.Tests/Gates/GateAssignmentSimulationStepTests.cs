using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateAssignmentSimulationStepTests
{
    [Fact]
    public void Advance_WhenFlightEntersAssignmentHorizon_ShouldAssignGate()
    {
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 15, 30, 0, TimeSpan.Zero);

        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };

        var flightProvider =
            new TestFlightProvider([flight]);

        var gateProvider =
            new InMemoryGateProvider();

        // Flight is initially 70 minutes away.
        var clock = new SimulationClock(
            scheduledTime.AddMinutes(-70));

        var processor = new GateAssignmentProcessor(
            clock,
            gateProvider);

        var step = new GateAssignmentSimulationStep(
            flightProvider,
            processor);

        var runner = new SimulationRunner(
            clock,
            [step]);

        Assert.DoesNotContain(
            gateProvider.GetGates(),
            gate => gate.AssignedFlight == flight);

        // Flight is now exactly 60 minutes away.
        runner.Advance(TimeSpan.FromMinutes(10));

        Assert.Contains(
            gateProvider.GetGates(),
            gate => gate.AssignedFlight == flight);
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