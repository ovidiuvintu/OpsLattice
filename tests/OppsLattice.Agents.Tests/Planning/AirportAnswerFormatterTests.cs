using OpsLattice.Agents.Planning;
using Xunit;

namespace OppsLattice.Agents.Tests.Planning;

public sealed class AirportAnswerFormatterTests
{
    [Fact]
    public void Format_WhenCountAndSummaryAreSuccessful_ShouldProduceDeterministicMultiPartOutput()
    {
        var formatter = new AirportAnswerFormatter();

        var execution = new AirportQueryExecutionResult(
        [
            new AirportQueryExecutionItem(
                0,
                new AirportQuery(
                    AirportQueryOperation.CountFlights,
                    AirportFlightType.Arrival,
                    null,
                    null,
                    "IAH",
                    new DateOnly(2026, 10, 6),
                    null),
                true,
                "get_flight_count",
                new Dictionary<string, object?>(),
                "50",
                10),
            new AirportQueryExecutionItem(
                1,
                new AirportQuery(
                    AirportQueryOperation.FlightSummary,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                true,
                "get_flight_summary",
                new Dictionary<string, object?>(),
                "{\"total\":100,\"scheduled\":100}",
                8)
        ]);

        var answer = formatter.Format("question", execution);

        Assert.Contains("Flight count", answer);
        Assert.Contains("date=2026-10-06", answer);
        Assert.Contains("Flight summary", answer);
    }

    [Fact]
    public void Format_WhenPartialFailuresOccur_ShouldReportFailuresExplicitly()
    {
        var formatter = new AirportAnswerFormatter();

        var execution = new AirportQueryExecutionResult(
        [
            new AirportQueryExecutionItem(
                0,
                new AirportQuery(
                    AirportQueryOperation.CountFlights,
                    AirportFlightType.Arrival,
                    null,
                    null,
                    "IAH",
                    new DateOnly(2026, 10, 6),
                    null),
                false,
                "get_flight_count",
                new Dictionary<string, object?>(),
                "validation error",
                0),
            new AirportQueryExecutionItem(
                1,
                new AirportQuery(
                    AirportQueryOperation.CountFlights,
                    AirportFlightType.Departure,
                    null,
                    "IAH",
                    null,
                    new DateOnly(2026, 10, 6),
                    null),
                true,
                "get_flight_count",
                new Dictionary<string, object?>(),
                "50",
                7)
        ]);

        var answer = formatter.Format("question", execution);

        Assert.Contains("failed - validation error", answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Flight count", answer);
    }
}
