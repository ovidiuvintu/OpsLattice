using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;

namespace OpsLattice.Agents.Infrastructure;

public sealed class OllamaLlmClient(
    HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaLlmClient> logger) : ILlmClient
{
    public async Task<AgentResponse> ChatAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<AgentTool> tools,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();

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
            model = options.Value.Model,
            messages = ollamaMessages,
            tools = ollamaTools,
            stream = false
        };

        stageStopwatch.Stop();

        logger.LogInformation(
            "Ollama request preparation: {DurationMs} ms",
            stageStopwatch.ElapsedMilliseconds);

        stageStopwatch.Restart();

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        stageStopwatch.Stop();

        logger.LogInformation(
            "Ollama HTTP execution: {DurationMs} ms",
            stageStopwatch.ElapsedMilliseconds);

        stageStopwatch.Restart();

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(
                cancellationToken),
            cancellationToken: cancellationToken);

        stageStopwatch.Stop();

        logger.LogInformation(
            "Ollama response parsing: {DurationMs} ms",
            stageStopwatch.ElapsedMilliseconds);

        var root = document.RootElement;

        static long ReadLong(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Number
                ? value.GetInt64()
                : 0;
        }

        var totalDuration = ReadLong(root, "total_duration");
        var loadDuration = ReadLong(root, "load_duration");
        var promptEvalDuration = ReadLong(root, "prompt_eval_duration");
        var evalDuration = ReadLong(root, "eval_duration");
        var promptTokens = ReadLong(root, "prompt_eval_count");
        var generatedTokens = ReadLong(root, "eval_count");

        logger.LogInformation(
            "Ollama metrics: Total={TotalMs}ms, Load={LoadMs}ms, " +
            "PromptEval={PromptEvalMs}ms ({PromptTokens} tokens), " +
            "Generation={GenerationMs}ms ({GeneratedTokens} tokens)",
            totalDuration / 1_000_000.0,
            loadDuration / 1_000_000.0,
            promptEvalDuration / 1_000_000.0,
            promptTokens,
            evalDuration / 1_000_000.0,
            generatedTokens
        );

        stageStopwatch.Restart();

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

        stageStopwatch.Stop();
        totalStopwatch.Stop();

        logger.LogInformation(
            "Ollama response mapping: {MappingMs} ms. Total ChatAsync: {TotalMs} ms",
            stageStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds);

        return new AgentResponse(content, toolCalls);
    }

    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = options.Value.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = false,
            keep_alive = "30m"
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
