using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpsLattice.AgentEvaluation.Evaluation;

public static class ReportWriter
{
    public const string JsonFileName = "evaluation-report.json";

    public const string MarkdownFileName = "evaluation-report.md";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string ToJson(EvaluationReport report)
    {
        return JsonSerializer.Serialize(report, _jsonOptions);
    }

    public static string ToMarkdown(EvaluationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Agent evaluation");
        builder.AppendLine();
        builder.AppendLine(
            $"Generated: {report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ssK", CultureInfo.InvariantCulture)}");
        builder.AppendLine(
            $"Ollama: {report.OllamaModel} at {report.OllamaBaseUrl}");
        builder.AppendLine(
            $"Simulation: {report.Simulation.FlightCount} flights, schedule date {report.Simulation.ScheduleDate}, seed {report.Simulation.RandomSeed}");
        builder.AppendLine();
        builder.AppendLine(
            "A run passes when the MCP flight-tool arguments and results match the scenario. Answers are recorded and are not graded by a model.");
        builder.AppendLine();
        builder.AppendLine("## Aggregate");
        builder.AppendLine();
        builder.AppendLine("| Agent | Passed | Failed | LLM calls | MCP calls | Duration (ms) |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");

        foreach (var agent in report.Aggregate.Agents)
        {
            builder.AppendLine(
                $"| {EscapeCell(agent.AgentName)} | {agent.Passed} | {agent.Failed} | {agent.LlmInvocationCount} | {agent.McpInvocationCount} | {agent.TotalDurationMs} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Overall: {report.Aggregate.Passed} passed, {report.Aggregate.Failed} failed, {report.Aggregate.RunCount} runs.");

        foreach (var scenarioId in report.Results
            .Select(result => result.ScenarioId)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var scenarioResults = report.Results
                .Where(result => string.Equals(
                    result.ScenarioId,
                    scenarioId,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            builder.AppendLine();
            builder.AppendLine($"## {scenarioId}");
            builder.AppendLine();
            builder.AppendLine(scenarioResults[0].Question);

            foreach (var result in scenarioResults)
                AppendResult(builder, result);
        }

        return builder.ToString();
    }

    public static ReportPaths Write(EvaluationReport report, string directory)
    {
        Directory.CreateDirectory(directory);

        var jsonPath = Path.Combine(directory, JsonFileName);
        var markdownPath = Path.Combine(directory, MarkdownFileName);
        File.WriteAllText(jsonPath, ToJson(report));
        File.WriteAllText(markdownPath, ToMarkdown(report));

        return new ReportPaths(jsonPath, markdownPath);
    }

    private static void AppendResult(StringBuilder builder, ScenarioEvaluation result)
    {
        var status = result.Passed ? "PASS" : "FAIL";
        builder.AppendLine();
        builder.AppendLine($"### {result.AgentName}: {status}");
        builder.AppendLine();
        builder.AppendLine($"- LLM invocations: {result.LlmInvocationCount}");
        builder.AppendLine($"- MCP invocations: {result.McpInvocationCount}");
        builder.AppendLine($"- Duration: {result.TotalDurationMs} ms");
        builder.AppendLine();
        builder.AppendLine("Answer:");
        builder.AppendLine();
        AppendQuoted(builder, string.IsNullOrWhiteSpace(result.Answer)
            ? "(empty)"
            : result.Answer.Trim());
        builder.AppendLine();
        builder.AppendLine("Tool calls:");
        builder.AppendLine();

        if (result.ToolCalls.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var call in result.ToolCalls)
            {
                builder.AppendLine(
                    $"- {call.ToolName} ({call.DurationMs} ms) {FormatArguments(call.Arguments)}");
                builder.AppendLine($"  - result: {call.Result.Trim()}");
            }
        }

        AppendList(builder, "Validation errors", result.ValidationErrors);
        AppendList(builder, "Execution errors", result.ExecutionErrors);
        AppendList(builder, "Assertion failures", result.AssertionFailures);
    }

    private static void AppendList(
        StringBuilder builder,
        string title,
        IReadOnlyList<string> items)
    {
        builder.AppendLine();
        builder.AppendLine($"{title}:");
        builder.AppendLine();

        if (items.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var item in items)
        {
            foreach (var line in item.Replace("\r\n", "\n").Split('\n'))
                builder.AppendLine($"- {line}");
        }
    }

    private static void AppendQuoted(StringBuilder builder, string text)
    {
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            builder.AppendLine($"> {line}");
    }

    private static string FormatArguments(IReadOnlyDictionary<string, string> arguments)
    {
        if (arguments.Count == 0)
            return "{}";

        return string.Join(
            ", ",
            arguments
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string EscapeCell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}

public sealed record ReportPaths(string JsonPath, string MarkdownPath);
