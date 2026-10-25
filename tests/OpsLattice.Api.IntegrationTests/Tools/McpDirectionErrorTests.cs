using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace OpsLattice.Api.IntegrationTests.Tools;

public sealed class McpDirectionErrorTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpDirectionErrorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetFlightCount_WhenDepartureOriginIsWrong_ShouldReturnTheDirectionError()
    {
        var httpClient = _factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient,
            NullLoggerFactory.Instance,
            ownsHttpClient: false);

        await using var client = await McpClient.CreateAsync(transport);

        var result = await client.CallToolAsync(
            "get_flight_count",
            new Dictionary<string, object?>
            {
                ["flightType"] = "Departure",
                ["origin"] = "LAX",
                ["destination"] = "IAH",
                ["date"] = "2026-10-06"
            });

        var text = string.Join(
            Environment.NewLine,
            result.Content.OfType<TextContentBlock>().Select(block => block.Text));

        Assert.True(result.IsError);
        Assert.Contains("origin=IAH", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Departure flights", text, StringComparison.OrdinalIgnoreCase);
    }
}
