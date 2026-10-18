using OpsLattice.Scenarios.Airport.Simulation;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateQueryService(
    AirportSimulationManager manager)
{
    public IReadOnlyCollection<GateStatusDto> GetGateStatuses()
    {
        return manager.GetGates()
            .Select(gate => new GateStatusDto(
                Code: gate.Code,
                IsAssigned: gate.IsAssigned,
                IsOccupied: gate.IsOccupied,
                IsAvailable: gate.IsAvailable,
                AssignedFlightNumber: gate.AssignedFlightNumber,
                OccupyingFlightNumber: gate.OccupyingFlightNumber))
            .ToArray();
    }

    public GateSummaryDto GetGateSummary()
    {
        var gates = manager.GetGates();

        return new GateSummaryDto(
            Total: gates.Count,
            Available: gates.Count(gate => gate.IsAvailable),
            Assigned: gates.Count(gate => gate.IsAssigned),
            Occupied: gates.Count(gate => gate.IsOccupied));
    }
}
