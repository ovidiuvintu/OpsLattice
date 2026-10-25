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

    [Fact]
    public void GetFlightCount_WithArrivalAndHomeAirportAsOriginOnly_ShouldThrowDirectionalValidationError()
    {
        var (tools, _) = CreateSut();

        var exception = Assert.Throws<ArgumentException>(
            () => tools.GetFlightCount(
                origin: "IAH",
                flightType: "Arrival",
                date: "2026-10-06"));

        Assert.Contains("destination=IAH", exception.Message);
    }

    [Fact]
    public void GetFlightCount_WithArrivalAndHomeAirportAsDestination_ShouldReturnMatchingCount()
    {
        var (tools, queryService) = CreateSut();

        var expectedArrivalsToIah = queryService.GetFlights()
            .Count(f =>
                string.Equals(f.Type, nameof(FlightType.Arrival), StringComparison.Ordinal)
                && string.Equals(f.Destination, "IAH", StringComparison.OrdinalIgnoreCase)
                && DateOnly.FromDateTime(f.ScheduledTime.UtcDateTime) == new DateOnly(2026, 10, 6));

        var result = tools.GetFlightCount(
            destination: "IAH",
            flightType: "Arrival",
            date: "2026-10-06");

        Assert.Equal(expectedArrivalsToIah, result);
    }

    [Fact]
    public void GetFlightCount_WithArrivalsAtIahFromOrdOnOctober62026_ShouldReturnMatchingCount()
    {
        var (tools, queryService) = CreateSut();

        var expected = queryService.GetFlights()
            .Count(f =>
                string.Equals(f.Type, nameof(FlightType.Arrival), StringComparison.Ordinal)
                && string.Equals(f.Origin, "ORD", StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.Destination, "IAH", StringComparison.OrdinalIgnoreCase)
                && DateOnly.FromDateTime(f.ScheduledTime.UtcDateTime) == new DateOnly(2026, 10, 6));

        var result = tools.GetFlightCount(
            origin: "ORD",
            destination: "IAH",
            flightType: "Arrival",
            date: "2026-10-06");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetFlightCount_WithDepartureFromLaxToIah_ShouldThrowDirectionalValidationError()
    {
        var (tools, _) = CreateSut();

        var exception = Assert.Throws<ArgumentException>(
            () => tools.GetFlightCount(
                origin: "LAX",
                destination: "IAH",
                flightType: "Departure",
                date: "2026-10-06"));

        Assert.Contains("departure flights", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFlightCount_WithArrivalFromIahToLax_ShouldThrowDirectionalValidationError()
    {
        var (tools, _) = CreateSut();

        var exception = Assert.Throws<ArgumentException>(
            () => tools.GetFlightCount(
                origin: "IAH",
                destination: "LAX",
                flightType: "Arrival",
                date: "2026-10-06"));

        Assert.Contains("arrival flights", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFlightCount_WithStatus_ShouldCountOnlyMatchingStatus()
    {
        var (tools, queryService) = CreateSut();
        var flights = queryService.GetFlights();

        var expectedScheduled = flights.Count(f =>
            string.Equals(f.Status, nameof(FlightStatus.Scheduled), StringComparison.Ordinal));

        Assert.Equal(expectedScheduled, tools.GetFlightCount(status: "scheduled"));
        Assert.Equal(0, tools.GetFlightCount(status: "Landed"));
    }

    [Fact]
    public void GetFlightCount_WithInvalidStatus_ShouldThrowValidationError()
    {
        var (tools, _) = CreateSut();

        var exception = Assert.Throws<ArgumentException>(
            () => tools.GetFlightCount(status: "OnTime"));

        Assert.Contains("Approaching", exception.Message);
        Assert.Contains("Omit status", exception.Message);
    }

    [Fact]
    public void SearchFlights_WithRouteAndDate_ShouldReturnMatchingFlightDetailsInScheduleOrder()
    {
        var (tools, queryService) = CreateSut();

        var departure = queryService.GetFlights()
            .First(f => string.Equals(f.Type, nameof(FlightType.Departure), StringComparison.Ordinal));

        var expected = queryService.GetFlights()
            .Where(f =>
                string.Equals(f.Type, nameof(FlightType.Departure), StringComparison.Ordinal)
                && string.Equals(f.Destination, departure.Destination, StringComparison.OrdinalIgnoreCase)
                && DateOnly.FromDateTime(f.ScheduledTime.UtcDateTime) == new DateOnly(2026, 10, 6))
            .OrderBy(f => f.ScheduledTime)
            .Select(f => f.FlightNumber)
            .ToArray();

        var result = tools.SearchFlights(
            origin: departure.Origin,
            destination: departure.Destination,
            flightType: "Departure",
            date: "2026-10-06");

        Assert.Equal(expected, result.Select(f => f.FlightNumber).ToArray());
        Assert.All(result, f =>
        {
            Assert.Equal(departure.Origin, f.Origin);
            Assert.Equal(departure.Destination, f.Destination);
            Assert.Equal(nameof(FlightType.Departure), f.Type);
            Assert.False(string.IsNullOrWhiteSpace(f.Airline));
        });
    }

    [Fact]
    public void SearchFlights_WithNoFilters_ShouldReturnAllFlights()
    {
        var (tools, queryService) = CreateSut();

        Assert.Equal(queryService.GetFlights().Count, tools.SearchFlights().Count);
    }

    [Fact]
    public void SearchFlights_WithArrivalAndHomeAirportAsOrigin_ShouldThrowDirectionalValidationError()
    {
        var (tools, _) = CreateSut();

        var exception = Assert.Throws<ArgumentException>(
            () => tools.SearchFlights(origin: "IAH", flightType: "Arrival"));

        Assert.Contains("destination=IAH", exception.Message);
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
