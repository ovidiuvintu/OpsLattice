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
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Simulation duration must be positive.");
        }

        var targetTime = _clock.CurrentTime.Add(duration);

        while (_clock.CurrentTime < targetTime)
        {
            var nextExecutionTime =
                GetNextExecutionTime(targetTime);

            if (nextExecutionTime.HasValue)
            {
                AdvanceClockTo(nextExecutionTime.Value);
                ExecuteSteps();
                continue;
            }

            AdvanceClockTo(targetTime);
            ExecuteSteps();
        }
    }

    private DateTimeOffset? GetNextExecutionTime(
        DateTimeOffset targetTime)
    {
        DateTimeOffset? nextExecutionTime = null;

        foreach (var step in
                 _steps.OfType<IScheduledSimulationStep>())
        {
            var candidate =
                step.GetNextExecutionTime(
                    _clock.CurrentTime,
                    targetTime);

            if (!candidate.HasValue)
            {
                continue;
            }

            if (!nextExecutionTime.HasValue ||
                candidate.Value < nextExecutionTime.Value)
            {
                nextExecutionTime = candidate.Value;
            }
        }

        return nextExecutionTime;
    }

    private void AdvanceClockTo(
        DateTimeOffset targetTime)
    {
        var duration =
            targetTime - _clock.CurrentTime;

        _clock.Advance(duration);
    }

    private void ExecuteSteps()
    {
        foreach (var step in _steps)
        {
            step.Execute();
        }
    }
}