using System.Text.Json;
using OpsLattice.AgentEvaluation.Evaluation;
using Xunit;

namespace OpsLattice.AgentEvaluation.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public void ToJsonAndMarkdown_WhenARunFails_ShouldIncludeScenarioMetricsAndFailure()
    {
        var report = CreateReport();

        var json = ReportWriter.ToJson(report);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var result = root.GetProperty("results")[0];

        Assert.Equal("lax-to-iah-today", result.GetProperty("scenarioId").GetString());
        Assert.Equal("airport-operations", result.GetProperty("agentName").GetString());
        Assert.False(result.GetProperty("passed").GetBoolean());
        Assert.Equal(3, result.GetProperty("llmInvocationCount").GetInt32());
        Assert.Equal(1, result.GetProperty("mcpInvocationCount").GetInt32());
        Assert.Equal(6400, result.GetProperty("totalDurationMs").GetInt64());
        Assert.Contains(
            "result was 0",
            result.GetProperty("assertionFailures")[0].GetString(),
            StringComparison.Ordinal);

        var aggregate = root.GetProperty("aggregate");
        Assert.Equal(0, aggregate.GetProperty("passed").GetInt32());
        Assert.Equal(1, aggregate.GetProperty("failed").GetInt32());
        Assert.Equal(3, aggregate.GetProperty("agents")[0].GetProperty("llmInvocationCount").GetInt32());

        var markdown = ReportWriter.ToMarkdown(report);
        Assert.Contains("How many flights are flying from LAX to IAH today?", markdown, StringComparison.Ordinal);
        Assert.Contains("airport-operations: FAIL", markdown, StringComparison.Ordinal);
        Assert.Contains("result was 0, expected 8.", markdown, StringComparison.Ordinal);
        Assert.Contains("| airport-operations | 0 | 1 | 3 | 1 | 6400 |", markdown, StringComparison.Ordinal);
        Assert.Contains("Overall: 0 passed, 1 failed, 1 runs.", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WhenDirectoryIsNew_ShouldCreateJsonAndMarkdownFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"opslattice-report-{Guid.NewGuid():N}");

        try
        {
            var paths = ReportWriter.Write(CreateReport(), directory);

            Assert.True(File.Exists(paths.JsonPath));
            Assert.True(File.Exists(paths.MarkdownPath));
            Assert.Contains("lax-to-iah-today", File.ReadAllText(paths.JsonPath), StringComparison.Ordinal);
            Assert.Contains("FAIL", File.ReadAllText(paths.MarkdownPath), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static EvaluationReport CreateReport()
    {
        var scenario = new EvaluationScenario
        {
            Id = "lax-to-iah-today",
            Question = "How many flights are flying from LAX to IAH today?",
            Expectation = new ScenarioExpectation
            {
                Kind = ExpectationKinds.Results,
                Calls =
                [
                    new ExpectedToolCall
                    {
                        Tool = "get_flight_count",
                        Arguments = new Dictionary<string, string>
                        {
                            ["flightType"] = "Arrival",
                            ["origin"] = "LAX",
                            ["destination"] = "IAH",
                            ["date"] = "2026-10-06"
                        },
                        Result = "8"
                    }
                ]
            }
        };

        var evaluation = ExpectationEvaluator.Evaluate(
            scenario,
            "airport-operations",
            "I could not count those flights.",
            llmInvocationCount: 3,
            totalDurationMs: 6400,
            [
                new CapturedToolCall(
                    "get_flight_count",
                    new Dictionary<string, string>
                    {
                        ["flightType"] = "Arrival",
                        ["origin"] = "LAX",
                        ["destination"] = "IAH",
                        ["date"] = "2026-10-06"
                    },
                    "0",
                    40)
            ]);

        return new EvaluationReport(
            new DateTimeOffset(2026, 10, 25, 13, 0, 0, TimeSpan.Zero),
            "qwen2.5:7b-instruct-q4_K_M",
            "http://127.0.0.1:11434",
            new SimulationDescriptor(100, "2026-10-06", 42),
            [evaluation],
            ExpectationEvaluator.Summarize([evaluation]));
    }
}
