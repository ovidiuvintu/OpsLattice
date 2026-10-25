namespace OpsLattice.AgentEvaluation.Evaluation;

public static class ExpectationKinds
{
    public const string Results = "results";

    public const string Failure = "failure";
}

public static class EvaluationAgents
{
    public static readonly IReadOnlyList<string> Names =
    [
        "airport-operations-planned",
        "airport-operations"
    ];
}

public static class ControlledSimulation
{
    public const int FlightCount = 100;

    public const int RandomSeed = 42;

    public static readonly DateOnly ScheduleDate = new(2026, 10, 6);
}

public sealed class EvaluationScenario
{
    public string Id { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public ScenarioExpectation Expectation { get; init; } = new();
}

public sealed class ScenarioExpectation
{
    public string Kind { get; init; } = string.Empty;

    public IReadOnlyList<ExpectedToolCall> Calls { get; init; } = [];
}

public sealed class ExpectedToolCall
{
    public string Tool { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Arguments { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string Result { get; init; } = string.Empty;
}

public sealed record CapturedToolCall(
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments,
    string Result,
    long DurationMs);

public sealed record ScenarioEvaluation(
    string ScenarioId,
    string Question,
    string AgentName,
    string Answer,
    int LlmInvocationCount,
    int McpInvocationCount,
    long TotalDurationMs,
    IReadOnlyList<CapturedToolCall> ToolCalls,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> ExecutionErrors,
    bool Passed,
    IReadOnlyList<string> AssertionFailures);

public sealed record AgentAggregate(
    string AgentName,
    int Passed,
    int Failed,
    int LlmInvocationCount,
    int McpInvocationCount,
    long TotalDurationMs);

public sealed record AggregateMetrics(
    int ScenarioCount,
    int RunCount,
    int Passed,
    int Failed,
    IReadOnlyList<AgentAggregate> Agents);

public sealed record SimulationDescriptor(
    int FlightCount,
    string ScheduleDate,
    int RandomSeed);

public sealed record EvaluationReport(
    DateTimeOffset GeneratedAt,
    string OllamaModel,
    string OllamaBaseUrl,
    SimulationDescriptor Simulation,
    IReadOnlyList<ScenarioEvaluation> Results,
    AggregateMetrics Aggregate);
