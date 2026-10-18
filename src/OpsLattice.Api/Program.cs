
using System.Text.Json.Serialization;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Api.Extensions;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Scheduling;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// API
// --------------------------------------------------

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

// --------------------------------------------------
// Application services
// --------------------------------------------------

builder.Services.AddAirportSimulation(builder.Environment);

builder.Services.AddOpsLatticeAgents();

// --------------------------------------------------
// MCP server
// --------------------------------------------------

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// --------------------------------------------------
// CORS
// --------------------------------------------------

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

// Initialize and activate the airport simulation.
var setupService = app.Services
    .GetRequiredService<AirportSimulationSetupService>();

var configuration = new AirportSetupConfiguration(
    Terminals:
    [
        new TerminalSetupConfiguration("A", 10),
        new TerminalSetupConfiguration("B", 10)
    ],
    FlightCount: 100,
    ScheduleDate: new DateOnly(2026, 10, 6),
    RandomSeed: 42);

var definition = setupService.Build(configuration);

var runtimeFactory = app.Services
    .GetRequiredService<AirportSimulationRuntimeFactory>();

var runtime = runtimeFactory.Create(definition);

var manager = app.Services
    .GetRequiredService<AirportSimulationManager>();

manager.Activate(runtime);

// --------------------------------------------------
// Development tools
// --------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet(
        "/api/debug/mcp-tools",
        async (
            IAgentToolGateway gateway,
            CancellationToken cancellationToken) =>
        {
            var tools = await gateway.ListToolsAsync(
                cancellationToken);

            return Results.Ok(tools);
        });

    app.MapGet(
        "/api/debug/mcp-flights",
        async (
            IAgentToolGateway gateway,
            CancellationToken cancellationToken) =>
        {
            var result = await gateway.CallToolAsync(
                "get_flights",
                new Dictionary<string, object?>(),
                cancellationToken);

            return Results.Text(
                result,
                "application/json");
        });
}

// --------------------------------------------------
// Startup diagnostics
// --------------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var loader = scope.ServiceProvider
        .GetRequiredService<IFlightScheduleLoader>();

    var flights = loader.Load(
        new DateOnly(2026, 10, 6));

    Console.WriteLine(
        $"Loaded {flights.Count} flights.");

    Console.WriteLine(
        $"Arrivals: {flights.Count(
            f => f.Type == FlightType.Arrival)}");

    Console.WriteLine(
        $"Departures: {flights.Count(
            f => f.Type == FlightType.Departure)}");
}

// --------------------------------------------------
// Middleware
// --------------------------------------------------

app.UseHttpsRedirection();

app.UseCors("WebClient");

// --------------------------------------------------
// Endpoints
// --------------------------------------------------

app.MapControllers();

app.MapMcp("/mcp");

app.MapPost(
    "/api/assistant/query",
    async (
        AssistantQueryRequest request,
        IAgentOrchestrator orchestrator,
        CancellationToken cancellationToken) =>
    {
        var agentName =
            request.AgentName ?? "airport-operations";

        var result = await orchestrator.ExecuteAsync(
            agentName,
            request.Question,
            cancellationToken);

        return Results.Ok(result);
    });

app.Run();

// --------------------------------------------------
// Request models
// --------------------------------------------------

public sealed record AssistantQueryRequest(
    string Question,
    string? AgentName = null);

// Make Program accessible to tests
public partial class Program;
