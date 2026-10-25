using System.Globalization;
using System.Text.Json;

namespace OpsLattice.AgentEvaluation.Evaluation;

public static class ExpectationEvaluator
{
    public const string PlanValidationTool = "plan_validation";

    private static readonly HashSet<string> _flightTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_flight_count",
        "search_flights",
        "get_flight_summary",
        "get_flights"
    };

    public static ScenarioEvaluation Evaluate(
        EvaluationScenario scenario,
        string agentName,
        string answer,
        int llmInvocationCount,
        long totalDurationMs,
        IReadOnlyList<CapturedToolCall> toolCalls)
    {
        var validationErrors = toolCalls
            .Where(call => IsPlanValidation(call.ToolName))
            .Select(call => string.IsNullOrWhiteSpace(call.Result)
                ? "plan_validation returned no detail."
                : call.Result.Trim())
            .ToArray();

        var executionErrors = toolCalls
            .Where(call => !IsPlanValidation(call.ToolName) && IsExecutionError(call))
            .Select(call => $"{call.ToolName}: {call.Result.Trim()}")
            .ToArray();

        var mcpInvocationCount = toolCalls.Count(call => !IsPlanValidation(call.ToolName));
        var assertionFailures = scenario.Expectation.Kind switch
        {
            ExpectationKinds.Results => EvaluateResults(scenario, toolCalls),
            ExpectationKinds.Failure => EvaluateFailure(toolCalls),
            _ => new List<string>
            {
                $"Unsupported expectation kind '{scenario.Expectation.Kind}'."
            }
        };

        return new ScenarioEvaluation(
            scenario.Id,
            scenario.Question,
            agentName,
            answer,
            llmInvocationCount,
            mcpInvocationCount,
            totalDurationMs,
            toolCalls,
            validationErrors,
            executionErrors,
            assertionFailures.Count == 0,
            assertionFailures);
    }

    public static AggregateMetrics Summarize(IReadOnlyList<ScenarioEvaluation> results)
    {
        var agents = results
            .GroupBy(result => result.AgentName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AgentAggregate(
                group.Key,
                group.Count(item => item.Passed),
                group.Count(item => !item.Passed),
                group.Sum(item => item.LlmInvocationCount),
                group.Sum(item => item.McpInvocationCount),
                group.Sum(item => item.TotalDurationMs)))
            .ToArray();

        return new AggregateMetrics(
            results
                .Select(result => result.ScenarioId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            results.Count,
            results.Count(result => result.Passed),
            results.Count(result => !result.Passed),
            agents);
    }

    private static List<string> EvaluateResults(
        EvaluationScenario scenario,
        IReadOnlyList<CapturedToolCall> toolCalls)
    {
        var failures = new List<string>();
        var actual = toolCalls
            .Where(call => _flightTools.Contains(call.ToolName))
            .ToList();
        var used = new bool[actual.Count];

        foreach (var expected in scenario.Expectation.Calls)
        {
            var match = FindMatch(expected, actual, used, requireResult: true);
            if (match >= 0)
            {
                used[match] = true;
                continue;
            }

            var sameArguments = FindMatch(expected, actual, used, requireResult: false);
            if (sameArguments >= 0)
            {
                used[sameArguments] = true;
                var call = actual[sameArguments];
                failures.Add(
                    $"{Describe(expected.Tool, expected.Arguments)} result was {NormalizeResult(call.Result)}, expected {expected.Result}.");
                continue;
            }

            failures.Add(
                $"Expected {Describe(expected.Tool, expected.Arguments)} result {expected.Result}. " +
                $"No matching MCP call was found. Actual flight calls: {DescribeAll(actual)}.");
        }

        for (var i = 0; i < actual.Count; i++)
        {
            if (used[i])
                continue;

            var call = actual[i];
            failures.Add(
                $"Unexpected MCP flight call {Describe(call.ToolName, call.Arguments)} result {NormalizeResult(call.Result)}.");
        }

        return failures;
    }

    private static List<string> EvaluateFailure(IReadOnlyList<CapturedToolCall> toolCalls)
    {
        var failures = new List<string>();

        foreach (var call in toolCalls)
        {
            if (!_flightTools.Contains(call.ToolName))
                continue;

            if (!IsSuccessfulFlightResult(call.Result))
                continue;

            failures.Add(
                "Expected no successful flight-tool result because the question should fail or ask for clarification. " +
                $"{Describe(call.ToolName, call.Arguments)} returned {NormalizeResult(call.Result)}.");
        }

        return failures;
    }

    private static int FindMatch(
        ExpectedToolCall expected,
        IReadOnlyList<CapturedToolCall> actual,
        bool[] used,
        bool requireResult)
    {
        for (var i = 0; i < actual.Count; i++)
        {
            if (used[i])
                continue;

            var call = actual[i];
            if (!string.Equals(call.ToolName, expected.Tool, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ArgumentsMatch(expected, call.Arguments))
                continue;

            if (requireResult && !ResultsMatch(call.Result, expected.Result))
                continue;

            return i;
        }

        return -1;
    }

    private static bool ArgumentsMatch(
        ExpectedToolCall expected,
        IReadOnlyDictionary<string, string> actual)
    {
        foreach (var pair in expected.Arguments)
        {
            if (!actual.TryGetValue(pair.Key, out var actualValue))
                return false;

            if (!string.Equals(pair.Value, actualValue, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var pair in actual)
        {
            if (expected.Arguments.ContainsKey(pair.Key))
                continue;

            if (!expected.OptionalArguments.TryGetValue(pair.Key, out var allowed))
                return false;

            if (!string.Equals(allowed, pair.Value, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool ResultsMatch(string actual, string expected)
    {
        return string.Equals(
            NormalizeResult(actual),
            expected.Trim(),
            StringComparison.Ordinal);
    }

    private static bool IsExecutionError(CapturedToolCall call)
    {
        if (IsErrorResult(call.Result))
            return true;

        return _flightTools.Contains(call.ToolName)
            && !IsSuccessfulFlightResult(call.Result);
    }

    private static bool IsSuccessfulFlightResult(string result)
    {
        if (IsErrorResult(result))
            return false;

        var text = NormalizeResult(result);
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return true;

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind is
                JsonValueKind.Array or
                JsonValueKind.Object or
                JsonValueKind.Number;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsErrorResult(string result)
    {
        var text = result.Trim();
        if (text.Length == 0)
            return true;

        return text.Contains("error", StringComparison.OrdinalIgnoreCase)
            || text.Contains("does not support", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cannot use", StringComparison.OrdinalIgnoreCase)
            || text.Contains("was not executed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not available", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Unable to", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Failed to", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeResult(string actual)
    {
        var text = actual.Trim();
        if (text.Length < 2 || text[0] != '"')
            return text;

        try
        {
            var parsed = JsonSerializer.Deserialize<string>(text);
            return parsed?.Trim() ?? text;
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static bool IsPlanValidation(string toolName)
    {
        return string.Equals(
            toolName,
            PlanValidationTool,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeAll(IReadOnlyList<CapturedToolCall> calls)
    {
        if (calls.Count == 0)
            return "none";

        return string.Join(
            "; ",
            calls.Select(call =>
                $"{Describe(call.ToolName, call.Arguments)} result {NormalizeResult(call.Result)}"));
    }

    private static string Describe(
        string toolName,
        IReadOnlyDictionary<string, string> arguments)
    {
        if (arguments.Count == 0)
            return $"{toolName} {{}}";

        var rendered = string.Join(
            ", ",
            arguments
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        return $"{toolName} {{{rendered}}}";
    }
}
