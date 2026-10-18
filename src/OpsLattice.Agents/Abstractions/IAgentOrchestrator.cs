using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Abstractions;

public interface IAgentOrchestrator
{
    Task<AgentExecutionResult> ExecuteAsync(
        string agentName,
        string question,
        CancellationToken cancellationToken = default);
}
