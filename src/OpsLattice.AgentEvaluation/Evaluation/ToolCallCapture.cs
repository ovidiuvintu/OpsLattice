using System.Globalization;
using System.Text.Json;

namespace OpsLattice.AgentEvaluation.Evaluation;

public static class ToolCallCapture
{
    public static IReadOnlyDictionary<string, string> FormatArguments(
        IReadOnlyDictionary<string, object?> arguments)
    {
        var formatted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in arguments)
        {
            var text = FormatValue(pair.Value);
            if (text is null)
                continue;

            formatted[pair.Key] = text;
        }

        return formatted;
    }

    public static string? FormatValue(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return text;
            case JsonElement element:
                return element.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    JsonValueKind.String => element.GetString(),
                    _ => element.ToString()
                };
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                return value.ToString();
        }
    }
}
