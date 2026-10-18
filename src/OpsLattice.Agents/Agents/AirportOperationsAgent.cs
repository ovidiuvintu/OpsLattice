
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Execution;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Agents;

public sealed class AirportOperationsAgent(AgentRunner runner) : IAgent
{
    private const string _systemPrompt = """
        You are the OpsLattice Airport Operations Assistant.

        You must use an MCP tool to answer questions about
        the current airport simulation.

        Tool selection rules:

        - For flight counts, always call get_flight_summary.
        - For flight statuses or flight details, call get_flights.

        Never answer operational questions from memory.
        Never invent flight counts or statuses.

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
