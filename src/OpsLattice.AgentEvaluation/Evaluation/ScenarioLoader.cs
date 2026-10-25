using System.Text.Json;

namespace OpsLattice.AgentEvaluation.Evaluation;

public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<EvaluationScenario> LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        return Load(json);
    }

    public static IReadOnlyList<EvaluationScenario> Load(string json)
    {
        EvaluationScenarioDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<EvaluationScenarioDocument>(
                json,
                _jsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Scenario document is not valid JSON: {exception.Message}",
                exception);
        }

        if (document?.Scenarios is null || document.Scenarios.Count == 0)
        {
            throw new InvalidDataException(
                "Scenario document must contain at least one scenario.");
        }

        var scenarios = new List<EvaluationScenario>(document.Scenarios.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scenario in document.Scenarios)
            scenarios.Add(Normalize(scenario, seenIds));

        return scenarios;
    }

    private static EvaluationScenario Normalize(
        ScenarioDocument scenario,
        HashSet<string> seenIds)
    {
        var id = RequireText(scenario.Id, "Scenario id");
        if (!seenIds.Add(id))
        {
            throw new InvalidDataException(
                $"Scenario id '{id}' is duplicated.");
        }

        var question = RequireText(scenario.Question, $"Scenario '{id}' question");
        var expectation = scenario.Expectation
            ?? throw new InvalidDataException(
                $"Scenario '{id}' is missing an expectation.");

        var kind = RequireText(expectation.Kind, $"Scenario '{id}' expectation kind")
            .ToLowerInvariant();

        if (kind is not ExpectationKinds.Results and not ExpectationKinds.Failure)
        {
            throw new InvalidDataException(
                $"Scenario '{id}' has unsupported expectation kind '{expectation.Kind}'. Use '{ExpectationKinds.Results}' or '{ExpectationKinds.Failure}'.");
        }

        var calls = new List<ExpectedToolCall>();
        foreach (var call in expectation.Calls ?? [])
            calls.Add(NormalizeCall(id, kind, call));

        if (kind == ExpectationKinds.Results && calls.Count == 0)
        {
            throw new InvalidDataException(
                $"Scenario '{id}' expects results but lists no MCP calls.");
        }

        if (kind == ExpectationKinds.Failure && calls.Count > 0)
        {
            throw new InvalidDataException(
                $"Scenario '{id}' expects failure or clarification and cannot list MCP calls.");
        }

        return new EvaluationScenario
        {
            Id = id,
            Question = question,
            Expectation = new ScenarioExpectation
            {
                Kind = kind,
                Calls = calls
            }
        };
    }

    private static ExpectedToolCall NormalizeCall(
        string scenarioId,
        string kind,
        ExpectedCallDocument call)
    {
        var tool = RequireText(
            call.Tool,
            $"Scenario '{scenarioId}' tool name");

        var arguments = ReadArguments(
            scenarioId,
            call.Arguments);
        var optionalArguments = ReadArguments(
            scenarioId,
            call.OptionalArguments);

        foreach (var key in optionalArguments.Keys)
        {
            if (arguments.ContainsKey(key))
            {
                throw new InvalidDataException(
                    $"Scenario '{scenarioId}' lists '{key}' as both a required and an optional argument.");
            }
        }

        if (kind == ExpectationKinds.Results &&
            string.IsNullOrWhiteSpace(call.Result))
        {
            throw new InvalidDataException(
                $"Scenario '{scenarioId}' call '{tool}' is missing an expected result.");
        }

        return new ExpectedToolCall
        {
            Tool = tool,
            Arguments = arguments,
            OptionalArguments = optionalArguments,
            Result = call.Result?.Trim() ?? string.Empty
        };
    }

    private static Dictionary<string, string> ReadArguments(
        string scenarioId,
        Dictionary<string, string?>? source)
    {
        var arguments = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (source is null)
            return arguments;

        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new InvalidDataException(
                    $"Scenario '{scenarioId}' has an empty argument name.");
            }

            arguments[pair.Key] = pair.Value ?? string.Empty;
        }

        return arguments;
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{label} is required.");

        return value.Trim();
    }

    private sealed class EvaluationScenarioDocument
    {
        public List<ScenarioDocument>? Scenarios { get; init; }
    }

    private sealed class ScenarioDocument
    {
        public string? Id { get; init; }

        public string? Question { get; init; }

        public ExpectationDocument? Expectation { get; init; }
    }

    private sealed class ExpectationDocument
    {
        public string? Kind { get; init; }

        public List<ExpectedCallDocument>? Calls { get; init; }
    }

    private sealed class ExpectedCallDocument
    {
        public string? Tool { get; init; }

        public Dictionary<string, string?>? Arguments { get; init; }

        public Dictionary<string, string?>? OptionalArguments { get; init; }

        public string? Result { get; init; }
    }
}
