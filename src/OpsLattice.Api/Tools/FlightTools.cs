using System.ComponentModel;
using ModelContextProtocol.Server;
using OpsLattice.Scenarios.Airport.Flights;

[McpServerToolType]
public sealed class FlightTools(
    FlightQueryService flightQueryService)
{
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
            flightType,
            parsedDate);
    }
}
