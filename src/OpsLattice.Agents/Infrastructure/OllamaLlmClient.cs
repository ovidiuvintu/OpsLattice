using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpsLattice.Agents.Infrastructure;

public sealed class OllamaLlmClient(
    HttpClient httpClient) : ILlmClient
{
    public async Task<AgentResponse> ChatAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentTool> tools,
        CancellationToken cancellationToken = default)
    {
        var ollamaMessages = messages.Select(message =>
        {
            var json = new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };

            if (message.ToolName is not null)
                json["tool_name"] = message.ToolName;

            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();

                foreach (var call in message.ToolCalls)
                {
                    calls.Add(new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = JsonSerializer.SerializeToNode(
                                call.Arguments)
                        }
                    });
                }

                json["tool_calls"] = calls;
            }

            return json;
        }).ToArray();

        var ollamaTools = tools.Select(tool =>
            new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = tool.Parameters
                }
            }).ToArray();

        var request = new
        {
            model = "qwen2.5:3b-instruct-q4_K_M",
            messages = ollamaMessages,
            tools = ollamaTools,
            stream = false
        };

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(
                cancellationToken),
            cancellationToken: cancellationToken);

        var message = document.RootElement
            .GetProperty("message");

        var content = message.TryGetProperty(
            "content", out var contentElement)
                ? contentElement.GetString()
                : null;

        var toolCalls = new List<AgentToolCall>();

        if (message.TryGetProperty(
            "tool_calls", out var callsElement))
        {
            foreach (var call in callsElement.EnumerateArray())
            {
                var function = call.GetProperty("function");

                var name = function
                    .GetProperty("name")
                    .GetString()!;

                var arguments = JsonSerializer.Deserialize<
                    Dictionary<string, object?>>(
                        function.GetProperty("arguments")
                            .GetRawText())
                    ?? new Dictionary<string, object?>();

                toolCalls.Add(new AgentToolCall(
                    name, arguments));
            }
        }

        return new AgentResponse(content, toolCalls);
    }

    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = "qwen2.5:3b-instruct-q4_K_M",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = false
        };

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<
            OllamaChatResponse>(cancellationToken);

        return result?.Message?.Content
            ?? throw new InvalidOperationException(
                "Ollama returned an empty response.");
    }

    private sealed record OllamaChatResponse(
        OllamaMessage Message);

    private sealed record OllamaMessage(
        string Content);
}