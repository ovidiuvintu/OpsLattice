using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class GateOccupancySimulationStepTests
{
    private static readonly DateTimeOffset _startTime =
        new(2026, 10, 4, 15, 20, 0, TimeSpan.Zero);

    private readonly SimulationClock _clock;
    private readonly InMemoryGateProvider _gateProvider;

    public GateOccupancySimulationStepTests()
    {
        _clock = new SimulationClock(_startTime);
        _gateProvider = new InMemoryGateProvider();
    }

    [Fact]
    public void GetNextExecutionTime_WhenFlightHasLanded_ShouldReturnTaxiCompletionTime()
    {
        // Arrange
        var flight = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(5));

        var gate = _gateProvider.GetGates().First();
        gate.Assign(flight);

        var step = CreateStep(flight);

        // Act
        var next = step.GetNextExecutionTime(
            _startTime,
            _startTime.AddMinutes(30));

        // Assert
        Assert.Equal(
            _startTime.AddMinutes(15),
            next);
    }

    [Fact]
    public void GetNextExecutionTime_WhenLandedFlightHasNoGate_ShouldReturnNull()
    {
        // Arrange
        var flight = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(5));

        var step = CreateStep(flight);

        // Act
        var next = step.GetNextExecutionTime(
            _startTime,
            _startTime.AddMinutes(30));

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetNextExecutionTime_WhenFlightHasNotLanded_ShouldReturnNull()
    {
        // Arrange
        var flight = CreateFlight(
            "DL100",
            FlightType.Arrival,
            _startTime.AddMinutes(20));

        var gate = _gateProvider.GetGates().First();
        gate.Assign(flight);

        var step = CreateStep(flight);

        // Act
        var next = step.GetNextExecutionTime(
            _startTime,
            _startTime.AddMinutes(30));

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetNextExecutionTime_WhenFlightIsDeparture_ShouldReturnNull()
    {
        // Arrange
        var flight = CreateFlight(
            "DL200",
            FlightType.Departure,
            _startTime.AddMinutes(20));

        var step = CreateStep(flight);

        // Act
        var next = step.GetNextExecutionTime(
            _startTime,
            _startTime.AddMinutes(30));

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetNextExecutionTime_WhenTaxiCompletionIsOutsideWindow_ShouldReturnNull()
    {
        // Arrange
        var flight = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(15));

        var gate = _gateProvider.GetGates().First();
        gate.Assign(flight);

        var step = CreateStep(flight);

        // Act
        var next = step.GetNextExecutionTime(
            _startTime,
            _startTime.AddMinutes(20));

        // Taxi completion is at StartTime + 25 minutes.

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetNextExecutionTime_WhenTaxiCompletionIsAlreadyPast_ShouldReturnNull()
    {
        // Arrange
        var flight = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(5));

        var gate = _gateProvider.GetGates().First();
        gate.Assign(flight);

        _clock.Advance(TimeSpan.FromMinutes(20));

        var step = CreateStep(flight);

        // Act
        var next = step.GetNextExecutionTime(
            _clock.CurrentTime,
            _clock.CurrentTime.AddMinutes(30));

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetNextExecutionTime_WithMultipleFlights_ShouldReturnEarliestEvent()
    {
        // Arrange
        var first = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(10));

        var second = CreateLandedArrival(
            "DL200",
            _startTime.AddMinutes(5));

        var gates = _gateProvider.GetGates().ToArray();

        gates[0].Assign(first);
        gates[1].Assign(second);

        var step = CreateStep(first, second);

        // Act
        var next = step.GetNextExecutionTime(
            _startTime,
            _startTime.AddMinutes(30));

        // Assert
        Assert.Equal(
            _startTime.AddMinutes(15),
            next);
    }

    [Fact]
    public void Execute_WhenTaxiCompleted_ShouldOccupyGate()
    {
        // Arrange
        var flight = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(5));

        var gate = _gateProvider.GetGates().First();
        gate.Assign(flight);

        var step = CreateStep(flight);

        _clock.Advance(TimeSpan.FromMinutes(15));

        // Act
        step.Execute();

        // Assert
        Assert.Equal(
            FlightStatus.AtGate,
            flight.Status);

        Assert.True(gate.IsOccupied);
        Assert.Same(flight, gate.OccupyingFlight);

        Assert.Equal(
            _clock.CurrentTime,
            flight.ArrivedAtGateAt);
    }

    [Fact]
    public void Execute_WhenFlightHasNoGate_ShouldRemainLanded()
    {
        // Arrange
        var flight = CreateLandedArrival(
            "DL100",
            _startTime.AddMinutes(5));

        var step = CreateStep(flight);

        _clock.Advance(TimeSpan.FromMinutes(15));

        // Act
        step.Execute();

        // Assert
        Assert.Equal(
            FlightStatus.Landed,
            flight.Status);

        Assert.All(
            _gateProvider.GetGates(),
            gate => Assert.False(gate.IsOccupied));
    }

    private GateOccupancySimulationStep CreateStep(
        params Flight[] flights)
    {
        // Uses the same flight provider contract as production.
        var flightProvider =
            new InMemoryFlightProvider(flights);

        var processor = new GateOccupancyProcessor(
            _clock,
            _gateProvider);

        return new GateOccupancySimulationStep(
            flightProvider,
            processor);
    }

    private static Flight CreateFlight(
        string flightNumber,
        FlightType type,
        DateTimeOffset scheduledTime)
    {
        return new Flight
        {
            FlightNumber = flightNumber,
            Airline = "Delta",
            Origin = "ORD",
            Destination = "ATL",
            Type = type,
            ScheduledTime = scheduledTime
        };
    }

    private static Flight CreateLandedArrival(
        string flightNumber,
        DateTimeOffset landedAt)
    {
        var flight = CreateFlight(
            flightNumber,
            FlightType.Arrival,
            landedAt.AddMinutes(-10));

        flight.BeginApproach(landedAt.AddMinutes(-10));
        flight.Land(landedAt);

        return flight;
    }
}
