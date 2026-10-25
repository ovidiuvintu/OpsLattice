using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Simulation;

[McpServerToolType]
public sealed class FlightTools(
    FlightQueryService flightQueryService)
{
    private const string _arrival = nameof(FlightType.Arrival);
    private const string _departure = nameof(FlightType.Departure);

    private static string? ValidateFlightType(string? flightType)
    {
        if (string.IsNullOrWhiteSpace(flightType))
            return null;

        if (string.Equals(
                flightType,
                nameof(FlightType.Arrival),
                StringComparison.OrdinalIgnoreCase))
        {
            return nameof(FlightType.Arrival);
        }

        if (string.Equals(
                flightType,
                nameof(FlightType.Departure),
                StringComparison.OrdinalIgnoreCase))
        {
            return nameof(FlightType.Departure);
        }

        throw new McpException(
            "flightType must be 'Arrival' or 'Departure'. Omit flightType to include all flight types.");
    }

    private static string? ValidateStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        if (Enum.TryParse<FlightStatus>(status.Trim(), ignoreCase: true, out var value)
            && Enum.IsDefined(value))
        {
            return value.ToString();
        }

        throw new McpException(
            $"status must be one of: {string.Join(", ", Enum.GetNames<FlightStatus>())}. Omit status to include all statuses.");
    }

    private static DateOnly? ParseDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return null;

        if (!DateOnly.TryParseExact(
                date,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var value))
        {
            throw new McpException(
                "Date must be in YYYY-MM-DD format.");
        }

        return value;
    }

    private static string? ResolvePrimaryAirportCode(
        IReadOnlyCollection<FlightSnapshot> flights)
    {
        return flights
            .Where(f =>
                (string.Equals(f.Type, _departure, StringComparison.OrdinalIgnoreCase)
                 && !string.IsNullOrWhiteSpace(f.Origin))
                ||
                (string.Equals(f.Type, _arrival, StringComparison.OrdinalIgnoreCase)
                 && !string.IsNullOrWhiteSpace(f.Destination)))
            .Select(f => string.Equals(f.Type, _departure, StringComparison.OrdinalIgnoreCase)
                ? f.Origin
                : f.Destination)
            .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private void ValidateDirectionalAirportUsage(
        string? origin,
        string? destination,
        string? flightType)
    {
        if (string.IsNullOrWhiteSpace(flightType))
            return;

        var flights = flightQueryService.GetFlights();
        var airportCode = ResolvePrimaryAirportCode(flights);

        if (string.IsNullOrWhiteSpace(airportCode))
            return;

        if (string.Equals(flightType, _arrival, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(destination)
            && !string.Equals(destination, airportCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(
                $"Arrival flights require destination={airportCode} when destination is specified; received destination={destination}.");
        }

        if (string.Equals(flightType, _arrival, StringComparison.OrdinalIgnoreCase)
            && string.Equals(origin, airportCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(
                $"Arrival flights cannot use origin={airportCode}; use destination={airportCode} for arrivals at the primary airport.");
        }

        if (string.Equals(flightType, _departure, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(origin)
            && !string.Equals(origin, airportCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(
                $"Departure flights require origin={airportCode} when origin is specified; received origin={origin}.");
        }

        if (string.Equals(flightType, _departure, StringComparison.OrdinalIgnoreCase)
            && string.Equals(destination, airportCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(
                $"Departure flights cannot use destination={airportCode}; use origin={airportCode} for departures from the primary airport.");
        }
    }

    [McpServerTool(Name = "get_flights", ReadOnly = true)]
    [Description("Returns current flights and their statuses.")]
    public IReadOnlyCollection<FlightStatusDto> GetFlights()
    {
        return flightQueryService.GetFlightStatuses();
    }

    [McpServerTool(Name = "get_flight_summary", ReadOnly = true)]
    [Description(
        "Returns the total number of flights and the number " +
        "of scheduled flights. Use this tool for flight counts.")]
    public FlightSummaryDto GetFlightSummary()
    {
        var flights = flightQueryService.GetFlights();

        return new FlightSummaryDto(
            Total: flights.Count,
            Scheduled: flights.Count(
                f => f.Status == nameof(FlightStatus.Scheduled))
        );
    }

    [McpServerTool(Name = "get_flight_count", ReadOnly = true)]
    [Description("""
        Counts flights matching explicitly requested filters.

        Supported filters are: origin, destination, flightType, date,
        and status. Omit any filter the user did not ask for.

        status is the flight's current operational state (for example
        Scheduled, Approaching, Landed, AtGate, Departed). A flight's
        scheduled date does not imply status=Scheduled.
        """)]
    public int GetFlightCount(
    string? origin = null,
    string? destination = null,
    string? flightType = null,
    string? date = null,
    string? status = null)
    {
        var validatedFlightType = ValidateFlightType(flightType);
        var validatedStatus = ValidateStatus(status);
        ValidateDirectionalAirportUsage(
            origin,
            destination,
            validatedFlightType);

        return flightQueryService.CountFlights(
            origin,
            destination,
            validatedFlightType,
            ParseDate(date),
            validatedStatus);
    }

    [McpServerTool(Name = "search_flights", ReadOnly = true)]
    [Description("""
        Lists flights matching explicitly requested filters, ordered by
        scheduled time. Each result includes flight number, airline,
        type, origin, destination, scheduled time (UTC), status and gate.

        Supported filters are: origin, destination, flightType, date,
        and status. Omit any filter the user did not ask for.

        Use get_flight_count instead when only a count is needed.
        """)]
    public IReadOnlyCollection<FlightDetailsDto> SearchFlights(
    string? origin = null,
    string? destination = null,
    string? flightType = null,
    string? date = null,
    string? status = null)
    {
        var validatedFlightType = ValidateFlightType(flightType);
        var validatedStatus = ValidateStatus(status);
        ValidateDirectionalAirportUsage(
            origin,
            destination,
            validatedFlightType);

        return flightQueryService.SearchFlights(
                origin,
                destination,
                validatedFlightType,
                ParseDate(date),
                validatedStatus)
            .Select(f => new FlightDetailsDto(
                f.FlightNumber,
                f.Airline,
                f.Type,
                f.Origin,
                f.Destination,
                f.ScheduledTime,
                f.Status,
                f.Gate ?? f.OccupiedGate))
            .ToArray();
    }
}
