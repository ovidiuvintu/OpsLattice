using System.ComponentModel;
using ModelContextProtocol.Server;
using OpsLattice.Scenarios.Airport.Simulation;

[McpServerToolType]
public sealed class SimulationTools(
    AirportSimulationManager manager)
{
    [McpServerTool(Name = "get_simulation_time", ReadOnly = true)]
    [Description("Returns the current UTC time of the active airport simulation.")]
    public DateTimeOffset GetSimulationTime()
    {
        return manager.GetCurrentTime();
    }
}
