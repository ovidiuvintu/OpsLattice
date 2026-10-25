using OpsLattice.AgentEvaluation.Evaluation;
using Xunit;

namespace OpsLattice.AgentEvaluation.Tests;

public sealed class ControlledSimulationTests
{
    [Fact]
    public async Task StartAsync_WhenSimulationIsActivated_ShouldExposeTheScenarioCounts()
    {
        await using var session = await EvaluationSession.StartAsync();

        var laxToIah = await session.CallToolTextAsync(
            "get_flight_count",
            new Dictionary<string, string>
            {
                ["flightType"] = "Arrival",
                ["origin"] = "LAX",
                ["destination"] = "IAH",
                ["date"] = "2026-10-06"
            });

        var iahToOrd = await session.CallToolTextAsync(
            "get_flight_count",
            new Dictionary<string, string>
            {
                ["flightType"] = "Departure",
                ["origin"] = "IAH",
                ["destination"] = "ORD",
                ["date"] = "2026-10-06"
            });

        var arrivalsTomorrow = await session.CallToolTextAsync(
            "get_flight_count",
            new Dictionary<string, string>
            {
                ["flightType"] = "Arrival",
                ["destination"] = "IAH",
                ["date"] = "2026-10-07"
            });

        var arrivals = await session.CallToolTextAsync(
            "get_flight_count",
            new Dictionary<string, string>
            {
                ["flightType"] = "Arrival",
                ["destination"] = "IAH",
                ["date"] = "2026-10-06"
            });

        var departures = await session.CallToolTextAsync(
            "get_flight_count",
            new Dictionary<string, string>
            {
                ["flightType"] = "Departure",
                ["origin"] = "IAH",
                ["date"] = "2026-10-06"
            });

        var simulationTime = await session.CallToolTextAsync(
            "get_simulation_time",
            new Dictionary<string, string>());

        Assert.Equal("8", laxToIah.Trim());
        Assert.Equal("9", iahToOrd.Trim());
        Assert.Equal("50", arrivals.Trim());
        Assert.Equal("50", departures.Trim());
        Assert.Equal("0", arrivalsTomorrow.Trim());
        Assert.Contains("2026-10-06", simulationTime, StringComparison.Ordinal);
    }
}
