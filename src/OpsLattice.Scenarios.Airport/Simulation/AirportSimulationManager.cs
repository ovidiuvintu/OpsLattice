
namespace OpsLattice.Scenarios.Airport.Simulation;

public sealed class AirportSimulationManager
{
    private readonly object _sync = new();

    private AirportSimulationRuntime? _activeRuntime;

    public bool HasActiveSimulation
    {
        get
        {
            lock (_sync)
            {
                return _activeRuntime is not null;
            }
        }
    }

    public void Activate(AirportSimulationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_sync)
        {
            _activeRuntime = runtime;
        }
    }

    public DateTimeOffset GetCurrentTime()
    {
        lock (_sync)
        {
            return GetActiveRuntime().Clock.CurrentTime;
        }
    }

    public DateTimeOffset Advance(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Simulation time must advance by a positive duration.");
        }

        lock (_sync)
        {
            var runtime = GetActiveRuntime();

            runtime.Runner.Advance(duration);

            return runtime.Clock.CurrentTime;
        }
    }

    private AirportSimulationRuntime GetActiveRuntime()
    {
        return _activeRuntime ??
            throw new InvalidOperationException(
                "No active airport simulation exists.");
    }
}
