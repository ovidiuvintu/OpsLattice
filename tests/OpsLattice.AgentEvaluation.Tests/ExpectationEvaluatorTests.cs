using OpsLattice.AgentEvaluation.Evaluation;
using Xunit;

namespace OpsLattice.AgentEvaluation.Tests;

public sealed class ExpectationEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenFlightCallMatchesAndSimulationTimeIsExtra_ShouldPass()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            LaxToIah(),
            "airport-operations-planned",
            "There are 8 flights.",
            llmInvocationCount: 1,
            totalDurationMs: 2100,
            [
                new CapturedToolCall(
                    "get_simulation_time",
                    new Dictionary<string, string>(),
                    "\"2026-10-06T00:00:00+00:00\"",
                    20),
                FlightCount("Arrival", "LAX", "IAH", "2026-10-06", "8")
            ]);

        Assert.True(evaluation.Passed);
        Assert.Empty(evaluation.AssertionFailures);
        Assert.Equal(2, evaluation.McpInvocationCount);
        Assert.Equal(1, evaluation.LlmInvocationCount);
        Assert.Equal(2100, evaluation.TotalDurationMs);
    }

    [Fact]
    public void Evaluate_WhenFlightTypeDiffers_ShouldFail()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            LaxToIah(),
            "airport-operations",
            "There are 0 flights.",
            llmInvocationCount: 3,
            totalDurationMs: 9000,
            [FlightCount("Departure", "LAX", "IAH", "2026-10-06", "0")]);

        Assert.False(evaluation.Passed);
        Assert.Contains(
            evaluation.AssertionFailures,
            failure => failure.Contains("flightType=Arrival", StringComparison.Ordinal)
                && failure.Contains("No matching MCP call", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_WhenResultDiffers_ShouldFailWithBothNumbers()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            LaxToIah(),
            "airport-operations-planned",
            "There are 9 flights.",
            llmInvocationCount: 1,
            totalDurationMs: 2000,
            [FlightCount("Arrival", "LAX", "IAH", "2026-10-06", "9")]);

        Assert.False(evaluation.Passed);
        var failure = Assert.Single(evaluation.AssertionFailures);
        Assert.Contains("result was 9", failure, StringComparison.Ordinal);
        Assert.Contains("expected 8", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_WhenAnExtraFlightCallIsPresent_ShouldFail()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            LaxToIah(),
            "airport-operations",
            "8 and 50.",
            llmInvocationCount: 2,
            totalDurationMs: 4000,
            [
                FlightCount("Arrival", "LAX", "IAH", "2026-10-06", "8"),
                FlightCount("Arrival", null, "IAH", "2026-10-06", "50")
            ]);

        Assert.False(evaluation.Passed);
        Assert.Contains(
            evaluation.AssertionFailures,
            failure => failure.Contains("Unexpected MCP flight call", StringComparison.Ordinal)
                && failure.Contains("result 50", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_WhenCallsArriveInReverseOrder_ShouldPass()
    {
        var scenario = new EvaluationScenario
        {
            Id = "two-counts",
            Question = "How many arrivals and departures?",
            Expectation = new ScenarioExpectation
            {
                Kind = ExpectationKinds.Results,
                Calls =
                [
                    Expected("Arrival", null, "IAH", "2026-10-06", "50"),
                    Expected("Departure", "IAH", "ORD", "2026-10-06", "9")
                ]
            }
        };

        var evaluation = ExpectationEvaluator.Evaluate(
            scenario,
            "airport-operations-planned",
            "50 and 9.",
            llmInvocationCount: 1,
            totalDurationMs: 3000,
            [
                FlightCount("Departure", "IAH", "ORD", "2026-10-06", "9"),
                FlightCount("Arrival", null, "IAH", "2026-10-06", "50")
            ]);

        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void Evaluate_WhenPlanValidationPrecedesTheMatchingCall_ShouldPassAndRecordTheError()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            LaxToIah(),
            "airport-operations-planned",
            "There are 8 flights.",
            llmInvocationCount: 2,
            totalDurationMs: 5000,
            [
                new CapturedToolCall(
                    "plan_validation",
                    new Dictionary<string, string> { ["attempt"] = "1" },
                    "arrival flights cannot use origin=IAH",
                    0),
                FlightCount("Arrival", "LAX", "IAH", "2026-10-06", "8")
            ]);

        Assert.True(evaluation.Passed);
        Assert.Equal(1, evaluation.McpInvocationCount);
        Assert.Contains(
            "arrival flights cannot use origin=IAH",
            Assert.Single(evaluation.ValidationErrors),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_WhenFailureHasNoSuccessfulFlightResult_ShouldPass()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            IahFromIah(),
            "airport-operations",
            "Departure flights require origin=IAH.",
            llmInvocationCount: 4,
            totalDurationMs: 12000,
            [
                new CapturedToolCall(
                    "get_flight_count",
                    Arguments("Arrival", "IAH", "IAH", "2026-10-06"),
                    "Arrival flights cannot use origin=IAH.",
                    30)
            ]);

        Assert.True(evaluation.Passed);
        Assert.Single(evaluation.ExecutionErrors);
    }

    [Fact]
    public void Evaluate_WhenFailureHasNoToolCalls_ShouldPass()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            IahFromIah(),
            "airport-operations-planned",
            "A flight cannot use IAH as both origin and destination.",
            llmInvocationCount: 1,
            totalDurationMs: 1800,
            []);

        Assert.True(evaluation.Passed);
        Assert.Equal(0, evaluation.McpInvocationCount);
    }

    [Fact]
    public void Evaluate_WhenFailureReturnsASuccessfulCount_ShouldFail()
    {
        var evaluation = ExpectationEvaluator.Evaluate(
            IahFromIah(),
            "airport-operations-planned",
            "There are 50 arrivals.",
            llmInvocationCount: 2,
            totalDurationMs: 5000,
            [FlightCount("Arrival", null, "IAH", "2026-10-06", "50")]);

        Assert.False(evaluation.Passed);
        var failure = Assert.Single(evaluation.AssertionFailures);
        Assert.Contains("returned 50", failure, StringComparison.Ordinal);
        Assert.Contains("clarification", failure, StringComparison.OrdinalIgnoreCase);
    }

    private static EvaluationScenario LaxToIah()
    {
        return new EvaluationScenario
        {
            Id = "lax-to-iah-today",
            Question = "How many flights are flying from LAX to IAH today?",
            Expectation = new ScenarioExpectation
            {
                Kind = ExpectationKinds.Results,
                Calls = [Expected("Arrival", "LAX", "IAH", "2026-10-06", "8")]
            }
        };
    }

    private static EvaluationScenario IahFromIah()
    {
        return new EvaluationScenario
        {
            Id = "iah-from-iah",
            Question = "How many flights are arriving at IAH from IAH on October 6, 2026?",
            Expectation = new ScenarioExpectation
            {
                Kind = ExpectationKinds.Failure,
                Calls = []
            }
        };
    }

    private static ExpectedToolCall Expected(
        string flightType,
        string? origin,
        string? destination,
        string date,
        string result)
    {
        return new ExpectedToolCall
        {
            Tool = "get_flight_count",
            Arguments = Arguments(flightType, origin, destination, date),
            Result = result
        };
    }

    private static CapturedToolCall FlightCount(
        string flightType,
        string? origin,
        string? destination,
        string date,
        string result)
    {
        return new CapturedToolCall(
            "get_flight_count",
            Arguments(flightType, origin, destination, date),
            result,
            15);
    }

    private static Dictionary<string, string> Arguments(
        string flightType,
        string? origin,
        string? destination,
        string date)
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["flightType"] = flightType,
            ["date"] = date
        };

        if (origin is not null)
            arguments["origin"] = origin;

        if (destination is not null)
            arguments["destination"] = destination;

        return arguments;
    }
}
