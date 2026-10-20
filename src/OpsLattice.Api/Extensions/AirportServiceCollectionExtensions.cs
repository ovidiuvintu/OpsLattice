using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;

namespace OpsLattice.Api.Extensions;

public static class AirportServiceCollectionExtensions
{
    public static IServiceCollection AddAirportSimulation(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services.AddSingleton<FlightQueryService>();
        services.AddSingleton<GateQueryService>();

        services.AddSingleton<TerminalGenerator>();
        services.AddSingleton<GateGenerator>();
        services.AddSingleton<FlightScheduleGenerator>();

        services.AddSingleton<AirportSimulationSetupService>();
        services.AddSingleton<AirportSimulationRuntimeFactory>();
        services.AddSingleton<AirportSimulationManager>();

        return services;
    }
}
