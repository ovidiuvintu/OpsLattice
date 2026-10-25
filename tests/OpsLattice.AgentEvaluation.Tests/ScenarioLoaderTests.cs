using System.Text.Json;
using OpsLattice.AgentEvaluation.Evaluation;
using Xunit;

namespace OpsLattice.AgentEvaluation.Tests;

public sealed class ScenarioLoaderTests
{
    [Fact]
    public void Load_WhenDocumentIsValid_ShouldReturnScenarioFields()
    {
        var scenarios = ScenarioLoader.Load(
            """
            {
              "scenarios": [
                {
                  "id": "lax-to-iah-today",
                  "question": "How many flights are flying from LAX to IAH today?",
                  "expectation": {
                    "kind": "results",
                    "calls": [
                      {
                        "tool": "get_flight_count",
                        "arguments": {
                          "flightType": "Arrival",
                          "origin": "LAX",
                          "destination": "IAH",
                          "date": "2026-10-06"
                        },
                        "result": "8"
                      }
                    ]
                  }
                }
              ]
            }
            """);

        var scenario = Assert.Single(scenarios);
        Assert.Equal("lax-to-iah-today", scenario.Id);
        Assert.Equal(
            "How many flights are flying from LAX to IAH today?",
            scenario.Question);
        Assert.Equal(ExpectationKinds.Results, scenario.Expectation.Kind);

        var call = Assert.Single(scenario.Expectation.Calls);
        Assert.Equal("get_flight_count", call.Tool);
        Assert.Equal("Arrival", call.Arguments["flightType"]);
        Assert.Equal("LAX", call.Arguments["origin"]);
        Assert.Equal("IAH", call.Arguments["destination"]);
        Assert.Equal("2026-10-06", call.Arguments["date"]);
        Assert.Equal("8", call.Result);
    }

    [Fact]
    public void LoadFile_WhenPathExists_ShouldReadTheDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opslattice-scenarios-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            """
            {
              "scenarios": [
                {
                  "id": "contradiction",
                  "question": "How many flights are arriving at IAH from IAH?",
                  "expectation": { "kind": "failure", "calls": [] }
                }
              ]
            }
            """);

        try
        {
            var scenarios = ScenarioLoader.LoadFile(path);
            var scenario = Assert.Single(scenarios);
            Assert.Equal("contradiction", scenario.Id);
            Assert.Equal(ExpectationKinds.Failure, scenario.Expectation.Kind);
            Assert.Empty(scenario.Expectation.Calls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WhenScenarioIdIsDuplicated_ShouldThrow()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ScenarioLoader.Load(
            """
            {
              "scenarios": [
                {
                  "id": "same",
                  "question": "First?",
                  "expectation": { "kind": "failure", "calls": [] }
                },
                {
                  "id": "same",
                  "question": "Second?",
                  "expectation": { "kind": "failure", "calls": [] }
                }
              ]
            }
            """));

        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenKindIsUnknown_ShouldThrow()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ScenarioLoader.Load(
            """
            {
              "scenarios": [
                {
                  "id": "bad-kind",
                  "question": "How many flights?",
                  "expectation": { "kind": "judge", "calls": [] }
                }
              ]
            }
            """));

        Assert.Contains("unsupported expectation kind", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenResultsScenarioHasNoCalls_ShouldThrow()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ScenarioLoader.Load(
            """
            {
              "scenarios": [
                {
                  "id": "missing-calls",
                  "question": "How many flights?",
                  "expectation": { "kind": "results", "calls": [] }
                }
              ]
            }
            """));

        Assert.Contains("no MCP calls", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenShippedSuiteIsRead_ShouldContainResultAndFailureScenarios()
    {
        var path = Path.Combine(
            FindSolutionRoot(),
            "src",
            "OpsLattice.AgentEvaluation",
            "scenarios",
            "airport-operations.json");

        var scenarios = ScenarioLoader.LoadFile(path);

        Assert.Contains(scenarios, scenario => scenario.Id == "lax-to-iah-today");
        Assert.Contains(
            scenarios,
            scenario => scenario.Id == "iah-from-iah"
                && scenario.Expectation.Kind == ExpectationKinds.Failure
                && scenario.Expectation.Calls.Count == 0);
        Assert.All(
            scenarios.Where(scenario => scenario.Expectation.Kind == ExpectationKinds.Results),
            scenario => Assert.NotEmpty(scenario.Expectation.Calls));
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpsLattice.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate OpsLattice.slnx.");
    }
}

public sealed class ToolCallCaptureTests
{
    [Fact]
    public void FormatArguments_WhenValuesAreJsonElements_ShouldReadStringsAndNumbers()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "flightType": "Arrival",
              "date": "2026-10-06",
              "ignored": null
            }
            """);

        var arguments = new Dictionary<string, object?>
        {
            ["flightType"] = document.RootElement.GetProperty("flightType").Clone(),
            ["date"] = document.RootElement.GetProperty("date").Clone(),
            ["count"] = 8,
            ["ignored"] = document.RootElement.GetProperty("ignored").Clone()
        };

        var formatted = ToolCallCapture.FormatArguments(arguments);

        Assert.Equal("Arrival", formatted["flightType"]);
        Assert.Equal("2026-10-06", formatted["date"]);
        Assert.Equal("8", formatted["count"]);
        Assert.False(formatted.ContainsKey("ignored"));
    }
}
