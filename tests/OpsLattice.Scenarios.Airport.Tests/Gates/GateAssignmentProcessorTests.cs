using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Time;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests;

public sealed class GateAssignmentProcessorTests
{
    private static readonly DateTimeOffset _startTime =
        new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly SimulationClock _clock;
    private readonly GateAssignmentProcessor _processor;
    private readonly IGateProvider _gateProvider;

    public GateAssignmentProcessorTests()
    {
        _clock = new SimulationClock(_startTime);

        _gateProvider = new InMemoryGateProvider();

        _processor = new GateAssignmentProcessor(
            _clock,
            _gateProvider);
    }

    [Fact]
    public void Process_ShouldAssignGate_WhenArrivalIsWithinHorizon()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(30));

        // Act
        _processor.Process(flight);

        // Assert
        Assert.Contains(
            _gateProvider.GetGates(),
            gate => gate.IsAssigned);
    }

    [Fact]
    public void Process_ShouldNotAssignGate_WhenArrivalIsOutsideHorizon()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(90));

        // Act
        _processor.Process(flight);

        // Assert
        Assert.All(
            _gateProvider.GetGates(),
            gate => Assert.False(gate.IsAssigned));
    }

    [Fact]
    public void Process_ShouldAssignGate_WhenArrivalIsExactlyAtHorizon()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(60));

        // Act
        _processor.Process(flight);

        // Assert
        Assert.Contains(
            _gateProvider.GetGates(),
            gate => gate.IsAssigned);
    }

    [Fact]
    public void Process_ShouldNotAssignGate_ToDepartureFlight()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Departure,
            _startTime.AddMinutes(30));

        // Act
        _processor.Process(flight);

        // Assert
        Assert.All(
            _gateProvider.GetGates(),
            gate => Assert.False(gate.IsAssigned));
    }

    [Fact]
    public void Process_ShouldAssignGate_WhenArrivalIsApproaching()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(30));

        flight.BeginApproach(_startTime);

        // Act
        _processor.Process(flight);

        // Assert
        Assert.Contains(
            _gateProvider.GetGates(),
            gate => gate.AssignedFlight == flight);
    }

    [Fact]
    public void Process_ShouldNotAssignAdditionalGate_WhenAllGatesAreAssigned()
    {
        // Arrange
        var gates = _gateProvider.GetGates().ToArray();

        for (var i = 0; i < gates.Length; i++)
        {
            var existingFlight = CreateFlight(
                FlightType.Arrival,
                _startTime.AddMinutes(20),
                $"DL{i + 100}");

            gates[i].Assign(existingFlight);
        }

        var newFlight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(30),
            "DL999");

        // Act
        _processor.Process(newFlight);

        // Assert
        Assert.All(
            gates,
            gate => Assert.True(gate.IsAssigned));

        Assert.DoesNotContain(
            gates,
            gate => gate.AssignedFlight == newFlight);
    }

    [Fact]
    public void GetNextExecutionTime_ShouldReturnHorizonStart_ForScheduledArrival()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(90));

        // Act
        var nextExecutionTime =
            _processor.GetNextExecutionTime(flight);

        // Assert
        Assert.Equal(
            _startTime.AddMinutes(30),
            nextExecutionTime);
    }

    [Fact]
    public void GetNextExecutionTime_ShouldReturnNull_ForDepartureFlight()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Departure,
            _startTime.AddMinutes(30));

        // Act
        var nextExecutionTime =
            _processor.GetNextExecutionTime(flight);

        // Assert
        Assert.Null(nextExecutionTime);
    }

    [Fact]
    public void GetNextExecutionTime_ShouldReturnNull_WhenArrivalIsNotScheduled()
    {
        // Arrange
        var flight = CreateFlight(
            FlightType.Arrival,
            _startTime.AddMinutes(30));

        flight.BeginApproach(_startTime);

        // Act
        var nextExecutionTime =
            _processor.GetNextExecutionTime(flight);

        // Assert
        Assert.Null(nextExecutionTime);
    }

    private static Flight CreateFlight(
        FlightType type,
        DateTimeOffset scheduledTime,
        string flightNumber = "DL100")
    {
        return new Flight
        {
            FlightNumber = flightNumber,
            Airline = "Delta",
            Origin = "ATL",
            Destination = "IAH",
            Type = type,
            ScheduledTime = scheduledTime
        };
    }
}
