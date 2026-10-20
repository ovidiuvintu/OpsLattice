using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Orchestration;

public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IReadOnlyDictionary<string, IAgent> _agents;

    public AgentOrchestrator(IEnumerable<IAgent> agents)
    {
        _agents = agents.ToDictionary(
            agent => agent.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<AgentExecutionResult> ExecuteAsync(
        string agentName,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentName, out var agent))
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' is not registered.");
        }

        return agent.RunAsync(question, cancellationToken);
    }
}
