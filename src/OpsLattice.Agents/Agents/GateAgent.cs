
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Execution;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Agents;

public sealed class GateAgent(AgentRunner runner) : IAgent
{
    private const string _systemPrompt = """
        You are the OpsLattice Airport Gate Assistant.

        Answer questions about airport gate availability,
        assignments, and occupancy using current simulation data.

        Always use an available MCP tool before answering
        questions about current gate operations.

        A gate is available for assignment only when it is
        neither assigned nor occupied.

        Assigned does not necessarily mean occupied.

        Never invent gate states, flight assignments, or counts.
        Do not change simulation state.
        """;

    private static readonly string[] _allowedTools =
    [
        "get_gates",
        "get_gate_summary"
    ];

    public string Name => "gate";

    public Task<AgentExecutionResult> RunAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        return runner.RunAsync(
            Name,
            _systemPrompt,
            _allowedTools,
            question,
            cancellationToken);
    }
}
