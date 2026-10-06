namespace OpsLattice.Simulator.Simulation;

public interface IScheduledSimulationStep : ISimulationStep
{
    DateTimeOffset? GetNextExecutionTime(
        DateTimeOffset currentTime,
        DateTimeOffset targetTime);
}