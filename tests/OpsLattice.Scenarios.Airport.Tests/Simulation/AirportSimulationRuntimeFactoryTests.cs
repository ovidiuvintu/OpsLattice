
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Simulation;

public sealed class AirportSimulationRuntimeFactoryTests
{
    private readonly AirportSimulationSetupService _setupService = new(
        new TerminalGenerator(),
        new GateGenerator(),
        new FlightScheduleGenerator());

    private readonly AirportSimulationRuntimeFactory _factory = new();

    private static AirportSetupConfiguration CreateConfiguration() =>
        new(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("B", 12)
            ],
            FlightCount: 250,
            ScheduleDate: new DateOnly(2026, 11, 2),
            RandomSeed: 42);

    private AirportSimulationRuntime CreateRuntime()
    {
        var definition = _setupService.Build(CreateConfiguration());

        return _factory.Create(definition);
    }

    [Fact]
    public void Create_ShouldInitializeClock()
    {
        var runtime = CreateRuntime();

        Assert.Equal(
            new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            runtime.Clock.CurrentTime);
    }

    [Fact]
    public void Create_ShouldUseDefinitionFlights()
    {
        var runtime = CreateRuntime();

        Assert.Equal(250, runtime.FlightProvider.GetFlights().Count());

        Assert.Same(
            runtime.Definition.Flights.First(),
            runtime.FlightProvider.GetFlights().First());
    }

    [Fact]
    public void Create_ShouldUseDefinitionGates()
    {
        var runtime = CreateRuntime();

        Assert.Equal(22, runtime.GateProvider.GetGates().Count());

        Assert.Same(
            runtime.Definition.Gates.First(),
            runtime.GateProvider.GetGates().First());
    }

    [Fact]
    public void Create_ShouldProduceIndependentRuntimes()
    {
        var first = CreateRuntime();
        var second = CreateRuntime();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Clock, second.Clock);
        Assert.NotSame(first.Runner, second.Runner);

        Assert.NotSame(
            first.FlightProvider.GetFlights().First(),
            second.FlightProvider.GetFlights().First());

        Assert.NotSame(
            first.GateProvider.GetGates().First(),
            second.GateProvider.GetGates().First());
    }

    [Fact]
    public void Advance_ShouldNotAffectAnotherRuntime()
    {
        var first = CreateRuntime();
        var second = CreateRuntime();

        var originalTime = second.Clock.CurrentTime;

        first.Runner.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(
            originalTime.AddMinutes(30),
            first.Clock.CurrentTime);

        Assert.Equal(
            originalTime,
            second.Clock.CurrentTime);
    }
}
