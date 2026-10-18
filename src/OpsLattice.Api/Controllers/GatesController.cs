using Microsoft.AspNetCore.Mvc;
using OpsLattice.Api.Models;
using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/gates")]
public sealed class GatesController : ControllerBase
{
    private readonly GateQueryService _gateQueryService;

    public GatesController(GateQueryService gateQueryService)
    {
        _gateQueryService = gateQueryService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<GateResponse>> Get()
    {
        var gates = _gateQueryService
            .GetGateStatuses()
            .Select(gate => new GateResponse(
                gate.Code,
                GetStatus(gate.IsAssigned, gate.IsOccupied),
                gate.AssignedFlightNumber,
                gate.OccupyingFlightNumber))
            .ToArray();

        return Ok(gates);
    }

    private static string GetStatus(bool isAssigned, bool isOccupied)
    {
        if (isOccupied)
        {
            return "Occupied";
        }

        if (isAssigned)
        {
            return "Assigned";
        }

        return "Available";
    }
}
