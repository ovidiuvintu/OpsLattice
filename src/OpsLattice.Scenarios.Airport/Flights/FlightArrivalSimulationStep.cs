using OpsLattice.Simulator.Simulation;

namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class FlightArrivalSimulationStep : ISimulationStep
{
    private readonly IFlightProvider _flightProvider;
    private readonly FlightArrivalProcessor _arrivalProcessor;

    public FlightArrivalSimulationStep(
        IFlightProvider flightProvider,
        FlightArrivalProcessor arrivalProcessor)
    {
        _flightProvider = flightProvider;
        _arrivalProcessor = arrivalProcessor;
    }

    public void Execute()
    {
        foreach (var flight in _flightProvider.GetFlights())
        {
            _arrivalProcessor.Process(flight);
        }
    }
}