using OpsLattice.AgentEvaluation.Evaluation;

EvaluationCommandOptions options;
try
{
    options = EvaluationCommand.Parse(args);
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(EvaluationCommand.HelpText);
    return 0;
}

string scenariosPath;
try
{
    scenariosPath = EvaluationCommand.ResolveScenariosPath(options.ScenariosPath);
}
catch (FileNotFoundException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

IReadOnlyList<EvaluationScenario> scenarios;
try
{
    scenarios = ScenarioLoader.LoadFile(scenariosPath);
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

if (options.List)
{
    foreach (var scenario in scenarios)
        Console.WriteLine($"{scenario.Id}\t{scenario.Question}");

    return 0;
}

var selected = options.ScenarioId is null
    ? scenarios
    : scenarios
        .Where(scenario => string.Equals(
            scenario.Id,
            options.ScenarioId,
            StringComparison.OrdinalIgnoreCase))
        .ToArray();

if (selected.Count == 0)
{
    Console.Error.WriteLine($"Scenario '{options.ScenarioId}' was not found.");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await using var session = await EvaluationSession.StartAsync(cancellation.Token);
var report = await session.EvaluateAsync(
    selected,
    evaluation =>
    {
        var status = evaluation.Passed ? "PASS" : "FAIL";
        Console.WriteLine(
            $"{evaluation.ScenarioId}  {evaluation.AgentName}  {status}  llm={evaluation.LlmInvocationCount}  mcp={evaluation.McpInvocationCount}  {evaluation.TotalDurationMs}ms");
    },
    (scenarioId, agentName) =>
        Console.WriteLine($"{scenarioId}  {agentName}  running"),
    cancellation.Token);

var paths = ReportWriter.Write(report, options.OutputDirectory);
Console.WriteLine($"JSON report: {paths.JsonPath}");
Console.WriteLine($"Markdown report: {paths.MarkdownPath}");
Console.WriteLine(
    $"{report.Aggregate.Passed} passed, {report.Aggregate.Failed} failed.");

return report.Aggregate.Failed == 0 ? 0 : 1;

internal sealed record EvaluationCommandOptions(
    bool ShowHelp,
    bool List,
    string? ScenariosPath,
    string? ScenarioId,
    string OutputDirectory);

internal static class EvaluationCommand
{
    public const string HelpText =
        """
        OpsLattice.AgentEvaluation [--scenarios <path>] [--scenario <id>] [--output <dir>] [--list]

        Runs the airport scenario file against airport-operations-planned and airport-operations.
        Uses the local Ollama settings in appsettings.json.
        Writes evaluation-report.json and evaluation-report.md.
        """;

    public static EvaluationCommandOptions Parse(string[] args)
    {
        string? scenariosPath = null;
        string? scenarioId = null;
        var outputDirectory = Path.GetFullPath("evaluation-results");
        var list = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    return new EvaluationCommandOptions(
                        ShowHelp: true,
                        List: false,
                        ScenariosPath: null,
                        ScenarioId: null,
                        OutputDirectory: outputDirectory);
                case "--list":
                    list = true;
                    break;
                case "--scenarios":
                    scenariosPath = RequireValue(args, ref i, "--scenarios");
                    break;
                case "--scenario":
                    scenarioId = RequireValue(args, ref i, "--scenario");
                    break;
                case "--output":
                    outputDirectory = Path.GetFullPath(
                        RequireValue(args, ref i, "--output"));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown option '{args[i]}'.{Environment.NewLine}{HelpText}");
            }
        }

        return new EvaluationCommandOptions(
            ShowHelp: false,
            List: list,
            ScenariosPath: scenariosPath,
            ScenarioId: scenarioId,
            OutputDirectory: outputDirectory);
    }

    public static string ResolveScenariosPath(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var candidate = Path.GetFullPath(requested);
            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException(
                    $"Scenario file '{candidate}' was not found.",
                    candidate);
            }

            return candidate;
        }

        var besideApplication = Path.Combine(
            AppContext.BaseDirectory,
            "scenarios",
            "airport-operations.json");

        if (File.Exists(besideApplication))
            return besideApplication;

        var currentDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "scenarios",
            "airport-operations.json");

        if (File.Exists(currentDirectory))
            return currentDirectory;

        throw new FileNotFoundException(
            "Could not find scenarios/airport-operations.json. Pass --scenarios <path>.",
            besideApplication);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            throw new InvalidOperationException(
                $"Option {option} requires a value.{Environment.NewLine}{HelpText}");
        }

        index++;
        return args[index];
    }
}
