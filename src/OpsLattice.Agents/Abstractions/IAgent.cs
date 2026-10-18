using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Abstractions;

public interface IAgent
{
    string Name { get; }

    Task<AgentExecutionResult> RunAsync(
        string question,
        CancellationToken cancellationToken = default);
}
