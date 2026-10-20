using System.ComponentModel;
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

        throw new ArgumentException(
            "flightType must be 'Arrival' or 'Departure'. Omit flightType to include all flight types.");
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
            && string.Equals(origin, airportCode, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException(
                $"For arrivals at {airportCode}, use destination={airportCode}. origin={airportCode} with flightType=Arrival is likely reversed.");
        }

        if (string.Equals(flightType, _departure, StringComparison.OrdinalIgnoreCase)
            && string.Equals(destination, airportCode, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(origin))
        {
            throw new ArgumentException(
                $"For departures at {airportCode}, use origin={airportCode}. destination={airportCode} with flightType=Departure is likely reversed.");
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

        Only include the status parameter when the user explicitly
        specifies an operational status, such as Scheduled,
        Approaching, Landed, or Departed.

        A flight's scheduled date does not imply status=Scheduled.

        Omit status when the user requests all flights regardless
        of their current operational state.
        """)]
    public int GetFlightCount(
    string? origin = null,
    string? destination = null,
    string? flightType = null,
    string? date = null)
    {
        var validatedFlightType = ValidateFlightType(flightType);
        ValidateDirectionalAirportUsage(
            origin,
            destination,
            validatedFlightType);

        DateOnly? parsedDate = null;

        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var value))
            {
                throw new ArgumentException(
                    "Date must be in YYYY-MM-DD format.");
            }

            parsedDate = value;
        }

        return flightQueryService.CountFlights(
            origin,
            destination,
            validatedFlightType,
            parsedDate);
    }
}
