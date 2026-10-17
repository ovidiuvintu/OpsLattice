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

    public async Task<string> CallToolAsync(
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

        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"MCP tool '{name}' returned an error.");
        }

        var text = result.Content
            .OfType<TextContentBlock>()
            .Select(content => content.Text);

        return string.Join(Environment.NewLine, text);
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