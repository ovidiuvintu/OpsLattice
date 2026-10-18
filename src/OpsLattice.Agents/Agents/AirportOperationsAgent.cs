using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Agents;

public sealed class AirportOperationsAgent(
    ILlmClient llmClient,
    IAgentToolGateway toolGateway,
    ILogger<AirportOperationsAgent> logger)
{
    private const int _maxIterations = 5;

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
    public async Task<AgentExecutionResult> RunAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException(
                "Question cannot be empty.",
                nameof(question));

        var executionId = Guid.NewGuid().ToString("N");
        var totalStopwatch = Stopwatch.StartNew();

        var llmExecutions = new List<AgentLlmExecution>();
        var toolExecutions = new List<AgentToolExecution>();

        var executions = new List<AgentToolExecution>();

        var tools = await toolGateway.ListToolsAsync(
            cancellationToken);

        // Read-only allowlist.
        var allowedNames = new HashSet<string>(
            StringComparer.Ordinal)
        {
            "get_flight_summary",
            "get_flights",
            "get_gates",
            "get_simulation_time"
        };

        var allowedTools = tools
            .Where(t => allowedNames.Contains(t.Name))
            .ToArray();

        var messages = new List<AgentMessage>
        {
            new("system", _systemPrompt),
            new("user", question)
        };


        for (var iteration = 0;
             iteration < _maxIterations;
             iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Execute the LLM and measure inference time
            var llmStopwatch = Stopwatch.StartNew();

            var response = await llmClient.ChatAsync(
                messages,
                allowedTools,
                cancellationToken);

            llmStopwatch.Stop();

            // 2. Record LLM execution
            var requestedToolNames = response.ToolCalls
                .Select(call => call.Name)
                .ToArray();

            llmExecutions.Add(new AgentLlmExecution(
                Iteration: iteration + 1,
                DurationMs: llmStopwatch.ElapsedMilliseconds,
                RequestedTools: requestedToolNames.Length > 0,
                RequestedToolNames: requestedToolNames
            ));

            logger.LogInformation(
                "Agent {ExecutionId}: LLM iteration {Iteration} completed in {DurationMs} ms. Tools: {ToolNames}",
                executionId,
                iteration + 1,
                llmStopwatch.ElapsedMilliseconds,
                string.Join(", ", requestedToolNames)
            );

            // 3. Check whether the LLM produced a final answer
            if (response.ToolCalls.Count == 0)
            {
                // Current operational answers require a tool execution.
                if (toolExecutions.Count == 0)
                {
                    logger.LogWarning(
                        "Agent {ExecutionId} attempted to answer without executing an MCP tool",
                        executionId
                    );

                    throw new InvalidOperationException(
                        "The agent must execute an MCP tool before answering " +
                        "questions about current airport operations.");
                }

                totalStopwatch.Stop();

                logger.LogInformation(
                    "Agent {ExecutionId} completed in {TotalDurationMs} ms",
                    executionId,
                    totalStopwatch.ElapsedMilliseconds
                );

                return new AgentExecutionResult(
                    ExecutionId: executionId,
                    Answer: response.Content ?? "",
                    TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                    LlmExecutions: llmExecutions,
                    ToolExecutions: toolExecutions
                );
            }

            // 4. Preserve the assistant's tool requests
            messages.Add(new AgentMessage(
                "assistant",
                response.Content,
                response.ToolCalls
            ));

            // 5. Execute each requested MCP tool
            foreach (var call in response.ToolCalls)
            {
                if (!allowedNames.Contains(call.Name) ||
                    !allowedTools.Any(t => t.Name == call.Name))
                {
                    throw new InvalidOperationException(
                        $"Tool '{call.Name}' is not permitted.");
                }

                var toolStopwatch = Stopwatch.StartNew();

                var result = await toolGateway.CallToolAsync(
                    call.Name,
                    call.Arguments,
                    cancellationToken);

                toolStopwatch.Stop();

                toolExecutions.Add(new AgentToolExecution(
                    ToolName: call.Name,
                    Arguments: call.Arguments,
                    Result: result,
                    DurationMs: toolStopwatch.ElapsedMilliseconds
                ));

                logger.LogInformation(
                    "Agent {ExecutionId}: MCP tool {ToolName} completed in {DurationMs} ms",
                    executionId,
                    call.Name,
                    toolStopwatch.ElapsedMilliseconds
                );

                // 6. Send the tool result back to the LLM
                messages.Add(new AgentMessage(
                    "tool",
                    result,
                    ToolName: call.Name
                ));
            }
        }


        throw new InvalidOperationException(
            "Agent exceeded the maximum number of iterations.");
    }
}