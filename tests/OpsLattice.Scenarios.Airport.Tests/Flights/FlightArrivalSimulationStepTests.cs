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
            ScheduledTime = scheduledTime,
            Type = FlightType.Arrival
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

    [Fact]
    public void Advance_AfterApproachDuration_ShouldLandFlight()
    {
        var scheduledTime = new DateTimeOffset(
            2026, 10, 4, 14, 30, 0, TimeSpan.Zero);

        var flight = new Flight
        {
            FlightNumber = "UA123",
            Airline = "United Airlines",
            Origin = "ORD",
            Destination = "IAH",
            ScheduledTime = scheduledTime,
            Type = FlightType.Arrival
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

        // 14:20 → 14:30
        runner.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(
            FlightStatus.Approaching,
            flight.Status);

        // 14:30 → 14:40
        runner.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(
            FlightStatus.Landed,
            flight.Status);
    }

    [Fact]
    public void GetNextExecutionTime_ShouldReturnEarliestScheduledFlightWithinWindow()
    {
        var flightProvider = new InMemoryFlightProvider(new[]
        {
            new Flight
            {
                FlightNumber = "DL456",
                Airline = "Delta Air Lines",
                Origin = "ATL",
                Destination = "IAH",
                ScheduledTime = new DateTimeOffset(
                    2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
                Type = FlightType.Arrival
            }
        });

        var clock = new SimulationClock(
            new DateTimeOffset(
                2026, 10, 4, 14, 50, 0, TimeSpan.Zero));

        var processor =
            new FlightArrivalProcessor(clock);

        var step =
            new FlightArrivalSimulationStep(
                flightProvider,
                processor);

        var targetTime =
            new DateTimeOffset(
                2026, 10, 4, 15, 20, 0, TimeSpan.Zero);

        var nextExecutionTime =
            step.GetNextExecutionTime(
                clock.CurrentTime,
                targetTime);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
            nextExecutionTime);
    }

    [Fact]
    public void SimulationRunner_WhenAdvancingPastScheduledTime_ShouldBeginApproachAtScheduledTime()
    {
        var flightProvider = new InMemoryFlightProvider(new[]
        {
            new Flight
            {
                FlightNumber = "DL456",
                Airline = "Delta Air Lines",
                Origin = "ATL",
                Destination = "IAH",
                ScheduledTime = new DateTimeOffset(
                    2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
                Type = FlightType.Arrival
            }
        });

        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 50, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var processor =
            new FlightArrivalProcessor(clock);

        var step =
            new FlightArrivalSimulationStep(
                flightProvider,
                processor);

        var runner = new SimulationRunner(
            clock,
            [step]);

        runner.Advance(
            TimeSpan.FromMinutes(30));

        var flight = flightProvider
            .GetFlights()
            .Single(flight =>
                flight.FlightNumber == "DL456");

        Assert.Equal(
            FlightStatus.Approaching,
            flight.Status);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
            flight.ApproachStartedAt);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 15, 20, 0, TimeSpan.Zero),
            clock.CurrentTime);
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

    [Fact]
    public void SimulationRunner_WhenAdvancingPastApproachAndLanding_ShouldLandAtCorrectTime()
    {
        var flightProvider = new InMemoryFlightProvider(new[]
        {
            new Flight
            {
                FlightNumber = "DL456",
                Airline = "Delta Air Lines",
                Origin = "ATL",
                Destination = "IAH",
                ScheduledTime = new DateTimeOffset(
                    2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
                Type = FlightType.Arrival
            }
        });

        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 50, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var processor =
            new FlightArrivalProcessor(clock);

        var step =
            new FlightArrivalSimulationStep(
                flightProvider,
                processor);

        var runner = new SimulationRunner(
            clock,
            [step]);

        runner.Advance(
            TimeSpan.FromMinutes(40));

        var flight = flightProvider
            .GetFlights()
            .Single(flight =>
                flight.FlightNumber == "DL456");

        Assert.Equal(
            FlightStatus.Landed,
            flight.Status);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 15, 15, 0, TimeSpan.Zero),
            flight.ApproachStartedAt);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 15, 25, 0, TimeSpan.Zero),
            flight.LandedAt);

        Assert.Equal(
            new DateTimeOffset(
                2026, 10, 4, 15, 30, 0, TimeSpan.Zero),
            clock.CurrentTime);
    }
}