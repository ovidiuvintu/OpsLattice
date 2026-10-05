
namespace OpsLattice.Simulator.Time;

public interface ISimulationClock
{
    DateTimeOffset CurrentTime { get; }
}
