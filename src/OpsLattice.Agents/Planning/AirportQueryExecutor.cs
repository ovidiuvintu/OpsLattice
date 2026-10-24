using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Planning;

public sealed class AirportQueryExecutor(
    IAgentToolGateway toolGateway) : IAirportQueryExecutor
{
    private const string _flightCountTool = "get_flight_count";
    private const string _flightsTool = "get_flights";
    private const string _flightSummaryTool = "get_flight_summary";
    private const string _simulationTimeTool = "get_simulation_time";

    public async Task<AirportQueryExecutionResult> ExecuteAsync(
        AirportQueryPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var tools = await toolGateway.ListToolsAsync(cancellationToken);
        var toolMap = tools.ToDictionary(
            t => t.Name,
            StringComparer.Ordinal);

        DateOnly? simulationDate = null;

        var items = new List<AirportQueryExecutionItem>();

        for (var i = 0; i < plan.Queries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var query = plan.Queries[i];
            var toolName = GetToolName(query.Operation);

            if (!toolMap.TryGetValue(toolName, out var tool))
            {
                items.Add(new AirportQueryExecutionItem(
                    QueryIndex: i,
                    Query: query,
                    IsSuccess: false,
                    ToolName: toolName,
                    Arguments: new Dictionary<string, object?>(),
                    Result: $"Required MCP tool '{toolName}' is not available.",
                    DurationMs: 0));
                continue;
            }

            var arguments = await BuildArgumentsAsync(
                query,
                toolMap,
                simulationDate,
                cancellationToken);

            if (arguments.Error is not null)
            {
                items.Add(new AirportQueryExecutionItem(
                    QueryIndex: i,
                    Query: query,
                    IsSuccess: false,
                    ToolName: toolName,
                    Arguments: arguments.Values,
                    Result: arguments.Error,
                    DurationMs: 0));
                continue;
            }

            if (query.DateKeyword is not null && arguments.ResolvedDate.HasValue)
                simulationDate = arguments.ResolvedDate.Value;

            var unsupportedKeys = arguments.Values.Keys
                .Where(key => !GetToolParameterNames(tool).Contains(
                    key,
                    StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (unsupportedKeys.Length > 0)
            {
                items.Add(new AirportQueryExecutionItem(
                    QueryIndex: i,
                    Query: query,
                    IsSuccess: false,
                    ToolName: toolName,
                    Arguments: arguments.Values,
                    Result: $"Tool '{toolName}' does not support argument(s): {string.Join(", ", unsupportedKeys)}.",
                    DurationMs: 0));
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            var callResult = await toolGateway.CallToolAsync(
                toolName,
                arguments.Values,
                cancellationToken);
            stopwatch.Stop();

            items.Add(new AirportQueryExecutionItem(
                QueryIndex: i,
                Query: query,
                IsSuccess: callResult.IsSuccess,
                ToolName: toolName,
                Arguments: arguments.Values,
                Result: callResult.Content,
                DurationMs: stopwatch.ElapsedMilliseconds));
        }

        return new AirportQueryExecutionResult(items);
    }

    private async Task<PreparedArguments> BuildArgumentsAsync(
        AirportQuery query,
        IReadOnlyDictionary<string, AgentTool> toolMap,
        DateOnly? simulationDate,
        CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(query.Origin))
            args["origin"] = query.Origin;

        if (!string.IsNullOrWhiteSpace(query.Destination))
            args["destination"] = query.Destination;

        if (query.FlightType.HasValue)
            args["flightType"] = query.FlightType.Value.ToString();

        if (query.Status.HasValue)
            args["status"] = query.Status.Value.ToString();

        if (query.Date.HasValue)
        {
            args["date"] = query.Date.Value.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

            return new PreparedArguments(args, null, null);
        }

        if (query.DateKeyword is null)
            return new PreparedArguments(args, null, null);

        if (simulationDate is null)
        {
            if (!toolMap.ContainsKey(_simulationTimeTool))
            {
                return new PreparedArguments(
                    args,
                    "Cannot resolve relative date because MCP tool 'get_simulation_time' is not available.",
                    null);
            }

            var simulationTimeResult = await toolGateway.CallToolAsync(
                _simulationTimeTool,
                new Dictionary<string, object?>(),
                cancellationToken);

            if (!simulationTimeResult.IsSuccess)
            {
                return new PreparedArguments(
                    args,
                    $"Failed to resolve relative date via '{_simulationTimeTool}': {simulationTimeResult.Content}",
                    null);
            }

            if (!TryParseDate(simulationTimeResult.Content, out var parsed))
            {
                return new PreparedArguments(
                    args,
                    "Unable to parse simulation time returned by MCP tool.",
                    null);
            }

            simulationDate = parsed;
        }

        var resolvedDate = query.DateKeyword.ToLowerInvariant() switch
        {
            "today" => simulationDate.Value,
            "tomorrow" => simulationDate.Value.AddDays(1),
            "yesterday" => simulationDate.Value.AddDays(-1),
            _ => (DateOnly?)null
        };

        if (!resolvedDate.HasValue)
        {
            return new PreparedArguments(
                args,
                $"Unsupported relative date keyword '{query.DateKeyword}'.",
                simulationDate);
        }

        args["date"] = resolvedDate.Value.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        return new PreparedArguments(args, null, simulationDate);
    }

    private static bool TryParseDate(
        string content,
        out DateOnly date)
    {
        date = default;

        if (DateTimeOffset.TryParse(
                content,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dt))
        {
            date = DateOnly.FromDateTime(dt.UtcDateTime);
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    root.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var fromString))
            {
                date = DateOnly.FromDateTime(fromString.UtcDateTime);
                return true;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (!property.Name.Equals("currentTime", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (property.Value.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(
                            property.Value.GetString(),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out var fromObject))
                    {
                        date = DateOnly.FromDateTime(fromObject.UtcDateTime);
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static IEnumerable<string> GetToolParameterNames(AgentTool tool)
    {
        if (tool.Parameters is not JsonElement json ||
            json.ValueKind != JsonValueKind.Object ||
            !json.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        return properties.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private static string GetToolName(AirportQueryOperation operation)
    {
        return operation switch
        {
            AirportQueryOperation.CountFlights => _flightCountTool,
            AirportQueryOperation.ListFlights => _flightsTool,
            AirportQueryOperation.FlightSummary => _flightSummaryTool,
            AirportQueryOperation.GetSimulationTime => _simulationTimeTool,
            _ => throw new NotSupportedException(
                $"Unsupported operation '{operation}'.")
        };
    }

    private sealed record PreparedArguments(
        IReadOnlyDictionary<string, object?> Values,
        string? Error,
        DateOnly? ResolvedDate);
}
