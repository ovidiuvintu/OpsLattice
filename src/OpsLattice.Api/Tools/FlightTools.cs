using System.ComponentModel;
using ModelContextProtocol.Server;
using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Api.Mcp;

[McpServerToolType]
public sealed class FlightTools(FlightQueryService flightQueryService)
{
    [McpServerTool(Name = "get_flights", ReadOnly = true)]
    [Description(
        "Returns current flights, their statuses, assigned gates, " +
        "and occupied gates.")]
    public IReadOnlyCollection<FlightStatusDto> GetFlights()
    {
        return flightQueryService.GetFlightStatuses();
    }
}