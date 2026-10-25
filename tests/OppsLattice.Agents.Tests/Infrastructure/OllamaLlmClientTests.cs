using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsLattice.Agents.Infrastructure;
using OpsLattice.Agents.Models;
using Xunit;

namespace OppsLattice.Agents.Tests.Infrastructure;

public sealed class OllamaLlmClientTests
{
    private const string _okResponse =
        """{"message":{"role":"assistant","content":"ok"}}""";

    [Fact]
    public async Task ChatAsync_ShouldSendModelContextLengthAndThinkSettings()
    {
        var handler = new RecordingHandler(_ => Reply(HttpStatusCode.OK, _okResponse));
        var client = CreateClient(handler, new OllamaOptions
        {
            Model = "test-model-settings",
            ContextLength = 8192,
            Think = false
        });

        var response = await client.ChatAsync(
            [new AgentMessage("user", "hello")],
            []);

        Assert.Equal("ok", response.Content);

        var body = Assert.Single(handler.RequestBodies);
        Assert.Equal("test-model-settings", body["model"]!.GetValue<string>());
        Assert.False(body["think"]!.GetValue<bool>());
        Assert.Equal(8192, body["options"]!["num_ctx"]!.GetValue<int>());
        Assert.False(body["stream"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ChatAsync_WhenThinkIsNull_ShouldNotSendThink()
    {
        var handler = new RecordingHandler(_ => Reply(HttpStatusCode.OK, _okResponse));
        var client = CreateClient(handler, new OllamaOptions
        {
            Model = "test-model-think-null",
            Think = null
        });

        await client.ChatAsync([new AgentMessage("user", "hello")], []);

        var body = Assert.Single(handler.RequestBodies);
        Assert.False(body.ContainsKey("think"));
    }

    [Fact]
    public async Task GenerateAsync_WhenModelDoesNotSupportThinking_ShouldRetryWithoutThinkAndRemember()
    {
        var handler = new RecordingHandler(body => body.ContainsKey("think")
            ? Reply(HttpStatusCode.BadRequest, """{"error":"model does not support thinking"}""")
            : Reply(HttpStatusCode.OK, _okResponse));

        var client = CreateClient(handler, new OllamaOptions
        {
            Model = "test-model-no-thinking",
            Think = false
        });

        var first = await client.GenerateAsync("hello");
        var second = await client.GenerateAsync("hello again");

        Assert.Equal("ok", first);
        Assert.Equal("ok", second);

        // First call: rejected with think, retried without. Second call: no think at all.
        Assert.Equal(3, handler.RequestBodies.Count);
        Assert.True(handler.RequestBodies[0].ContainsKey("think"));
        Assert.False(handler.RequestBodies[1].ContainsKey("think"));
        Assert.False(handler.RequestBodies[2].ContainsKey("think"));
    }

    [Fact]
    public async Task GenerateAsync_WhenBadRequestIsUnrelatedToThinking_ShouldNotRetry()
    {
        var handler = new RecordingHandler(_ =>
            Reply(HttpStatusCode.BadRequest, """{"error":"model not found"}"""));

        var client = CreateClient(handler, new OllamaOptions
        {
            Model = "test-model-missing",
            Think = false
        });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync("hello"));

        Assert.Single(handler.RequestBodies);
    }

    private static OllamaLlmClient CreateClient(
        RecordingHandler handler,
        OllamaOptions options)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://ollama.test")
        };

        return new OllamaLlmClient(
            httpClient,
            Options.Create(options),
            NullLogger<OllamaLlmClient>.Instance);
    }

    private static HttpResponseMessage Reply(
        HttpStatusCode statusCode,
        string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHandler(
        Func<JsonObject, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<JsonObject> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = JsonNode.Parse(json)!.AsObject();

            RequestBodies.Add(body);

            return respond(body);
        }
    }
}
