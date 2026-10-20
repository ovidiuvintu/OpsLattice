using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;
using Xunit;

namespace OpsLattice.Api.IntegrationTests.Tools;

public sealed class FlightToolsTests
{
    [Fact]
    public void GetFlightCount_WithInvalidFlightType_ShouldThrowValidationError()
    {
        var (tools, _) = CreateSut();

        var exception = Assert.Throws<ArgumentException>(
            () => tools.GetFlightCount(flightType: "All"));

        Assert.Contains("Arrival", exception.Message);
        Assert.Contains("Departure", exception.Message);
        Assert.Contains("Omit flightType", exception.Message);
    }

    [Fact]
    public void GetFlightCount_WithNullOrOmittedFlightType_ShouldIncludeAllFlights()
    {
        var (tools, queryService) = CreateSut();
        var expectedTotal = queryService.GetFlights().Count;

        var omittedResult = tools.GetFlightCount();
        var nullResult = tools.GetFlightCount(flightType: null);

        Assert.Equal(expectedTotal, omittedResult);
        Assert.Equal(expectedTotal, nullResult);
    }

    [Fact]
    public void GetFlightCount_WithArrivalOrDepartureFlightType_ShouldFilterCorrectly()
    {
        var (tools, queryService) = CreateSut();
        var flights = queryService.GetFlights();

        var expectedArrivals = flights.Count(f =>
            string.Equals(f.Type, nameof(FlightType.Arrival), StringComparison.Ordinal));

        var expectedDepartures = flights.Count(f =>
            string.Equals(f.Type, nameof(FlightType.Departure), StringComparison.Ordinal));

        var arrivalResult = tools.GetFlightCount(flightType: "arrival");
        var departureResult = tools.GetFlightCount(flightType: "DEPARTURE");

        Assert.Equal(expectedArrivals, arrivalResult);
        Assert.Equal(expectedDepartures, departureResult);
    }

    private static (FlightTools Tools, FlightQueryService QueryService) CreateSut()
    {
        var setupService = new AirportSimulationSetupService(
            new TerminalGenerator(),
            new GateGenerator(),
            new FlightScheduleGenerator());

        var configuration = new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("B", 8)
            ],
            FlightCount: 24,
            ScheduleDate: new DateOnly(2026, 10, 6),
            RandomSeed: 42);

        var definition = setupService.Build(configuration);
        var runtime = new AirportSimulationRuntimeFactory().Create(definition);

        var manager = new AirportSimulationManager();
        manager.Activate(runtime);

        var queryService = new FlightQueryService(manager);
        var tools = new FlightTools(queryService);

        return (tools, queryService);
    }
}
