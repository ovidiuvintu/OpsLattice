using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateOccupancySimulationStep : ISimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly GateOccupancyProcessor _processor;

    public GateOccupancySimulationStep(
        IFlightProvider flightProvider,
        GateOccupancyProcessor processor)
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