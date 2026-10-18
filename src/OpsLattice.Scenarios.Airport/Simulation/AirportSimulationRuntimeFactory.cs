
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Scenarios.Airport.Simulation;

public sealed class AirportSimulationRuntimeFactory
{
    public AirportSimulationRuntime Create(
        AirportSimulationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Each runtime gets its own clock.
        var clock = new SimulationClock(definition.InitialTime);

        // Providers share the scenario's domain objects.
        var flightProvider =
            new InMemoryFlightProvider(definition.Flights);

        var gateProvider =
            new InMemoryGateProvider(definition.Gates);

        // All processors use the same clock and gate provider.
        var arrivalProcessor =
            new FlightArrivalProcessor(clock);

        var assignmentProcessor =
            new GateAssignmentProcessor(clock, gateProvider);

        var occupancyProcessor =
            new GateOccupancyProcessor(clock, gateProvider);

        var gateDepartureProcessor =
            new GateDepartureProcessor(clock, gateProvider);

        var flightDepartureProcessor =
            new FlightDepartureProcessor(clock);

        // All steps use the same flight provider.
        var assignmentStep =
            new GateAssignmentSimulationStep(
                flightProvider,
                assignmentProcessor);

        var arrivalStep =
            new FlightArrivalSimulationStep(
                flightProvider,
                arrivalProcessor);

        var occupancyStep =
            new GateOccupancySimulationStep(
                flightProvider,
                occupancyProcessor);

        var gateDepartureStep =
            new GateDepartureSimulationStep(
                flightProvider,
                gateDepartureProcessor);

        var flightDepartureStep =
            new FlightDepartureSimulationStep(
                flightProvider,
                flightDepartureProcessor);

        // Preserve the existing execution order.
        var runner = new SimulationRunner(
            clock,
            [
                assignmentStep,
                arrivalStep,
                occupancyStep,
                gateDepartureStep,
                flightDepartureStep
            ]);

        return new AirportSimulationRuntime(
            definition,
            clock,
            runner,
            flightProvider,
            gateProvider);
    }
}
