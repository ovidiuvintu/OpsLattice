using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Planning;
using System.Text.Json;
using Xunit;

namespace OppsLattice.Agents.Tests.Planning;

public sealed class AirportQueryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCountFlightsQueryProvided_ShouldMapToFlightCountToolWithDeterministicArguments()
    {
        var gateway = CreateGatewayWithTools(
            CreateTool("get_flight_count", ["origin", "destination", "flightType", "date"]));

        IReadOnlyDictionary<string, object?>? capturedArgs = null;

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, CancellationToken>((_, args, _) => capturedArgs = args)
            .ReturnsAsync(new AgentToolCallResult(true, "50"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Arrival,
                null,
                null,
                "IAH",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsSuccess);
        Assert.Equal("get_flight_count", result.Items[0].ToolName);
        Assert.Equal("50", result.Items[0].Result);

        Assert.NotNull(capturedArgs);
        Assert.Equal("Arrival", capturedArgs!["flightType"]);
        Assert.Equal("IAH", capturedArgs["destination"]);
        Assert.Equal("2026-10-06", capturedArgs["date"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleQueriesProvided_ShouldPreserveOrderAndAssociation()
    {
        var gateway = CreateGatewayWithTools(
            CreateTool("get_flight_count", ["origin", "destination", "flightType", "date"]),
            CreateTool("get_flight_summary", []));

        gateway.SetupSequence(x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolCallResult(true, "50"))
            .ReturnsAsync(new AgentToolCallResult(true, "{\"total\":100,\"scheduled\":100}"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Departure,
                null,
                "IAH",
                null,
                new DateOnly(2026, 10, 6),
                null),
            new AirportQuery(
                AirportQueryOperation.FlightSummary,
                null,
                null,
                null,
                null,
                null,
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(0, result.Items[0].QueryIndex);
        Assert.Equal(1, result.Items[1].QueryIndex);
        Assert.Equal("get_flight_count", result.Items[0].ToolName);
        Assert.Equal("get_flight_summary", result.Items[1].ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnsupportedFilterRequested_ShouldReturnFailedExecutionWithoutCallingTool()
    {
        var gateway = CreateGatewayWithTools(
            CreateTool("get_flight_count", ["origin", "destination", "flightType", "date"]));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Arrival,
                AirportFlightStatus.Scheduled,
                null,
                "IAH",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.Single(result.Items);
        Assert.False(result.Items[0].IsSuccess);
        Assert.Contains("does not support argument", result.Items[0].Result, StringComparison.OrdinalIgnoreCase);

        gateway.Verify(x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolReturnsRecoverableError_ShouldRecordFailedExecution()
    {
        var gateway = CreateGatewayWithTools(
            CreateTool("get_flight_count", ["origin", "destination", "flightType", "date"]));

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolCallResult(false, "validation error"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Arrival,
                null,
                null,
                "IAH",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.Single(result.Items);
        Assert.False(result.Items[0].IsSuccess);
        Assert.Equal("validation error", result.Items[0].Result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInfrastructureFailureOccurs_ShouldPropagateException()
    {
        var gateway = CreateGatewayWithTools(
            CreateTool("get_flight_count", ["origin", "destination", "flightType", "date"]));

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("gateway unavailable"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Arrival,
                null,
                null,
                "IAH",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => executor.ExecuteAsync(plan));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCountSchemaIncludesStatus_ShouldPassExplicitStatusAndAcceptZero()
    {
        var gateway = CreateGatewayWithTools(CreateLiveFlightCountTool());

        IReadOnlyDictionary<string, object?>? capturedArgs = null;

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, CancellationToken>((_, args, _) => capturedArgs = args)
            .ReturnsAsync(new AgentToolCallResult(true, "0"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Arrival,
                AirportFlightStatus.Scheduled,
                null,
                "IAH",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsSuccess);
        Assert.Equal("0", result.Items[0].Result);
        Assert.NotNull(capturedArgs);
        Assert.Equal("Scheduled", capturedArgs!["status"]);
        Assert.Equal("Arrival", capturedArgs["flightType"]);
        Assert.Equal("IAH", capturedArgs["destination"]);
        Assert.Equal("2026-10-06", capturedArgs["date"]);
        Assert.False(capturedArgs.ContainsKey("origin"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenScheduleDateQueryHasNoStatus_ShouldOmitStatusArgument()
    {
        var gateway = CreateGatewayWithTools(CreateLiveFlightCountTool());

        IReadOnlyDictionary<string, object?>? capturedArgs = null;

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, CancellationToken>((_, args, _) => capturedArgs = args)
            .ReturnsAsync(new AgentToolCallResult(true, "100"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                null,
                null,
                null,
                "IAH",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.True(result.Items[0].IsSuccess);
        Assert.NotNull(capturedArgs);
        Assert.False(capturedArgs!.ContainsKey("status"));
        Assert.Equal("IAH", capturedArgs["destination"]);
        Assert.Equal("2026-10-06", capturedArgs["date"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenListFlightsQueryProvided_ShouldCallSearchFlightsWithFilters()
    {
        var gateway = CreateGatewayWithTools(
            CreateTool("search_flights", ["origin", "destination", "flightType", "date", "status"]));

        IReadOnlyDictionary<string, object?>? capturedArgs = null;

        gateway.Setup(x => x.CallToolAsync(
                "search_flights",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, CancellationToken>((_, args, _) => capturedArgs = args)
            .ReturnsAsync(new AgentToolCallResult(true, "[]"));

        var executor = new AirportQueryExecutor(gateway.Object);

        var plan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.ListFlights,
                AirportFlightType.Departure,
                AirportFlightStatus.Delayed,
                "IAH",
                "ORD",
                new DateOnly(2026, 10, 6),
                null)
        ]);

        var result = await executor.ExecuteAsync(plan);

        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsSuccess);
        Assert.Equal("search_flights", result.Items[0].ToolName);

        Assert.NotNull(capturedArgs);
        Assert.Equal("IAH", capturedArgs!["origin"]);
        Assert.Equal("ORD", capturedArgs["destination"]);
        Assert.Equal("Departure", capturedArgs["flightType"]);
        Assert.Equal("Delayed", capturedArgs["status"]);
        Assert.Equal("2026-10-06", capturedArgs["date"]);
    }

    private static Mock<IAgentToolGateway> CreateGatewayWithTools(
        params AgentTool[] tools)
    {
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);

        return gateway;
    }

    private static AgentTool CreateTool(
        string name,
        IReadOnlyList<string> parameterNames)
    {
        var properties = parameterNames.ToDictionary(
            p => p,
            _ => (object)new { type = "string" });

        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required = Array.Empty<string>()
        });

        return new AgentTool(name, name, schema);
    }

    private static AgentTool CreateLiveFlightCountTool()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "origin": { "type": ["string", "null"], "default": null },
                "destination": { "type": ["string", "null"], "default": null },
                "flightType": { "type": ["string", "null"], "default": null },
                "date": { "type": ["string", "null"], "default": null },
                "status": { "type": ["string", "null"], "default": null }
              }
            }
            """;

        return new AgentTool(
            "get_flight_count",
            "get_flight_count",
            JsonSerializer.Deserialize<JsonElement>(schemaJson));
    }
}
