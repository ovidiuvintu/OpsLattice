namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateQueryService
{
    private readonly IGateProvider _gateProvider;

    public GateQueryService(IGateProvider gateProvider)
    {
        _gateProvider = gateProvider;
    }

    public IReadOnlyCollection<GateStatusDto> GetGateStatuses()
    {
        return _gateProvider.GetGates()
            .Select(gate => new GateStatusDto(
                Code: gate.Code,
                IsAssigned: gate.IsAssigned,
                IsOccupied: gate.IsOccupied,
                IsAvailable: !gate.IsAssigned && !gate.IsOccupied,
                AssignedFlightNumber:
                    gate.AssignedFlight?.FlightNumber,
                OccupyingFlightNumber:
                    gate.OccupyingFlight?.FlightNumber))
            .ToArray();
    }

    public GateSummaryDto GetGateSummary()
    {
        var gates = _gateProvider.GetGates();

        return new GateSummaryDto(
            Total: gates.Count(),
            Available: gates.Count(
                gate => !gate.IsAssigned && !gate.IsOccupied),
            Assigned: gates.Count(
                gate => gate.IsAssigned),
            Occupied: gates.Count(
                gate => gate.IsOccupied));
    }
}
