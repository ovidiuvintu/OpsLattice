using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Abstractions;

public interface IAgentToolGateway
{
    Task<IReadOnlyList<AgentTool>> ListToolsAsync(
        CancellationToken cancellationToken = default);

    Task<string> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}