using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Flights;

public sealed class FlightQueryServiceTests
{
    [Fact]
    public void GetFlightStatuses_WhenFlightHasAssignedGate_ReturnsGateCode()
    {
        // Arrange
        var setupService = new AirportSimulationSetupService(
            new TerminalGenerator(),
            new GateGenerator(),
            new FlightScheduleGenerator());

        var configuration = new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("B", 5)
            ],
            FlightCount: 10,
            ScheduleDate: new DateOnly(2026, 10, 6),
            RandomSeed: 42);

        var definition = setupService.Build(configuration);

        var runtime = new AirportSimulationRuntimeFactory()
            .Create(definition);

        var flight = runtime.Definition.Flights.First();
        var gate = runtime.Definition.Gates.First();

        gate.Assign(flight);

        var manager = new AirportSimulationManager();
        manager.Activate(runtime);

        var service = new FlightQueryService(manager);

        // Act
        var result = service.GetFlightStatuses()
            .Single(f => f.FlightNumber == flight.FlightNumber);

        // Assert
        Assert.Equal(flight.FlightNumber, result.FlightNumber);
        Assert.Equal("Scheduled", result.Status);
        Assert.Equal(gate.Code, result.AssignedGate);
        Assert.Null(result.OccupiedGate);
    }
}
