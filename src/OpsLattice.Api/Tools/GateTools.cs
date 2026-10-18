using System.ComponentModel;
using ModelContextProtocol.Server;
using OpsLattice.Scenarios.Airport.Gates;

[McpServerToolType]
public sealed class GateTools(GateQueryService gateQueryService)
{
    [McpServerTool(Name = "get_gates", ReadOnly = true)]
    [Description(
        "Returns the current state of all airport gates, " +
        "including availability, assigned flights, and occupancy.")]
    public IReadOnlyCollection<GateStatusDto> GetGates()
    {
        return gateQueryService.GetGateStatuses();
    }

    [McpServerTool(Name = "get_gate_summary", ReadOnly = true)]
    [Description(
        "Returns total, available, assigned, and occupied gate counts. " +
        "Use this tool for questions about gate availability or counts.")]
    public GateSummaryDto GetGateSummary()
    {
        return gateQueryService.GetGateSummary();
    }
}
