using Microsoft.AspNetCore.Mvc;
using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/flights")]
public sealed class FlightsController : ControllerBase
{
    private readonly IFlightProvider _flightProvider;

    public FlightsController(IFlightProvider flightProvider)
    {
        _flightProvider = flightProvider;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Flight>> GetFlights()
    {
        var flights = _flightProvider.GetFlights();

        return Ok(flights);
    }
}