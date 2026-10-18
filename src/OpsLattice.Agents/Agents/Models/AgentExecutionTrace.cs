namespace OpsLattice.Agents.Models;

public sealed record AgentToolExecution(
    string ToolName,
    IReadOnlyDictionary<string, object?> Arguments,
    string Result,
    long DurationMs
);

public sealed record AgentLlmExecution(
    int Iteration,
    long DurationMs,
    bool RequestedTools,
    IReadOnlyList<string> RequestedToolNames
);

public sealed record AgentExecutionResult(
    string ExecutionId,
    string Answer,
    long TotalDurationMs,
    IReadOnlyList<AgentLlmExecution> LlmExecutions,
    IReadOnlyList<AgentToolExecution> ToolExecutions
);
