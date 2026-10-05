namespace OpsLattice.Simulator.Time;

public sealed class SimulationClock : ISimulationClock
{
    public SimulationClock(DateTimeOffset startTime)
    {
        CurrentTime = startTime;
    }

    public DateTimeOffset CurrentTime { get; private set; }

    public void Advance(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Simulation time must advance by a positive duration.");
        }

        CurrentTime = CurrentTime.Add(duration);
    }
}