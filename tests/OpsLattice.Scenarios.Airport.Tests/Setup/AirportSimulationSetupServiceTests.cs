
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Setup;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Setup;

public sealed class AirportSimulationSetupServiceTests
{
    private readonly AirportSimulationSetupService _service = new(
        new TerminalGenerator(),
        new GateGenerator(),
        new FlightScheduleGenerator());

    private static AirportSetupConfiguration CreateConfiguration()
    {
        return new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("B", 12)
            ],
            FlightCount: 250,
            ScheduleDate: new DateOnly(2026, 11, 2),
            RandomSeed: 42);
    }

    [Fact]
    public void Build_ShouldCreateConfiguredTerminals()
    {
        var definition = _service.Build(CreateConfiguration());

        Assert.Equal(2, definition.Terminals.Count);
        Assert.Equal(
            ["A", "B"],
            definition.Terminals.Select(t => t.Code));
    }

    [Fact]
    public void Build_ShouldCreateConfiguredGates()
    {
        var definition = _service.Build(CreateConfiguration());

        Assert.Equal(22, definition.Gates.Count);

        Assert.Equal(
            10,
            definition.Gates.Count(g => g.TerminalCode == "A"));

        Assert.Equal(
            12,
            definition.Gates.Count(g => g.TerminalCode == "B"));
    }

    [Fact]
    public void Build_ShouldCreateConfiguredFlights()
    {
        var definition = _service.Build(CreateConfiguration());

        Assert.Equal(250, definition.Flights.Count);

        Assert.Contains(
            definition.Flights,
            f => f.Type == FlightType.Arrival);

        Assert.Contains(
            definition.Flights,
            f => f.Type == FlightType.Departure);
    }

    [Fact]
    public void Build_ShouldInitializeSimulationTime()
    {
        var definition = _service.Build(CreateConfiguration());

        Assert.Equal(
            new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            definition.InitialTime);
    }

    [Fact]
    public void Build_ShouldCreateUnstartedSimulation()
    {
        var definition = _service.Build(CreateConfiguration());

        Assert.All(definition.Flights, flight =>
        {
            Assert.Equal(FlightStatus.Scheduled, flight.Status);
            Assert.Null(flight.ApproachStartedAt);
            Assert.Null(flight.LandedAt);
            Assert.Null(flight.ArrivedAtGateAt);
        });

        Assert.All(definition.Gates, gate =>
        {
            Assert.False(gate.IsAssigned);
            Assert.False(gate.IsOccupied);
        });
    }

    [Fact]
    public void Build_ShouldCreateIndependentSimulations()
    {
        var configuration = CreateConfiguration();

        var first = _service.Build(configuration);
        var second = _service.Build(configuration);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Flights.First(), second.Flights.First());
        Assert.NotSame(first.Gates.First(), second.Gates.First());
    }

    [Fact]
    public void Build_WithInvalidConfiguration_ShouldThrow()
    {
        var configuration = CreateConfiguration() with
        {
            FlightCount = 0
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Build(configuration));
    }
}
