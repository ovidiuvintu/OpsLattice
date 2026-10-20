using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Infrastructure;

public sealed class McpAgentToolGateway(
    Uri endpoint) : IAgentToolGateway
{
    public async Task<IReadOnlyList<AgentTool>> ListToolsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var client = await CreateClientAsync(
            cancellationToken);

        var tools = await client.ListToolsAsync(
            cancellationToken: cancellationToken);

        return tools.Select(tool => new AgentTool(
            tool.Name,
            tool.Description ?? string.Empty,
            JsonSerializer.Deserialize<JsonElement>(
                tool.JsonSchema.GetRawText())
        )).ToArray();
    }

    public async Task<AgentToolCallResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        await using var client = await CreateClientAsync(
            cancellationToken);

        var result = await client.CallToolAsync(
            name,
            arguments,
            cancellationToken: cancellationToken);

        var text = string.Join(
            Environment.NewLine,
            result.Content
                .OfType<TextContentBlock>()
                .Select(content => content.Text));

        if (result.IsError == true)
        {
            if (string.IsNullOrWhiteSpace(text))
                text = $"MCP tool '{name}' returned a validation error.";

            return new AgentToolCallResult(
                IsSuccess: false,
                Content: text);
        }

        return new AgentToolCallResult(
            IsSuccess: true,
            Content: text);
    }

    private async Task<McpClient> CreateClientAsync(
        CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp
            });

        return await McpClient.CreateAsync(
            transport,
            cancellationToken: cancellationToken);
    }
}
