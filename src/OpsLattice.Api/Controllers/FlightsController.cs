using Microsoft.AspNetCore.Mvc;
using OpsLattice.Api.Models;
using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/flights")]
public sealed class FlightsController : ControllerBase
{
    private readonly FlightQueryService _flightQueryService;

    public FlightsController(FlightQueryService flightQueryService)
    {
        _flightQueryService = flightQueryService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<FlightResponse>> Get()
    {
        var flights = _flightQueryService.GetFlights()
            .Select(flight => new FlightResponse(
                flight.FlightNumber,
                flight.Airline,
                flight.Type,
                flight.Origin,
                flight.Destination,
                flight.ScheduledTime,
                flight.Status,
                flight.ApproachStartedAt,
                flight.LandedAt,
                flight.ArrivedAtGateAt,
                flight.Gate))
            .ToArray();

        return Ok(flights);
    }
}
