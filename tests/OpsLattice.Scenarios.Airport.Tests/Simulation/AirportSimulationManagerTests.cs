
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Simulation;

public sealed class AirportSimulationManagerTests
{
    private readonly AirportSimulationSetupService _setupService = new(
        new TerminalGenerator(),
        new GateGenerator(),
        new FlightScheduleGenerator());

    private readonly AirportSimulationRuntimeFactory _factory = new();

    private AirportSimulationRuntime CreateRuntime(
        DateOnly? date = null)
    {
        var configuration = new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("B", 12)
            ],
            FlightCount: 250,
            ScheduleDate: date ?? new DateOnly(2026, 11, 2),
            RandomSeed: 42);

        var definition = _setupService.Build(configuration);

        return _factory.Create(definition);
    }

    [Fact]
    public void HasActiveSimulation_Initially_ShouldBeFalse()
    {
        var manager = new AirportSimulationManager();

        Assert.False(manager.HasActiveSimulation);
    }

    [Fact]
    public void Activate_ShouldSetActiveSimulation()
    {
        var manager = new AirportSimulationManager();

        manager.Activate(CreateRuntime());

        Assert.True(manager.HasActiveSimulation);
    }

    [Fact]
    public void GetCurrentTime_WithoutActiveSimulation_ShouldThrow()
    {
        var manager = new AirportSimulationManager();

        Assert.Throws<InvalidOperationException>(
            () => manager.GetCurrentTime());
    }

    [Fact]
    public void Advance_WithoutActiveSimulation_ShouldThrow()
    {
        var manager = new AirportSimulationManager();

        Assert.Throws<InvalidOperationException>(() =>
            manager.Advance(TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Advance_ShouldAdvanceActiveSimulation()
    {
        var manager = new AirportSimulationManager();

        manager.Activate(CreateRuntime());

        var result = manager.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(
            new DateTimeOffset(2026, 11, 2, 0, 30, 0, TimeSpan.Zero),
            result);
    }

    [Fact]
    public void Activate_ShouldReplacePreviousSimulation()
    {
        var manager = new AirportSimulationManager();

        manager.Activate(CreateRuntime(new DateOnly(2026, 11, 2)));

        manager.Advance(TimeSpan.FromHours(2));

        manager.Activate(CreateRuntime(new DateOnly(2026, 11, 3)));

        Assert.Equal(
            new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero),
            manager.GetCurrentTime());
    }

    [Fact]
    public void Advance_WithInvalidDuration_ShouldThrow()
    {
        var manager = new AirportSimulationManager();

        manager.Activate(CreateRuntime());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.Advance(TimeSpan.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.Advance(TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void GetFlights_WithActiveSimulation_ShouldReturnFlightSnapshots()
    {
        var setupService = new AirportSimulationSetupService(
            new TerminalGenerator(),
            new GateGenerator(),
            new FlightScheduleGenerator());

        var configuration = new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 5)
            ],
            FlightCount: 10,
            ScheduleDate: new DateOnly(2026, 10, 6),
            RandomSeed: 42);

        var definition = setupService.Build(configuration);

        var runtime = new AirportSimulationRuntimeFactory()
            .Create(definition);

        var manager = new AirportSimulationManager();
        manager.Activate(runtime);

        var flights = manager.GetFlights();

        Assert.Equal(10, flights.Count);
        Assert.All(flights, flight =>
            Assert.False(string.IsNullOrWhiteSpace(flight.FlightNumber)));
    }
    [Fact]
    public void GetFlights_WithoutActiveSimulation_ShouldThrow()
    {
        var manager = new AirportSimulationManager();

        Assert.Throws<InvalidOperationException>(
            () => manager.GetFlights());
    }

    [Fact]
    public void GetFlights_WhenGateIsAssigned_ShouldIncludeGateCode()
    {
        // Arrange
        var setupService = new AirportSimulationSetupService(
            new TerminalGenerator(),
            new GateGenerator(),
            new FlightScheduleGenerator());

        var configuration = new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 5)
            ],
            FlightCount: 10,
            ScheduleDate: new DateOnly(2026, 10, 6),
            RandomSeed: 42);

        var definition = setupService.Build(configuration);

        var runtime = new AirportSimulationRuntimeFactory()
            .Create(definition);

        // Select a flight and an available gate.
        var flight = runtime.Definition.Flights.First();
        var gate = runtime.Definition.Gates.First();

        // Assign the flight to the gate.
        gate.Assign(flight);

        // Activate the simulation.
        var manager = new AirportSimulationManager();
        manager.Activate(runtime);

        // Act
        var flights = manager.GetFlights();

        var snapshot = flights.Single(
            f => f.FlightNumber == flight.FlightNumber);

        // Assert
        Assert.Equal(gate.Code, snapshot.Gate);
        Assert.Equal(flight.FlightNumber, snapshot.FlightNumber);
        Assert.Equal(flight.Status.ToString(), snapshot.Status);
    }

}
