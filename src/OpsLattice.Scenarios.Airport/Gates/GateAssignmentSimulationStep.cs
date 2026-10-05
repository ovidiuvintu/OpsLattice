using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class GateAssignmentSimulationStep : ISimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly GateAssignmentProcessor _processor;

    public GateAssignmentSimulationStep(
        IFlightProvider flightProvider,
        GateAssignmentProcessor processor)
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