namespace OpsLattice.Agents.Planning;

public enum AirportQueryOperation
{
    CountFlights,
    ListFlights,
    FlightSummary,
    GetSimulationTime
}

public enum AirportFlightType
{
    Arrival,
    Departure
}

public enum AirportFlightStatus
{
    Scheduled,
    Approaching,
    Landed,
    AtGate,
    Boarding,
    ReadyForDeparture,
    Departed,
    Delayed,
    Cancelled
}

public sealed record AirportQueryPlanDraft(
    IReadOnlyList<AirportQueryDraft> Queries);

public sealed record AirportQueryDraft(
    string? Operation,
    string? FlightType,
    string? Status,
    string? Origin,
    string? Destination,
    string? Date);

public sealed record AirportQueryPlan(
    IReadOnlyList<AirportQuery> Queries);

public sealed record AirportQuery(
    AirportQueryOperation Operation,
    AirportFlightType? FlightType,
    AirportFlightStatus? Status,
    string? Origin,
    string? Destination,
    DateOnly? Date,
    string? DateKeyword);

public sealed record AirportQueryValidationResult(
    bool IsValid,
    bool RequiresClarification,
    string? Clarification,
    AirportQueryPlan? Plan,
    IReadOnlyList<string> Errors)
{
    public static AirportQueryValidationResult Success(
        AirportQueryPlan plan)
    {
        return new AirportQueryValidationResult(
            IsValid: true,
            RequiresClarification: false,
            Clarification: null,
            Plan: plan,
            Errors: Array.Empty<string>());
    }

    public static AirportQueryValidationResult Invalid(
        IReadOnlyList<string> errors)
    {
        return new AirportQueryValidationResult(
            IsValid: false,
            RequiresClarification: false,
            Clarification: null,
            Plan: null,
            Errors: errors);
    }

    public static AirportQueryValidationResult ClarificationNeeded(
        string clarification)
    {
        return new AirportQueryValidationResult(
            IsValid: false,
            RequiresClarification: true,
            Clarification: clarification,
            Plan: null,
            Errors: Array.Empty<string>());
    }
}

public sealed record AirportPlannerOutput(
    AirportQueryPlanDraft? Plan,
    string? Error,
    string RawJson);

public sealed record AirportQueryExecutionResult(
    IReadOnlyList<AirportQueryExecutionItem> Items)
{
    public bool HasFailures => Items.Any(item => !item.IsSuccess);
}

public sealed record AirportQueryExecutionItem(
    int QueryIndex,
    AirportQuery Query,
    bool IsSuccess,
    string ToolName,
    IReadOnlyDictionary<string, object?> Arguments,
    string Result,
    long DurationMs);
