
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Agents;
using OpsLattice.Agents.Execution;
using OpsLattice.Agents.Infrastructure;
using OpsLattice.Agents.Orchestration;

namespace OpsLattice.Api.Extensions;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddOpsLatticeAgents(
        this IServiceCollection services)
    {
        services.AddSingleton<IAgentToolGateway>(
            _ => new McpAgentToolGateway(
                new Uri("http://localhost:5160/mcp")));

        services.AddScoped<AgentRunner>();

        services
            .AddHttpClient<ILlmClient, OllamaLlmClient>(client =>
            {
                client.BaseAddress =
                    new Uri("http://127.0.0.1:11434");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    UseProxy = false
                });

        services.AddScoped<AirportOperationsAgent>();
        services.AddScoped<GateAgent>();

        services.AddScoped<IAgent>(
            provider =>
                provider.GetRequiredService<AirportOperationsAgent>());

        services.AddScoped<IAgent>(
            provider =>
                provider.GetRequiredService<GateAgent>());

        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}
