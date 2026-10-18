
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Scenarios.Airport.Terminals;

namespace OpsLattice.Scenarios.Airport.Setup;

public sealed class AirportSimulationDefinition
{
    public AirportSimulationDefinition(
        IReadOnlyCollection<Terminal> terminals,
        IReadOnlyCollection<Gate> gates,
        IReadOnlyCollection<Flight> flights,
        DateTimeOffset initialTime)
    {
        ArgumentNullException.ThrowIfNull(terminals);
        ArgumentNullException.ThrowIfNull(gates);
        ArgumentNullException.ThrowIfNull(flights);

        Terminals = Array.AsReadOnly(terminals.ToArray());
        Gates = Array.AsReadOnly(gates.ToArray());
        Flights = Array.AsReadOnly(flights.ToArray());
        InitialTime = initialTime;
    }

    public IReadOnlyCollection<Terminal> Terminals { get; }

    public IReadOnlyCollection<Gate> Gates { get; }

    public IReadOnlyCollection<Flight> Flights { get; }

    public DateTimeOffset InitialTime { get; }

    private int _claimed;

    internal void ClaimForRuntime()
    {
        if (Interlocked.CompareExchange(
                ref _claimed,
                1,
                0) != 0)
        {
            throw new InvalidOperationException(
                "This airport simulation definition has already " +
                "been used to create a runtime.");
        }
    }

}
