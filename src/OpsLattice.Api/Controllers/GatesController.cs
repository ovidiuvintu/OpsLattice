using Microsoft.AspNetCore.Mvc;
using OpsLattice.Api.Models;
using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/gates")]
public sealed class GatesController : ControllerBase
{
    private readonly IGateProvider _gateProvider;

    public GatesController(
        IGateProvider gateProvider)
    {
        _gateProvider = gateProvider;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<GateResponse>> Get()
    {
        var gates = _gateProvider
            .GetGates()
            .Select(gate =>
                new GateResponse(
                    gate.Code,
                    GetStatus(gate),
                    gate.AssignedFlight?.FlightNumber,
                    gate.OccupyingFlight?.FlightNumber))
            .ToArray();

        return Ok(gates);
    }

    private static string GetStatus(Gate gate)
    {
        if (gate.IsOccupied)
        {
            return "Occupied";
        }

        if (gate.IsAssigned)
        {
            return "Assigned";
        }

        return "Available";
    }
}