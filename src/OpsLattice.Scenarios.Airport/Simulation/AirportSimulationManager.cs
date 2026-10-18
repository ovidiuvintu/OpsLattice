
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

    public IReadOnlyList<FlightSnapshot> GetFlights()
    {
        lock (_sync)
        {
            var runtime = GetActiveRuntime();

            var gates = runtime.Definition.Gates;

            return runtime.Definition.Flights
                .Select(flight =>
                {
                    var assignedGate = gates.FirstOrDefault(
                        gate => ReferenceEquals(
                            gate.AssignedFlight, flight));

                    var occupiedGate = gates.FirstOrDefault(
                        gate => ReferenceEquals(
                            gate.OccupyingFlight, flight));

                    return new FlightSnapshot(
                        flight.FlightNumber,
                        flight.Airline,
                        flight.Type.ToString(),
                        flight.Origin,
                        flight.Destination,
                        flight.ScheduledTime,
                        flight.Status.ToString(),
                        flight.ApproachStartedAt,
                        flight.LandedAt,
                        flight.ArrivedAtGateAt,
                        assignedGate?.Code,
                        occupiedGate?.Code);
                })
                .ToArray();
        }
    }

    public IReadOnlyList<GateSnapshot> GetGates()
    {
        lock (_sync)
        {
            var runtime = GetActiveRuntime();

            return runtime.Definition.Gates
                .Select(gate => new GateSnapshot(
                    Code: gate.Code,
                    TerminalCode: gate.TerminalCode,
                    IsAssigned: gate.IsAssigned,
                    IsOccupied: gate.IsOccupied,
                    IsAvailable: !gate.IsAssigned && !gate.IsOccupied,
                    AssignedFlightNumber:
                        gate.AssignedFlight?.FlightNumber,
                    OccupyingFlightNumber:
                        gate.OccupyingFlight?.FlightNumber))
                .ToArray();
        }
    }
}
