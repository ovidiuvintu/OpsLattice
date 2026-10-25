using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using OpsLattice.Api.Extensions;
using OpsLattice.Scenarios.Airport.Setup;
using OpsLattice.Scenarios.Airport.Simulation;

namespace OpsLattice.AgentEvaluation.Evaluation;

public sealed class EvaluationSession : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _ollamaModel;
    private readonly string _ollamaBaseUrl;

    private EvaluationSession(WebApplication app)
    {
        _app = app;
        _ollamaModel = app.Configuration["Ollama:Model"]
            ?? "qwen2.5:7b-instruct-q4_K_M";
        _ollamaBaseUrl = app.Configuration["Ollama:BaseUrl"]
            ?? "http://127.0.0.1:11434";
    }

    public static async Task<EvaluationSession> StartAsync(
        CancellationToken cancellationToken = default)
    {
        var port = ReserveLoopbackPort();
        var mcpBaseUrl = $"http://127.0.0.1:{port}/mcp";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = Environments.Production
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.AddFilter((_, level) => level >= LogLevel.Warning);
        builder.Configuration["Mcp:BaseUrl"] = mcpBaseUrl;

        builder.Services.AddAirportSimulation(builder.Environment);
        builder.Services.AddOpsLatticeAgents(builder.Configuration);
        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(FlightTools).Assembly);

        var app = builder.Build();
        ActivateSimulation(app.Services);
        app.MapMcp("/mcp");
        await app.StartAsync(cancellationToken);

        return new EvaluationSession(app);
    }

    public async Task<string> CallToolTextAsync(
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken = default)
    {
        var gateway = _app.Services.GetRequiredService<IAgentToolGateway>();
        var payload = arguments.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.OrdinalIgnoreCase);

        var result = await gateway.CallToolAsync(
            toolName,
            payload,
            cancellationToken);

        return result.Content;
    }

    public async Task<EvaluationReport> EvaluateAsync(
        IReadOnlyList<EvaluationScenario> scenarios,
        Action<ScenarioEvaluation>? onCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScenarioEvaluation>();

        foreach (var scenario in scenarios)
        {
            foreach (var agentName in EvaluationAgents.Names)
            {
                var evaluation = await EvaluateOneAsync(
                    scenario,
                    agentName,
                    cancellationToken);

                results.Add(evaluation);
                onCompleted?.Invoke(evaluation);
            }
        }

        return new EvaluationReport(
            DateTimeOffset.UtcNow,
            _ollamaModel,
            _ollamaBaseUrl,
            new SimulationDescriptor(
                ControlledSimulation.FlightCount,
                ControlledSimulation.ScheduleDate.ToString("yyyy-MM-dd"),
                ControlledSimulation.RandomSeed),
            results,
            ExpectationEvaluator.Summarize(results));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private async Task<ScenarioEvaluation> EvaluateOneAsync(
        EvaluationScenario scenario,
        string agentName,
        CancellationToken cancellationToken)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await orchestrator.ExecuteAsync(
                agentName,
                scenario.Question,
                cancellationToken);

            stopwatch.Stop();

            return ExpectationEvaluator.Evaluate(
                scenario,
                agentName,
                result.Answer,
                result.LlmExecutions.Count,
                stopwatch.ElapsedMilliseconds,
                Capture(result.ToolExecutions));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var message = $"{exception.GetType().Name}: {exception.Message}";

            return new ScenarioEvaluation(
                scenario.Id,
                scenario.Question,
                agentName,
                string.Empty,
                0,
                0,
                stopwatch.ElapsedMilliseconds,
                [],
                [],
                [message],
                false,
                [$"Agent execution failed. {message}"]);
        }
    }

    private static IReadOnlyList<CapturedToolCall> Capture(
        IReadOnlyList<AgentToolExecution> executions)
    {
        return executions
            .Select(execution => new CapturedToolCall(
                execution.ToolName,
                ToolCallCapture.FormatArguments(execution.Arguments),
                execution.Result,
                execution.DurationMs))
            .ToArray();
    }

    private static void ActivateSimulation(IServiceProvider services)
    {
        var setupService = services.GetRequiredService<AirportSimulationSetupService>();
        var configuration = new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("B", 10)
            ],
            FlightCount: ControlledSimulation.FlightCount,
            ScheduleDate: ControlledSimulation.ScheduleDate,
            RandomSeed: ControlledSimulation.RandomSeed);

        var definition = setupService.Build(configuration);
        var runtime = services
            .GetRequiredService<AirportSimulationRuntimeFactory>()
            .Create(definition);

        services
            .GetRequiredService<AirportSimulationManager>()
            .Activate(runtime);
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
