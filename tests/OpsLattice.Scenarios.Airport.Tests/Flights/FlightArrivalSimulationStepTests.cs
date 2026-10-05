using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightArrivalSimulationStepTests
{
    [Fact]
    public void Advance_WhenFlightReachesScheduledTime_ShouldBeginApproach()
    {
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 14, 30, 0, TimeSpan.Zero);

        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = scheduledTime
        };

        var provider = new TestFlightProvider([flight]);

        var clock = new SimulationClock(
            scheduledTime.AddMinutes(-10));

        var processor = new FlightArrivalProcessor(clock);

        var step = new FlightArrivalSimulationStep(
            provider,
            processor);

        var runner = new SimulationRunner(
            clock,
            [step]);

        runner.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(
            FlightStatus.Approaching,
            flight.Status);
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