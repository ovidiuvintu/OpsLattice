using System.Text.Json.Serialization;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

builder.Services.AddSingleton<IFlightProvider, InMemoryFlightProvider>();

var simulationStartTime =
    new DateTimeOffset(2026, 10, 4, 14, 20, 0, TimeSpan.Zero);

builder.Services.AddSingleton(
    new SimulationClock(simulationStartTime));

builder.Services.AddSingleton<ISimulationClock>(
    provider => provider.GetRequiredService<SimulationClock>());

builder.Services.AddSingleton<FlightArrivalProcessor>();
builder.Services.AddSingleton<FlightArrivalSimulationStep>();

builder.Services.AddSingleton<SimulationRunner>(provider =>
{
    var clock = provider.GetRequiredService<SimulationClock>();
    var arrivalStep =
        provider.GetRequiredService<FlightArrivalSimulationStep>();

    return new SimulationRunner(clock, [arrivalStep]);
});

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("WebClient");

app.MapControllers();

app.Run();