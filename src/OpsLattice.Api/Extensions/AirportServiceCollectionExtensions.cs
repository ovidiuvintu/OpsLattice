
using OpsLattice.Api.Infrastructure.Scheduling;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Scenarios.Airport.Scheduling;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Api.Extensions;

public static class AirportServiceCollectionExtensions
{
    public static IServiceCollection AddAirportSimulation(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        var scheduleDate = new DateOnly(2026, 10, 6);

        services.AddSingleton<IFlightScheduleLoader>(
            _ => new ExcelFlightScheduleLoader(
                Path.Combine(
                    environment.ContentRootPath,
                    "Data",
                    "OpsLattice_ATL_Daily_Flight_Schedule_2150.xlsx")));

        services.AddSingleton<IFlightProvider>(provider =>
        {
            var loader = provider.GetRequiredService<IFlightScheduleLoader>();
            return new InMemoryFlightProvider(loader.Load(scheduleDate));
        });

        services.AddSingleton<IGateProvider>(
            _ => new InMemoryGateProvider());

        services.AddSingleton<FlightQueryService>();
        services.AddSingleton<GateQueryService>();

        var startTime = new DateTimeOffset(
            2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

        services.AddSingleton(new SimulationClock(startTime));

        services.AddSingleton<ISimulationClock>(
            provider => provider.GetRequiredService<SimulationClock>());

        services.AddSingleton<FlightArrivalProcessor>();
        services.AddSingleton<FlightArrivalSimulationStep>();

        services.AddSingleton<GateAssignmentProcessor>();
        services.AddSingleton<GateAssignmentSimulationStep>();

        services.AddSingleton<GateOccupancyProcessor>();
        services.AddSingleton<GateOccupancySimulationStep>();

        services.AddSingleton<GateDepartureProcessor>();
        services.AddSingleton<GateDepartureSimulationStep>();

        services.AddSingleton<FlightDepartureProcessor>();
        services.AddSingleton<FlightDepartureSimulationStep>();

        services.AddSingleton<SimulationRunner>(provider =>
            new SimulationRunner(
                provider.GetRequiredService<SimulationClock>(),
                [
                    provider.GetRequiredService<GateAssignmentSimulationStep>(),
                    provider.GetRequiredService<FlightArrivalSimulationStep>(),
                    provider.GetRequiredService<GateOccupancySimulationStep>(),
                    provider.GetRequiredService<GateDepartureSimulationStep>(),
                    provider.GetRequiredService<FlightDepartureSimulationStep>()
                ]));

        services.AddSingleton<TerminalGenerator>();
        services.AddSingleton<GateGenerator>();
        services.AddSingleton<FlightScheduleGenerator>();

        services.AddSingleton<AirportSimulationSetupService>();
        services.AddSingleton<AirportSimulationRuntimeFactory>();
        services.AddSingleton<AirportSimulationManager>();

        return services;
    }
}
