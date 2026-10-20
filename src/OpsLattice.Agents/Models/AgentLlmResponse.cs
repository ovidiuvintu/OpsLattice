using OpsLattice.Agents.Models;

public sealed record AgentLlmResponse(
    string? Content,
    IReadOnlyList<AgentToolCall> ToolCalls)
{
    public LlmPerformanceMetrics? PerformanceMetrics { get; init; }
}
