using System.Globalization;
using System.Text.RegularExpressions;

namespace OpsLattice.Agents.Planning;

public sealed class AirportQueryValidator : IAirportQueryValidator
{
    private static readonly HashSet<string> _relativeDates =
    [
        "today",
        "tomorrow",
        "yesterday"
    ];

    private static readonly Regex _airportCodeRegex = new(
        "^[A-Za-z]{3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _isoDateRegex = new(
        "\\b\\d{4}-\\d{2}-\\d{2}\\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _fullMonthDayYearRegex = new(
        "\\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\\s+\\d{1,2},\\s+\\d{4}\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _monthDayWithoutYearRegex = new(
        "\\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\\s+\\d{1,2}(?!,\\s*\\d{4})\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Task<AirportQueryValidationResult> ValidateAsync(
        string question,
        AirportQueryPlanDraft? draft,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (draft?.Queries is null || draft.Queries.Count == 0)
        {
            return Task.FromResult(
                AirportQueryValidationResult.Invalid(
                    ["Plan must contain at least one query."]));
        }

        var missingYearClarification = TryBuildMissingYearClarification(question);
        if (missingYearClarification is not null)
        {
            return Task.FromResult(
                AirportQueryValidationResult.ClarificationNeeded(
                    missingYearClarification));
        }

        var errors = new List<string>();
        var validatedQueries = new List<AirportQuery>();

        for (var i = 0; i < draft.Queries.Count; i++)
        {
            var query = draft.Queries[i];
            var prefix = $"Query {i + 1}";

            if (!TryParseOperation(query.Operation, out var operation))
            {
                errors.Add($"{prefix}: unsupported operation '{query.Operation ?? "<null>"}'.");
                continue;
            }

            if (!TryParseFlightType(query.FlightType, out var flightType))
            {
                errors.Add($"{prefix}: invalid flightType '{query.FlightType}'.");
                continue;
            }

            if (!TryParseFlightStatus(query.Status, out var status))
            {
                errors.Add($"{prefix}: invalid status '{query.Status}'.");
                continue;
            }

            if (!IsValidAirportCode(query.Origin))
            {
                errors.Add($"{prefix}: origin must be a 3-letter IATA code.");
                continue;
            }

            if (!IsValidAirportCode(query.Destination))
            {
                errors.Add($"{prefix}: destination must be a 3-letter IATA code.");
                continue;
            }

            if (!TryParseDate(query.Date, out var parsedDate, out var dateKeyword, out var dateError))
            {
                errors.Add($"{prefix}: {dateError}");
                continue;
            }

            if (operation is AirportQueryOperation.FlightSummary or AirportQueryOperation.GetSimulationTime)
            {
                if (flightType is not null || status is not null || parsedDate is not null || dateKeyword is not null ||
                    query.Origin is not null || query.Destination is not null)
                {
                    errors.Add($"{prefix}: {operation} does not support flight filters.");
                    continue;
                }
            }

            var normalizedOrigin = NormalizeCode(query.Origin);
            var normalizedDestination = NormalizeCode(query.Destination);

            validatedQueries.Add(new AirportQuery(
                Operation: operation,
                FlightType: flightType,
                Status: status,
                Origin: normalizedOrigin,
                Destination: normalizedDestination,
                Date: parsedDate,
                DateKeyword: dateKeyword));
        }

        ValidateCriticalConstraints(question, validatedQueries, errors);

        if (errors.Count > 0)
        {
            return Task.FromResult(AirportQueryValidationResult.Invalid(errors));
        }

        return Task.FromResult(AirportQueryValidationResult.Success(
            new AirportQueryPlan(validatedQueries)));
    }

    private static string? TryBuildMissingYearClarification(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var hasMonthDayWithoutYear = _monthDayWithoutYearRegex.IsMatch(question);
        var hasFullDate = _fullMonthDayYearRegex.IsMatch(question)
            || _isoDateRegex.IsMatch(question);

        if (hasMonthDayWithoutYear && !hasFullDate)
        {
            return "Which year do you mean for that date?";
        }

        return null;
    }

    private static void ValidateCriticalConstraints(
        string question,
        IReadOnlyList<AirportQuery> queries,
        ICollection<string> errors)
    {
        // This is a lightweight semantic consistency pass based on explicit
        // lexical cues. It helps catch obvious plan drift, but it does not
        // guarantee correct natural-language interpretation.
        var lowerQuestion = question.ToLowerInvariant();

        if (lowerQuestion.Contains("arrivals") || lowerQuestion.Contains("arrival"))
        {
            if (!queries.Any(q => q.FlightType == AirportFlightType.Arrival))
            {
                errors.Add("Plan does not preserve the requested arrival direction.");
            }
        }

        if (lowerQuestion.Contains("departures") || lowerQuestion.Contains("departure"))
        {
            if (!queries.Any(q => q.FlightType == AirportFlightType.Departure))
            {
                errors.Add("Plan does not preserve the requested departure direction.");
            }
        }

        if ((lowerQuestion.Contains("arrivals and departures") ||
             lowerQuestion.Contains("arrival and departure")) &&
            queries.Count < 2)
        {
            errors.Add("Plan does not preserve the multi-part arrivals/departures request.");
        }

        var explicitDate = ExtractExplicitDate(question);
        if (explicitDate.HasValue && !queries.Any(q => q.Date == explicitDate.Value))
        {
            errors.Add("Plan does not preserve the explicit date requested by the user.");
        }
    }

    private static DateOnly? ExtractExplicitDate(string question)
    {
        var isoMatch = _isoDateRegex.Match(question);
        if (isoMatch.Success &&
            DateOnly.TryParseExact(
                isoMatch.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var isoDate))
        {
            return isoDate;
        }

        var monthDateYearMatch = _fullMonthDayYearRegex.Match(question);
        if (monthDateYearMatch.Success &&
            DateOnly.TryParse(
                monthDateYearMatch.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var fullDate))
        {
            return fullDate;
        }

        return null;
    }

    private static bool TryParseOperation(
        string? raw,
        out AirportQueryOperation operation)
    {
        return Enum.TryParse(raw, ignoreCase: true, out operation);
    }

    private static bool TryParseFlightType(
        string? raw,
        out AirportFlightType? flightType)
    {
        flightType = null;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (Enum.TryParse<AirportFlightType>(raw, true, out var value))
        {
            flightType = value;
            return true;
        }

        return false;
    }

    private static bool TryParseFlightStatus(
        string? raw,
        out AirportFlightStatus? status)
    {
        status = null;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (Enum.TryParse<AirportFlightStatus>(raw, true, out var value))
        {
            status = value;
            return true;
        }

        return false;
    }

    private static bool TryParseDate(
        string? raw,
        out DateOnly? date,
        out string? dateKeyword,
        out string? error)
    {
        date = null;
        dateKeyword = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var trimmed = raw.Trim();

        if (_relativeDates.Contains(trimmed.ToLowerInvariant()))
        {
            dateKeyword = trimmed.ToLowerInvariant();
            return true;
        }

        if (DateOnly.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            date = parsed;
            return true;
        }

        error = "date must be yyyy-MM-dd or one of today/tomorrow/yesterday.";
        return false;
    }

    private static bool IsValidAirportCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code)
            || _airportCodeRegex.IsMatch(code);
    }

    private static string? NormalizeCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }
}
