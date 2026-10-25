
using System.Diagnostics;
using System.Globalization;
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
        string? lastAssistantContent = null;
        var detailsRepromptSent = false;
        var clarificationRepromptSent = false;
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

        var deterministicResult = await TryRunDeterministicAirportCountAsync(
            agentName,
            question,
            executionId,
            totalStopwatch,
            llmExecutions,
            toolExecutions,
            cancellationToken);

        if (deterministicResult is not null)
            return deterministicResult;

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

            lastAssistantContent = response.Content;

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
                if (response.Type == AgentResponseType.Clarification
                    && string.Equals(agentName, "airport-operations", StringComparison.Ordinal)
                    && IsInvalidRelativeDateClarification(question, response.Content))
                {
                    messages.Add(new AgentMessage(
                        "assistant",
                        response.Content));

                    messages.Add(new AgentMessage(
                        "user",
                        "Relative dates like today, tomorrow, and yesterday are complete and must not trigger year clarification. Call get_simulation_time, resolve the relative date, execute required flight tools, and answer."));

                    continue;
                }

                if (!clarificationRepromptSent
                    && !hasSuccessfulToolExecution
                    && string.Equals(agentName, "airport-operations", StringComparison.Ordinal)
                    && IsUnnecessaryClarification(question, response))
                {
                    clarificationRepromptSent = true;

                    messages.Add(new AgentMessage(
                        "assistant",
                        response.Content));

                    messages.Add(new AgentMessage(
                        "user",
                        "Do not ask me a question; my question already has what you need. A date with a month, day and year is complete. The airport is the active simulation airport. Only filter by status if I asked about status. Call the appropriate tools now and answer."));

                    continue;
                }

                if (!hasSuccessfulToolExecution &&
                    response.Type != AgentResponseType.Clarification)
                {
                    if (string.Equals(agentName, "airport-operations", StringComparison.Ordinal))
                    {
                        messages.Add(new AgentMessage(
                            "assistant",
                            response.Content));

                        messages.Add(new AgentMessage(
                            "user",
                            "You must call at least one MCP tool before giving an operational answer. For relative dates (today/tomorrow/yesterday), call get_simulation_time first, then call the appropriate flight tool with a resolved date."));

                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Agent '{agentName}' must execute an MCP tool before answering.");
                }

                if (!detailsRepromptSent
                    && hasSuccessfulToolExecution
                    && string.Equals(agentName, "airport-operations", StringComparison.Ordinal)
                    && IsMissingRequestedFlightDetails(question, response.Content, toolExecutions))
                {
                    detailsRepromptSent = true;

                    messages.Add(new AgentMessage(
                        "assistant",
                        response.Content));

                    messages.Add(new AgentMessage(
                        "user",
                        "I already asked for the flight details, so do not ask whether I want them. Call search_flights now with the same filters you used for the count, then give the count and the flight details in one answer."));

                    continue;
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

        if (string.Equals(agentName, "airport-operations", StringComparison.Ordinal))
        {
            totalStopwatch.Stop();

            var fallbackAnswer = !string.IsNullOrWhiteSpace(lastAssistantContent)
                ? $"I couldn't complete the request because required MCP tool calls were not completed after multiple attempts. Last model response: {lastAssistantContent}"
                : "I couldn't complete the request because required MCP tool calls were not completed after multiple attempts. Please retry.";

            return new AgentExecutionResult(
                ExecutionId: executionId,
                Answer: fallbackAnswer,
                TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                LlmExecutions: llmExecutions,
                ToolExecutions: toolExecutions);
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

    // The airport agent only ever needs to ask one thing: the year, when the
    // user gives a month and day without one. Any other question back to the
    // user (which airport, which status, which year for a complete date) is
    // pushed back once so the model answers with tools instead.
    private static bool IsUnnecessaryClarification(
        string question,
        AgentResponse response)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        var content = response.Content ?? string.Empty;

        var asksUser = response.Type == AgentResponseType.Clarification
            || content.Contains('?', StringComparison.Ordinal);

        if (!asksUser)
            return false;

        var hasMonthDayWithoutYear = Regex.IsMatch(
            question,
            @"\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2}(?:st|nd|rd|th)?\b(?!,?\s*\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return !hasMonthDayWithoutYear;
    }

    // True when the user asked to see flights (list/show/details) but the model
    // stopped at a count or offered to list them instead of calling search_flights.
    private static bool IsMissingRequestedFlightDetails(
        string question,
        string? answer,
        IEnumerable<AgentToolExecution> toolExecutions)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        var asksForDetails = Regex.IsMatch(
            question,
            @"\b(list|details?|show|which\s+flights|what\s+flights)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!asksForDetails)
            return false;

        if (toolExecutions.Any(execution =>
                string.Equals(execution.ToolName, "search_flights", StringComparison.Ordinal)))
        {
            return false;
        }

        var text = answer ?? string.Empty;

        var offersInsteadOfAnswering = Regex.IsMatch(
            text,
            @"\b(would\s+you\s+like|do\s+you\s+want|shall\s+i|should\s+i|let\s+me\s+know\s+if|if\s+you(?:'d|\s+would)\s+like|i\s+can\s+(?:list|provide|show)|need\s+to\s+call)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var listsFlightNumbers = Regex.IsMatch(
            text,
            @"\b[A-Z0-9]{2}\d{3,4}\b",
            RegexOptions.CultureInvariant);

        return offersInsteadOfAnswering || !listsFlightNumbers;
    }

    private static bool IsInvalidRelativeDateClarification(
        string question,
        string? clarification)
    {
        if (string.IsNullOrWhiteSpace(question)
            || string.IsNullOrWhiteSpace(clarification))
        {
            return false;
        }

        var asksRelativeDate = Regex.IsMatch(
            question,
            @"\b(today|tomorrow|yesterday)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!asksRelativeDate)
            return false;

        return Regex.IsMatch(
            clarification,
            @"\b(year|which\s+year|exact\s+date|full\s+date|which\s+relative\s+date|today|tomorrow|yesterday)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private async Task<AgentExecutionResult?> TryRunDeterministicAirportCountAsync(
        string agentName,
        string question,
        string executionId,
        Stopwatch totalStopwatch,
        IReadOnlyList<AgentLlmExecution> llmExecutions,
        ICollection<AgentToolExecution> toolExecutions,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(agentName, "airport-operations", StringComparison.Ordinal))
            return null;

        var match = Regex.Match(
            question,
            @"^\s*How\s+many\s+flights\s+are\s+scheduled\s+at\s+(?<airport>[A-Za-z]{3})\s+on\s+(?<date>.+?)\?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
            return null;

        var airport = match.Groups["airport"].Value.ToUpperInvariant();
        var rawDate = match.Groups["date"].Value.Trim();

        if (!DateOnly.TryParse(
                rawDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var date))
        {
            return null;
        }

        var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var arrivalsArgs = new Dictionary<string, object?>
        {
            ["destination"] = airport,
            ["flightType"] = "Arrival",
            ["date"] = dateText
        };

        var departuresArgs = new Dictionary<string, object?>
        {
            ["origin"] = airport,
            ["flightType"] = "Departure",
            ["date"] = dateText
        };

        var arrivalsStopwatch = Stopwatch.StartNew();
        var arrivalsResult = await toolGateway.CallToolAsync(
            "get_flight_count",
            arrivalsArgs,
            cancellationToken);
        arrivalsStopwatch.Stop();

        toolExecutions.Add(new AgentToolExecution(
            ToolName: "get_flight_count",
            Arguments: arrivalsArgs,
            Result: arrivalsResult.Content,
            DurationMs: arrivalsStopwatch.ElapsedMilliseconds));

        if (!arrivalsResult.IsSuccess || !TryReadCount(arrivalsResult.Content, out var arrivalCount))
            return null;

        var departuresStopwatch = Stopwatch.StartNew();
        var departuresResult = await toolGateway.CallToolAsync(
            "get_flight_count",
            departuresArgs,
            cancellationToken);
        departuresStopwatch.Stop();

        toolExecutions.Add(new AgentToolExecution(
            ToolName: "get_flight_count",
            Arguments: departuresArgs,
            Result: departuresResult.Content,
            DurationMs: departuresStopwatch.ElapsedMilliseconds));

        if (!departuresResult.IsSuccess || !TryReadCount(departuresResult.Content, out var departureCount))
            return null;

        var total = arrivalCount + departureCount;

        totalStopwatch.Stop();

        return new AgentExecutionResult(
            ExecutionId: executionId,
            Answer: $"There are {total} flights scheduled at {airport} on {date:MMMM d, yyyy} (arrivals: {arrivalCount}, departures: {departureCount}).",
            TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
            LlmExecutions: llmExecutions,
            ToolExecutions: toolExecutions.ToArray());
    }

    private static bool TryReadCount(
        string content,
        out int count)
    {
        if (int.TryParse(
                content,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out count))
        {
            return true;
        }

        if (!TryParseJsonInteger(content, out count))
        {
            count = 0;
            return false;
        }

        return true;
    }

    private static bool TryParseJsonInteger(
        string content,
        out int count)
    {
        count = 0;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Number
                && doc.RootElement.TryGetInt32(out var value))
            {
                count = value;
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
