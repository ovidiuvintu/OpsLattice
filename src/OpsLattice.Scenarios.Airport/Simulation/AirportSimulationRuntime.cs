using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Simulation;

public sealed class AirportSimulationRuntime
{
    public AirportSimulationRuntime(
        AirportSimulationDefinition definition,
        SimulationClock clock,
        SimulationRunner runner,
        IFlightProvider flightProvider,
        IGateProvider gateProvider)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(flightProvider);
        ArgumentNullException.ThrowIfNull(gateProvider);

        Definition = definition;
        Clock = clock;
        Runner = runner;
        FlightProvider = flightProvider;
        GateProvider = gateProvider;
    }

    public AirportSimulationDefinition Definition { get; }

    public SimulationClock Clock { get; }

    public SimulationRunner Runner { get; }

    public IFlightProvider FlightProvider { get; }

    public IGateProvider GateProvider { get; }
}
