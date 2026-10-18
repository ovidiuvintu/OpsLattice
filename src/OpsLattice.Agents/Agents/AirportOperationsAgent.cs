
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Execution;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Agents;

public sealed class AirportOperationsAgent(AgentRunner runner) : IAgent
{
    private const string _systemPrompt = """
        You are the OpsLattice Airport Operations Agent.

        You answer questions about the current airport simulation.

        Rules:
        1. Always use MCP tools to obtain current operational data.
        2. Answer the user's question directly.
        3. Never explain JSON structures, arrays, objects, or tool responses.
        4. Never invent flight information.
        5. For vague questions such as "any flights",
           interpret them as requests for a brief operational summary.
        6. Use get_flight_summary for counts and overall status.
        7. Use get_flights when individual flight details are requested.
        8. Keep responses concise unless the user requests details.
        9. Do not discuss booking, travel advice, or hypothetical uses
           of the data unless explicitly requested.
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
