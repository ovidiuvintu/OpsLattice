using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateDepartureSimulationStep : ISimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly GateDepartureProcessor _processor;

    public GateDepartureSimulationStep(
        IFlightProvider flightProvider,
        GateDepartureProcessor processor)
    {
        _flightProvider = flightProvider;
        _processor = processor;
    }

    public void Execute()
    {
        foreach (var flight in _flightProvider.GetFlights())
        {
            _processor.Process(flight);
        }
    }
}