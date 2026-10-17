using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Abstractions;

public interface ILlmClient
{
    Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    Task<AgentResponse> ChatAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentTool> tools,
        CancellationToken cancellationToken = default);
}