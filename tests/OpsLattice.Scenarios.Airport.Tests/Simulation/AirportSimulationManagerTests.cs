
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
}
