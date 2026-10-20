namespace OpsLattice.Agents.Models;

public sealed record AgentMessage(
    string Role,
    string? Content,
    IReadOnlyList<AgentToolCall>? ToolCalls = null,
    string? ToolName = null);

public sealed record AgentToolCall(
    string Name,
    IReadOnlyDictionary<string, object?> Arguments);

public sealed record AgentTool(
    string Name,
    string Description,
    object Parameters);

public sealed record AgentToolCallResult(
    bool IsSuccess,
    string Content);

public sealed record AgentResponse(
    string? Content,
    IReadOnlyList<AgentToolCall> ToolCalls,
    AgentResponseType Type = AgentResponseType.Answer);
