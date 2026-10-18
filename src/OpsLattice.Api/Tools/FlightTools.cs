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

    [McpServerTool(Name = "get_flight_count")]
    [Description(
    "Counts flights in the active airport simulation. " +
    "Optional filters include origin airport code, " +
    "destination airport code, flight type and schedule date.")]
    public int GetFlightCount(
    string? origin = null,
    string? destination = null,
    string? flightType = null,
    DateOnly? date = null)
    {
        return flightQueryService.CountFlights(
            origin,
            destination,
            flightType,
            date);
    }
}
