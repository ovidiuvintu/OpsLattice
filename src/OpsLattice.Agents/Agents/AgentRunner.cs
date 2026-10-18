
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Execution;

public sealed class AgentRunner(
    ILlmClient llmClient,
    IAgentToolGateway toolGateway,
    ILogger<AgentRunner> logger)
{
    private const int _maxIterations = 5;

    public async Task<AgentExecutionResult> RunAsync(
        string agentName,
        string systemPrompt,
        IReadOnlyCollection<string> allowedToolNames,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException(
                "Question cannot be empty.", nameof(question));

        var executionId = Guid.NewGuid().ToString("N");
        var totalStopwatch = Stopwatch.StartNew();

        var llmExecutions = new List<AgentLlmExecution>();
        var toolExecutions = new List<AgentToolExecution>();

        var allowedNames = new HashSet<string>(
            allowedToolNames, StringComparer.Ordinal);

        var tools = await toolGateway.ListToolsAsync(cancellationToken);

        var allowedTools = tools
            .Where(tool => allowedNames.Contains(tool.Name))
            .ToArray();

        var messages = new List<AgentMessage>
        {
            new("system", systemPrompt),
            new("user", question)
        };

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var llmStopwatch = Stopwatch.StartNew();

            var response = await llmClient.ChatAsync(
                messages, allowedTools, cancellationToken);

            llmStopwatch.Stop();

            var requestedToolNames = response.ToolCalls
                .Select(call => call.Name)
                .ToArray();

            llmExecutions.Add(new AgentLlmExecution(
                Iteration: iteration + 1,
                DurationMs: llmStopwatch.ElapsedMilliseconds,
                RequestedTools: requestedToolNames.Length > 0,
                RequestedToolNames: requestedToolNames));

            logger.LogInformation(
                "Agent {AgentName} ({ExecutionId}): LLM iteration {Iteration} completed in {DurationMs} ms. Tools: {ToolNames}",
                agentName,
                executionId,
                iteration + 1,
                llmStopwatch.ElapsedMilliseconds,
                string.Join(", ", requestedToolNames));

            if (response.ToolCalls.Count == 0)
            {
                if (toolExecutions.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Agent '{agentName}' must execute an MCP tool before answering.");
                }

                totalStopwatch.Stop();

                return new AgentExecutionResult(
                    ExecutionId: executionId,
                    Answer: response.Content ?? "",
                    TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                    LlmExecutions: llmExecutions,
                    ToolExecutions: toolExecutions);
            }

            messages.Add(new AgentMessage(
                "assistant",
                response.Content,
                response.ToolCalls));

            foreach (var call in response.ToolCalls)
            {
                if (!allowedNames.Contains(call.Name) ||
                    !allowedTools.Any(tool => tool.Name == call.Name))
                {
                    throw new InvalidOperationException(
                        $"Tool '{call.Name}' is not permitted for agent '{agentName}'.");
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
                    DurationMs: toolStopwatch.ElapsedMilliseconds));

                logger.LogInformation(
                    "Agent {AgentName} ({ExecutionId}): MCP tool {ToolName} completed in {DurationMs} ms",
                    agentName,
                    executionId,
                    call.Name,
                    toolStopwatch.ElapsedMilliseconds);

                messages.Add(new AgentMessage(
                    "tool",
                    result,
                    ToolName: call.Name));
            }
        }

        throw new InvalidOperationException(
            $"Agent '{agentName}' exceeded the maximum number of iterations.");
    }
}
