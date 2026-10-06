using Microsoft.AspNetCore.Mvc;
using OpsLattice.Api.Models;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/simulation")]
public sealed class SimulationController : ControllerBase
{
    private readonly SimulationRunner _runner;
    private readonly ISimulationClock _clock;

    public SimulationController(
        SimulationRunner runner,
        ISimulationClock clock)
    {
        _runner = runner;
        _clock = clock;
    }

    [HttpGet]
    public ActionResult<SimulationResponse> Get()
    {
        return Ok(
            new SimulationResponse(
                _clock.CurrentTime));
    }

    [HttpPost("advance")]
    public ActionResult<SimulationResponse> Advance(
        AdvanceSimulationRequest request)
    {
        if (request.Minutes <= 0)
        {
            return BadRequest(
                "Minutes must be greater than zero.");
        }

        _runner.Advance(
            TimeSpan.FromMinutes(request.Minutes));

        return Ok(
            new SimulationResponse(
                _clock.CurrentTime));
    }
}