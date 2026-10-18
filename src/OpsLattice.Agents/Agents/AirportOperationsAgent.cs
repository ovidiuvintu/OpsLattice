
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
        4. Never invent flight information or counts.
        5. For vague questions such as "any flights",
           provide a brief operational summary.
        6. Use get_flight_summary only for unfiltered totals
           and overall simulation statistics.
        7. Use get_flight_count when the user requests a count
           filtered by destination, origin, flight type, or date.
        8. Use get_flights when individual flight details are requested.
        9. Airport codes identify destinations and origins.
           For example, DFW is Dallas/Fort Worth International Airport.
        10. Never present an unfiltered total as a filtered count.
        11. Keep responses concise unless details are requested.
        12. Do not discuss booking, travel advice, or hypothetical uses
            of the data unless explicitly requested.
        """;

    private static readonly string[] _allowedTools =
    [
        "get_flight_summary",
        "get_flight_count",
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
