using System.Globalization;
using System.Text.RegularExpressions;

namespace OpsLattice.Agents.Planning;

public sealed class AirportQueryValidator : IAirportQueryValidator
{
    private const string _primaryAirportCode = "IAH";

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

    // Accepts "October 6, 2026", "October 6 2026" and "October 6th, 2026".
    private static readonly Regex _fullMonthDayYearRegex = new(
        "\\b(?<month>January|February|March|April|May|June|July|August|September|October|November|December)\\s+(?<day>\\d{1,2})(?:st|nd|rd|th)?,?\\s+(?<year>\\d{4})\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _monthDayWithoutYearRegex = new(
        "\\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\\s+\\d{1,2}(?:st|nd|rd|th)?\\b(?!,?\\s*\\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Words that unambiguously ask about a flight's operational status.
    // "scheduled" is excluded because "scheduled on <date>" is date intent.
    private static readonly Regex _statusIntentRegex = new(
        "\\b(?:landed|approaching|departed|delayed|cancell?ed|boarding|at (?:the )?gate|(?:have|has|with) (?:a |the )?status)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _arrivalWordRegex = new(
        "\\barriv\\w*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _departureWordRegex = new(
        "\\bdepart\\w*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _scheduledWordRegex = new(
        "\\bscheduled\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _scheduledForDateRegex = new(
        "\\bscheduled\\s+(?:on|for|at)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex _relativeDateWordRegex = new(
        "\\b(?:today|tomorrow|yesterday)\\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Explicit requests for the Scheduled operational status.
    private static readonly Regex _scheduledStatusRegex = new(
        "\\b(?:status\\s+(?:of\\s+)?scheduled|currently\\s+scheduled|still\\s+scheduled)\\b",
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

        var contradictoryRoute = TryBuildContradictoryRouteRefusal(question);
        if (contradictoryRoute is not null)
        {
            return Task.FromResult(
                AirportQueryValidationResult.ClarificationNeeded(
                    contradictoryRoute));
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
                if (LooksLikeOptionList(query.FlightType))
                {
                    errors.Add($"{prefix}: invalid flightType '{query.FlightType}'. Expected a single value ('Arrival' or 'Departure'), not a pipe-separated options string.");
                }
                else
                {
                    errors.Add($"{prefix}: invalid flightType '{query.FlightType}'.");
                }
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

            if (normalizedOrigin is not null &&
                normalizedDestination is not null &&
                string.Equals(normalizedOrigin, normalizedDestination, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    AirportQueryValidationResult.ClarificationNeeded(
                        $"A flight cannot use {normalizedOrigin} as both origin and destination. That route contradicts itself, so there is no count to return."));
            }

            flightType = AlignFlightTypeWithRoute(
                question,
                flightType,
                normalizedOrigin,
                normalizedDestination);

            (normalizedOrigin, normalizedDestination) = AnchorRouteToPrimaryAirport(
                question,
                flightType,
                normalizedOrigin,
                normalizedDestination);

            var directionError = ValidateDirectionInvariants(
                flightType,
                normalizedOrigin,
                normalizedDestination);

            if (directionError is not null)
            {
                errors.Add($"{prefix}: {directionError}");
                continue;
            }

            validatedQueries.Add(new AirportQuery(
                Operation: operation,
                FlightType: flightType,
                Status: status,
                Origin: normalizedOrigin,
                Destination: normalizedDestination,
                Date: parsedDate,
                DateKeyword: dateKeyword));
        }

        if (errors.Count == 0)
        {
            ValidateCriticalConstraints(question, validatedQueries, errors);
        }

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

        if (AsksForOperationalStatus(question) &&
            !queries.Any(q => q.Status.HasValue))
        {
            errors.Add("Plan does not preserve the requested status filter. Set status on the query instead of dropping it.");
        }

        if (!AsksForOperationalStatus(question) &&
            queries.Any(q => q.Status.HasValue))
        {
            errors.Add("Plan added a status filter the user did not request. Remove status. Do not infer operational status from schedule wording.");
        }

        if (AsksForScheduleDate(question) &&
            queries.Any(q => q.Status == AirportFlightStatus.Scheduled))
        {
            errors.Add("\"scheduled\" with a date refers to the scheduled date, not status=Scheduled. Remove status and keep the date.");
        }

        RequireStatedAirports(question, queries, errors);

        var explicitDate = ExtractExplicitDate(question);
        if (explicitDate.HasValue && !queries.Any(q => q.Date == explicitDate.Value))
        {
            errors.Add("Plan does not preserve the explicit date requested by the user.");
        }
    }

    private static string? TryBuildContradictoryRouteRefusal(string question)
    {
        var clauses = Regex.Split(
            question,
            @"\band\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var clause in clauses)
        {
            var fromCode = ReadAirportCode(clause, @"\bfrom\s+([A-Za-z]{3})\b");
            var toCode = ReadAirportCode(clause, @"\bto\s+([A-Za-z]{3})\b");
            var arrivingAt = ReadAirportCode(clause, @"\barriv\w*\s+at\s+([A-Za-z]{3})\b");

            if (fromCode is not null && toCode is not null &&
                string.Equals(fromCode, toCode, StringComparison.OrdinalIgnoreCase))
            {
                return $"A flight cannot use {fromCode} as both origin and destination. That route contradicts itself, so there is no count to return.";
            }

            if (fromCode is not null && arrivingAt is not null &&
                string.Equals(fromCode, arrivingAt, StringComparison.OrdinalIgnoreCase))
            {
                return $"A flight cannot use {fromCode} as both origin and destination. That route contradicts itself, so there is no count to return.";
            }
        }

        return null;
    }

    private static void RequireStatedAirports(
        string question,
        IReadOnlyList<AirportQuery> queries,
        ICollection<string> errors)
    {
        RequireAirportRole(
            question,
            queries,
            errors,
            @"\bfrom\s+([A-Za-z]{3})\b",
            asOrigin: true);

        RequireAirportRole(
            question,
            queries,
            errors,
            @"\bdepart(?:ing|ure|ures)?\s+([A-Za-z]{3})\b",
            asOrigin: true);

        RequireAirportRole(
            question,
            queries,
            errors,
            @"\bto\s+([A-Za-z]{3})\b",
            asOrigin: false);

        RequireAirportRole(
            question,
            queries,
            errors,
            @"\barriv(?:ing|al|als)?\s+at\s+([A-Za-z]{3})\b",
            asOrigin: false);
    }

    private static void RequireAirportRole(
        string question,
        IReadOnlyList<AirportQuery> queries,
        ICollection<string> errors,
        string pattern,
        bool asOrigin)
    {
        foreach (Match match in Regex.Matches(
            question,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var code = match.Groups[1].Value;
            if (code.Any(char.IsLower))
                continue;

            code = code.ToUpperInvariant();
            var preserved = asOrigin
                ? queries.Any(query => string.Equals(query.Origin, code, StringComparison.OrdinalIgnoreCase))
                : queries.Any(query => string.Equals(query.Destination, code, StringComparison.OrdinalIgnoreCase));

            if (preserved)
                continue;

            var role = asOrigin ? "origin" : "destination";
            var error = $"Plan dropped the requested {role} {code}. Keep {role}={code}.";
            if (!errors.Contains(error))
                errors.Add(error);
        }
    }

    private static string? ReadAirportCode(string clause, string pattern)
    {
        var match = Regex.Match(
            clause,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success || match.Groups[1].Value.Any(char.IsLower))
            return null;

        return match.Groups[1].Value.ToUpperInvariant();
    }

    private static bool AsksForOperationalStatus(string question)
    {
        return _statusIntentRegex.IsMatch(question)
            || _scheduledStatusRegex.IsMatch(question);
    }

    // "scheduled at IAH on October 6" is a date filter. "currently scheduled"
    // and "status Scheduled" are operational-status requests and are excluded.
    private static bool AsksForScheduleDate(string question)
    {
        if (_scheduledStatusRegex.IsMatch(question) ||
            !_scheduledWordRegex.IsMatch(question))
        {
            return false;
        }

        return ExtractExplicitDate(question).HasValue
            || _relativeDateWordRegex.IsMatch(question)
            || _scheduledForDateRegex.IsMatch(question);
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
            DateOnly.TryParseExact(
                $"{monthDateYearMatch.Groups["month"].Value} {monthDateYearMatch.Groups["day"].Value} {monthDateYearMatch.Groups["year"].Value}",
                "MMMM d yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
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

    private static bool LooksLikeOptionList(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains('|', StringComparison.Ordinal);
    }

    private static string? ValidateDirectionInvariants(
        AirportFlightType? flightType,
        string? origin,
        string? destination)
    {
        if (!flightType.HasValue)
            return null;

        if (flightType == AirportFlightType.Arrival)
        {
            if (!string.IsNullOrWhiteSpace(destination)
                && !string.Equals(destination, _primaryAirportCode, StringComparison.OrdinalIgnoreCase))
            {
                return $"arrival flights require destination={_primaryAirportCode} when destination is specified; received destination={destination}.";
            }

            if (string.Equals(origin, _primaryAirportCode, StringComparison.OrdinalIgnoreCase))
            {
                return $"arrival flights cannot use origin={_primaryAirportCode}; use destination={_primaryAirportCode} for arrivals at the primary airport.";
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(origin)
            && !string.Equals(origin, _primaryAirportCode, StringComparison.OrdinalIgnoreCase))
        {
            return $"departure flights require origin={_primaryAirportCode} when origin is specified; received origin={origin}.";
        }

        if (string.Equals(destination, _primaryAirportCode, StringComparison.OrdinalIgnoreCase))
        {
            return $"departure flights cannot use destination={_primaryAirportCode}; use origin={_primaryAirportCode} for departures from the primary airport.";
        }

        return null;
    }

    // Every flight in the simulation touches the primary airport, so the route
    // already says which way it goes: "to DEN" is a departure and "from LAX" is
    // an arrival. Small models often get flightType backwards, so the route wins
    // unless the user explicitly asked for the direction the model chose.
    private static AirportFlightType? AlignFlightTypeWithRoute(
        string question,
        AirportFlightType? flightType,
        string? origin,
        string? destination)
    {
        var impliedType = GetFlightTypeImpliedByRoute(origin, destination);

        if (impliedType is null || flightType == impliedType)
            return flightType;

        if (flightType is null)
            return impliedType;

        var mentionsArrival = _arrivalWordRegex.IsMatch(question);
        var mentionsDeparture = _departureWordRegex.IsMatch(question);

        // "Departing LAX and arriving at IAH" names both ends of one route.
        // The airports decide the type. A question that only says "departures"
        // keeps that explicit type so an impossible departure is rejected.
        if (mentionsArrival && mentionsDeparture)
            return impliedType;

        var explicitlyRequested = flightType == AirportFlightType.Arrival
            ? mentionsArrival
            : mentionsDeparture;

        return explicitlyRequested ? flightType : impliedType;
    }

    // In multi-part questions ("arriving from LAX and departing to ORD") models
    // often leak one part's airport into the other query, producing an arrival
    // from LAX to ORD. When the user explicitly asked for arrivals (or
    // departures), the far end must be the primary airport, so fix it here
    // instead of spending a repair round-trip on it.
    private static (string? Origin, string? Destination) AnchorRouteToPrimaryAirport(
        string question,
        AirportFlightType? flightType,
        string? origin,
        string? destination)
    {
        if (!IsOtherAirport(origin) || !IsOtherAirport(destination))
            return (origin, destination);

        if (flightType == AirportFlightType.Arrival &&
            _arrivalWordRegex.IsMatch(question))
        {
            return (origin, _primaryAirportCode);
        }

        if (flightType == AirportFlightType.Departure &&
            _departureWordRegex.IsMatch(question))
        {
            return (_primaryAirportCode, destination);
        }

        return (origin, destination);
    }

    private static bool IsOtherAirport(string? code)
    {
        return !string.IsNullOrWhiteSpace(code)
            && !string.Equals(code, _primaryAirportCode, StringComparison.OrdinalIgnoreCase);
    }

    private static AirportFlightType? GetFlightTypeImpliedByRoute(
        string? origin,
        string? destination)
    {
        var fromOtherAirport = !string.IsNullOrWhiteSpace(origin)
            && !string.Equals(origin, _primaryAirportCode, StringComparison.OrdinalIgnoreCase);

        var toOtherAirport = !string.IsNullOrWhiteSpace(destination)
            && !string.Equals(destination, _primaryAirportCode, StringComparison.OrdinalIgnoreCase);

        if (toOtherAirport && !fromOtherAirport)
            return AirportFlightType.Departure;

        if (fromOtherAirport && !toOtherAirport)
            return AirportFlightType.Arrival;

        return null;
    }

    private static string? NormalizeCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }
}
