
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Agents;
using OpsLattice.Agents.Infrastructure;
using OpsLattice.Agents.Orchestration;

namespace OpsLattice.Api.Extensions;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddOpsLatticeAgents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"]
            ?? "http://127.0.0.1:11434";

        var mcpBaseUrl = configuration["Mcp:BaseUrl"]
            ?? "http://localhost:5160/mcp";

        // MCP tool gateway
        services.AddSingleton<IAgentToolGateway>(_ =>
            new McpAgentToolGateway(new Uri(mcpBaseUrl)));

        // Agent execution
        services.AddScoped<AgentRunner>();

        // Local Ollama LLM
        services
            .AddHttpClient<ILlmClient, OllamaLlmClient>(client =>
            {
                client.BaseAddress = new Uri(ollamaBaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    UseProxy = false
                });

        // Agents
        services.AddScoped<AirportOperationsAgent>();
        services.AddScoped<GateAgent>();

        services.AddScoped<IAgent>(provider =>
            provider.GetRequiredService<AirportOperationsAgent>());

        services.AddScoped<IAgent>(provider =>
            provider.GetRequiredService<GateAgent>());

        // Agent orchestration
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}
