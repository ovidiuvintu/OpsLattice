using System.Globalization;
using System.Text.Json;

namespace OpsLattice.Agents.Planning;

public sealed class AirportAnswerFormatter : IAirportAnswerFormatter
{
    public string Format(
        string question,
        AirportQueryExecutionResult executionResult)
    {
        if (executionResult.Items.Count == 0)
            return "No query results were produced.";

        var lines = executionResult.Items
            .OrderBy(item => item.QueryIndex)
            .Select(FormatItem)
            .ToArray();

        if (lines.Length == 1)
            return lines[0];

        return string.Join(
            Environment.NewLine,
            lines.Select((line, index) => $"{index + 1}. {line}"));
    }

    private static string FormatItem(AirportQueryExecutionItem item)
    {
        var scope = BuildScope(item.Query);

        if (!item.IsSuccess)
        {
            return scope.Length == 0
                ? $"{item.Query.Operation}: failed - {item.Result}"
                : $"{item.Query.Operation} ({scope}): failed - {item.Result}";
        }

        return item.Query.Operation switch
        {
            AirportQueryOperation.CountFlights => FormatCount(item, scope),
            AirportQueryOperation.ListFlights => FormatFlights(item, scope),
            AirportQueryOperation.FlightSummary => FormatSummary(item),
            AirportQueryOperation.GetSimulationTime => FormatSimulationTime(item),
            _ => $"{item.Query.Operation}: unsupported operation"
        };
    }

    private static string FormatCount(
        AirportQueryExecutionItem item,
        string scope)
    {
        if (!TryReadInt(item.Result, out var count))
            return $"CountFlights ({scope}): result format was not numeric.";

        return scope.Length == 0
            ? $"Flight count: {count}."
            : $"Flight count ({scope}): {count}.";
    }

    private static string FormatFlights(
        AirportQueryExecutionItem item,
        string scope)
    {
        if (!TryParseJson(item.Result, out var element) ||
            element.ValueKind != JsonValueKind.Array)
        {
            return scope.Length == 0
                ? "Flight list result was not a JSON array."
                : $"Flight list ({scope}) result was not a JSON array.";
        }

        var flights = element.EnumerateArray().ToArray();
        if (flights.Length == 0)
        {
            return scope.Length == 0
                ? "No flights matched."
                : $"No flights matched ({scope}).";
        }

        var preview = flights.Take(5)
            .Select(flight =>
            {
                var number = ReadCaseInsensitiveString(flight, "flightNumber") ?? "unknown";
                var status = ReadCaseInsensitiveString(flight, "status") ?? "unknown";
                return $"{number} ({status})";
            });

        var suffix = flights.Length > 5 ? " (showing first 5)" : string.Empty;

        return scope.Length == 0
            ? $"Flights matched: {flights.Length}{suffix}: {string.Join(", ", preview)}."
            : $"Flights matched ({scope}): {flights.Length}{suffix}: {string.Join(", ", preview)}.";
    }

    private static string FormatSummary(AirportQueryExecutionItem item)
    {
        if (!TryParseJson(item.Result, out var element) ||
            element.ValueKind != JsonValueKind.Object)
        {
            return "Flight summary result format was invalid.";
        }

        var total = ReadCaseInsensitiveInt(element, "total");
        var scheduled = ReadCaseInsensitiveInt(element, "scheduled");

        if (!total.HasValue || !scheduled.HasValue)
            return "Flight summary was missing expected fields.";

        return $"Flight summary: total={total.Value}, scheduled={scheduled.Value}.";
    }

    private static string FormatSimulationTime(AirportQueryExecutionItem item)
    {
        if (TryReadDateTime(item.Result, out var dateTime))
        {
            return $"Simulation time: {dateTime:yyyy-MM-dd HH:mm:ss 'UTC'}.";
        }

        return "Simulation time result format was invalid.";
    }

    private static string BuildScope(AirportQuery query)
    {
        var parts = new List<string>();

        if (query.FlightType.HasValue)
            parts.Add($"flightType={query.FlightType.Value}");

        if (query.Status.HasValue)
            parts.Add($"status={query.Status.Value}");

        if (!string.IsNullOrWhiteSpace(query.Origin))
            parts.Add($"origin={query.Origin}");

        if (!string.IsNullOrWhiteSpace(query.Destination))
            parts.Add($"destination={query.Destination}");

        if (query.Date.HasValue)
            parts.Add($"date={query.Date.Value:yyyy-MM-dd}");
        else if (!string.IsNullOrWhiteSpace(query.DateKeyword))
            parts.Add($"date={query.DateKeyword}");

        return string.Join(", ", parts);
    }

    private static bool TryReadInt(
        string content,
        out int value)
    {
        if (int.TryParse(
                content,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
        {
            return true;
        }

        if (TryParseJson(content, out var json) &&
            json.ValueKind == JsonValueKind.Number &&
            json.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static int? ReadCaseInsensitiveInt(
        JsonElement obj,
        string propertyName)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetInt32(out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadCaseInsensitiveString(
        JsonElement obj,
        string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in obj.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();
        }

        return null;
    }

    private static bool TryReadDateTime(
        string content,
        out DateTimeOffset value)
    {
        if (DateTimeOffset.TryParse(
                content,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value))
        {
            return true;
        }

        if (!TryParseJson(content, out var json))
            return false;

        if (json.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(
                json.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value);
        }

        if (json.ValueKind != JsonValueKind.Object)
            return false;

        var text = ReadCaseInsensitiveString(json, "currentTime");
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
    }

    private static bool TryParseJson(
        string content,
        out JsonElement element)
    {
        element = default;

        if (string.IsNullOrWhiteSpace(content))
            return false;

        try
        {
            using var document = JsonDocument.Parse(content);
            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
