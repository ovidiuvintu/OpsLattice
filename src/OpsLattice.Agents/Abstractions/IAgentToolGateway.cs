using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Abstractions;

public interface IAgentToolGateway
{
    Task<IReadOnlyList<AgentTool>> ListToolsAsync(
        CancellationToken cancellationToken = default);

    Task<AgentToolCallResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}
