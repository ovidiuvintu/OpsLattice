using System.Text.Json.Serialization;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;
using OpsLattice.Api.Infrastructure.Scheduling;
using OpsLattice.Scenarios.Airport.Scheduling;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

// Airport data providers
builder.Services.AddSingleton<IFlightProvider>(provider =>
{
    var loader = provider
        .GetRequiredService<IFlightScheduleLoader>();

    var scheduleDate = new DateOnly(2026, 10, 6);

    var flights = loader.Load(scheduleDate);

    return new InMemoryFlightProvider(flights);
}); 

builder.Services.AddSingleton<IGateProvider, InMemoryGateProvider>();

// Simulation clock
var simulationStartTime =
    new DateTimeOffset(
        2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

builder.Services.AddSingleton(
    new SimulationClock(simulationStartTime));

builder.Services.AddSingleton<ISimulationClock>(
    provider =>
        provider.GetRequiredService<SimulationClock>());

// Airport simulation processing
builder.Services.AddSingleton<FlightArrivalProcessor>();
builder.Services.AddSingleton<FlightArrivalSimulationStep>();

builder.Services.AddSingleton<GateAssignmentProcessor>();
builder.Services.AddSingleton<GateAssignmentSimulationStep>();

builder.Services.AddSingleton<FlightArrivalProcessor>();
builder.Services.AddSingleton<FlightArrivalSimulationStep>();

builder.Services.AddSingleton<GateAssignmentProcessor>();
builder.Services.AddSingleton<GateAssignmentSimulationStep>();

builder.Services.AddSingleton<GateOccupancyProcessor>();
builder.Services.AddSingleton<GateOccupancySimulationStep>();

builder.Services.AddSingleton<GateDepartureProcessor>();
builder.Services.AddSingleton<GateDepartureSimulationStep>();

// Simulation runner
builder.Services.AddSingleton<SimulationRunner>(provider =>
{
    var clock =
        provider.GetRequiredService<SimulationClock>();

    var gateAssignmentStep =
        provider.GetRequiredService<GateAssignmentSimulationStep>();

    var arrivalStep =
        provider.GetRequiredService<FlightArrivalSimulationStep>();

    var gateOccupancyStep =
        provider.GetRequiredService<GateOccupancySimulationStep>();

    var gateDepartureStep =
        provider.GetRequiredService<GateDepartureSimulationStep>();

    return new SimulationRunner(
        clock,
        [
            gateAssignmentStep,
            arrivalStep,
            gateOccupancyStep,
            gateDepartureStep
        ]);
});

builder.Services.AddSingleton<GateDepartureProcessor>();
builder.Services.AddSingleton<GateDepartureSimulationStep>();

builder.Services.AddSingleton<FlightDepartureProcessor>();
builder.Services.AddSingleton<FlightDepartureSimulationStep>();

builder.Services.AddSingleton<IFlightScheduleLoader>(
    _ => new ExcelFlightScheduleLoader(
        Path.Combine(
            builder.Environment.ContentRootPath,
            "Data",
            "OpsLattice_ATL_Daily_Flight_Schedule_2150.xlsx")));

// Web client
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:65494")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var loader = scope.ServiceProvider
        .GetRequiredService<IFlightScheduleLoader>();

    var flights = loader.Load(
        new DateOnly(2026, 10, 6));

    Console.WriteLine(
        $"Loaded {flights.Count} flights.");

    Console.WriteLine(
        $"Arrivals: {flights.Count(f => f.Type == FlightType.Arrival)}");

    Console.WriteLine(
        $"Departures: {flights.Count(f => f.Type == FlightType.Departure)}");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("WebClient");

app.MapControllers();

app.Run();