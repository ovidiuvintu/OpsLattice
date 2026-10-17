using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Agents;

public sealed class AirportOperationsAgent(
    ILlmClient llmClient,
    IAgentToolGateway toolGateway)
{
    private const int _maxIterations = 5;

    private const string systemPrompt = """
        You are the OpsLattice Airport Operations Assistant.

        Answer questions about the current airport simulation.

        Use the available tools to retrieve current simulation data.

        Never invent flight statuses, gate assignments,
        or simulation times.

        Do not claim to have changed simulation state.

        If the tools cannot provide the requested information,
        explain what information is missing.
        """;

    public async Task<string> RunAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException(
                "Question cannot be empty.",
                nameof(question));

        var tools = await toolGateway.ListToolsAsync(
            cancellationToken);

        // Read-only allowlist.
        var allowedNames = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "get_flights",
            "get_gates",
            "get_simulation_time"
        };

        var allowedTools = tools
            .Where(t => allowedNames.Contains(t.Name))
            .ToArray();

        var messages = new List<AgentMessage>
        {
            new("system", systemPrompt),
            new("user", question)
        };

        for (var iteration = 0;
             iteration < _maxIterations;
             iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await llmClient.ChatAsync(
                messages,
                allowedTools,
                cancellationToken);

            if (response.ToolCalls.Count == 0)
            {
                return response.Content
                    ?? throw new InvalidOperationException(
                        "LLM returned no final answer.");
            }

            // Preserve the assistant's tool requests.
            messages.Add(new AgentMessage(
                "assistant",
                response.Content,
                response.ToolCalls));

            foreach (var call in response.ToolCalls)
            {
                if (!allowedNames.Contains(call.Name) ||
                    !allowedTools.Any(t => t.Name == call.Name))
                {
                    throw new InvalidOperationException(
                        $"Tool '{call.Name}' is not permitted.");
                }

                var result = await toolGateway.CallToolAsync(
                    call.Name,
                    call.Arguments,
                    cancellationToken);

                messages.Add(new AgentMessage(
                    "tool",
                    result,
                    ToolName: call.Name));
            }
        }

        throw new InvalidOperationException(
            "Agent exceeded the maximum number of iterations.");
    }
}