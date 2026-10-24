
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Orchestration;

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
        var hasSuccessfulToolExecution = false;
        var successfulToolCallCache =
            new Dictionary<string, AgentToolCallResult>(StringComparer.Ordinal);

        var allowedNames = new HashSet<string>(
            allowedToolNames, StringComparer.Ordinal);

        var tools = await toolGateway.ListToolsAsync(cancellationToken);

        var allowedTools = tools
            .Where(tool => allowedNames.Contains(tool.Name))
            .ToArray();

        logger.LogInformation(
            "Agent {AgentName}: Available MCP tools: {Tools}",
            agentName,
            string.Join(", ", tools.Select(t => t.Name)));

        logger.LogInformation(
            "Agent {AgentName}: Allowed MCP tools: {Tools}",
            agentName,
            string.Join(", ", allowedTools.Select(t => t.Name)));

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
                if (!hasSuccessfulToolExecution &&
                    response.Type != AgentResponseType.Clarification)
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

                logger.LogInformation(
                    "Calling MCP tool {ToolName} with arguments {@Arguments}",
                    call.Name,
                    call.Arguments);
                AgentToolCallResult toolResult;
                long toolDurationMs;

                var callCacheKey = BuildToolCallCacheKey(
                    call.Name,
                    call.Arguments);

                if (successfulToolCallCache.TryGetValue(
                        callCacheKey,
                        out var cachedToolResult))
                {
                    toolResult = cachedToolResult;
                    toolDurationMs = 0;
                }
                else
                {
                    var toolStopwatch = Stopwatch.StartNew();

                    var argumentError = ValidateFlightCountDirection(
                        question,
                        call.Name,
                        call.Arguments);

                    if (argumentError is not null)
                    {
                        toolResult = new AgentToolCallResult(
                            IsSuccess: false,
                            Content: argumentError);
                    }
                    else
                    {
                        toolResult = await toolGateway.CallToolAsync(
                            call.Name,
                            call.Arguments,
                            cancellationToken);

                        if (toolResult.IsSuccess)
                        {
                            successfulToolCallCache[callCacheKey] = toolResult;
                        }
                    }

                    toolStopwatch.Stop();
                    toolDurationMs = toolStopwatch.ElapsedMilliseconds;
                }

                toolExecutions.Add(new AgentToolExecution(
                    ToolName: call.Name,
                    Arguments: call.Arguments,
                    Result: toolResult.Content,
                    DurationMs: toolDurationMs));

                if (toolResult.IsSuccess)
                {
                    hasSuccessfulToolExecution = true;
                }

                logger.LogInformation(
                    "Agent {AgentName} ({ExecutionId}): MCP tool {ToolName} completed in {DurationMs} ms",
                    agentName,
                    executionId,
                    call.Name,
                    toolDurationMs);

                messages.Add(new AgentMessage(
                    "tool",
                    toolResult.Content,
                    ToolName: call.Name));
            }
        }

        throw new InvalidOperationException(
            $"Agent '{agentName}' exceeded the maximum number of iterations.");
    }

    private static string? ValidateFlightCountDirection(
        string question,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments)
    {
        if (!string.Equals(
                toolName,
                "get_flight_count",
                StringComparison.Ordinal))
        {
            return null;
        }

        var flightType = GetStringArgument(arguments, "flightType");
        var origin = GetStringArgument(arguments, "origin");
        var destination = GetStringArgument(arguments, "destination");

        if (string.Equals(
                flightType,
                "Arrival",
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(origin)
            && string.IsNullOrWhiteSpace(destination)
            && MentionsAirportWithToken(question, origin, "at", "to"))
        {
            return $"For arrival counts at {origin}, use destination={origin}. origin={origin} with flightType=Arrival is likely reversed.";
        }

        if (string.Equals(
                flightType,
                "Departure",
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(destination)
            && string.IsNullOrWhiteSpace(origin)
            && MentionsAirportWithToken(question, destination, "at", "from"))
        {
            return $"For departure counts at {destination}, use origin={destination}. destination={destination} with flightType=Departure is likely reversed.";
        }

        return null;
    }

    private static string? GetStringArgument(
        IReadOnlyDictionary<string, object?> arguments,
        string key)
    {
        if (!arguments.TryGetValue(key, out var value) || value is null)
            return null;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? null : text;

        if (value is System.Text.Json.JsonElement element)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var textValue = element.GetString();
                return string.IsNullOrWhiteSpace(textValue) ? null : textValue;
            }

            if (element.ValueKind is System.Text.Json.JsonValueKind.Null
                or System.Text.Json.JsonValueKind.Undefined)
            {
                return null;
            }
        }

        var converted = value.ToString();
        return string.IsNullOrWhiteSpace(converted) ? null : converted;
    }

    private static bool MentionsAirportWithToken(
        string question,
        string airportCode,
        params string[] tokens)
    {
        var escapedCode = Regex.Escape(airportCode);
        var tokenPattern = string.Join("|", tokens.Select(Regex.Escape));
        var pattern = $@"\b(?:{tokenPattern})\s+{escapedCode}\b";

        return Regex.IsMatch(
            question,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string BuildToolCallCacheKey(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var orderedArguments = arguments
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);

        return $"{toolName}:{JsonSerializer.Serialize(orderedArguments)}";
    }
}
