using Microsoft.AspNetCore.Mvc;
using OpsLattice.Api.Models;
using OpsLattice.Scenarios.Airport.Simulation;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    private readonly AirportSimulationManager _manager;

    public SimulationController(AirportSimulationManager manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public ActionResult<SimulationResponse> Get()
    {
        var currentTime = _manager.GetCurrentTime();

        return Ok(new SimulationResponse(currentTime));
    }

    [HttpPost("advance")]
    public ActionResult<SimulationResponse> Advance(
        AdvanceSimulationRequest request)
    {
        if (request.Minutes <= 0)
        {
            return BadRequest("Minutes must be greater than zero.");
        }

        var currentTime = _manager.Advance(
            TimeSpan.FromMinutes(request.Minutes));

        return Ok(new SimulationResponse(currentTime));
    }
}
