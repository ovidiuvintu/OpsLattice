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
    [Description(
    "Counts flights matching optional filters for origin, destination, " +
    "flight type, scheduled date, and operational status. " +
    "Use flightType only for Arrival or Departure. " +
    "Use status for Scheduled, Approaching, Landed, or other flight statuses.")]
    public int GetFlightCount(
        string? origin = null,
        string? destination = null,
        string? flightType = null,
        DateOnly? date = null,
        [Description("Operational flight status, e.g. Scheduled, Approaching, Landed.")]
        string? status = null)
    {
        return flightQueryService.CountFlights(
            origin, destination, flightType, date, status);
    }
}
