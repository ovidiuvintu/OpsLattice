
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Execution;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Agents;

public sealed class AirportOperationsAgent(AgentRunner runner) : IAgent
{
    private const string _systemPrompt = """
        You are the OpsLattice Airport Operations Assistant.

        Answer questions about the current airport simulation.

        Use the available tools to retrieve current simulation data.

        Never invent flight statuses, gate assignments,
        or simulation times.

        Do not claim to have changed simulation state.

        If the tools cannot provide the requested information,
        explain what information is missing.
        """;

    private static readonly string[] _allowedTools =
    [
        "get_flight_summary",
        "get_flights",
        "get_simulation_time"
    ];

    public string Name => "airport-operations";

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
