namespace OpsLattice.Api.Models;

public sealed record GateResponse(
    string Code,
    string Status,
    string? AssignedFlight,
    string? OccupyingFlight);