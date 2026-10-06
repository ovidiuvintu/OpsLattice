using OpsLattice.Simulator.Time;

namespace OpsLattice.Simulator.Simulation;

public sealed class SimulationRunner
{
    private readonly SimulationClock _clock;
    private readonly IReadOnlyCollection<ISimulationStep> _steps;

    public SimulationRunner(
        SimulationClock clock,
        IEnumerable<ISimulationStep> steps)
    {
        _clock = clock;
        _steps = steps.ToArray();
    }

    public void Advance(TimeSpan duration)
    {
        _clock.Advance(duration);

        foreach (var step in _steps)
        {
            step.Execute();
        }
    }
}