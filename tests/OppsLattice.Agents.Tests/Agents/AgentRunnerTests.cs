
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Agents;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Orchestration;
using Xunit;

namespace OppsLattice.Agents.Tests.Execution;

public sealed class AgentRunnerTests
{
    private const string _agentName = "gate";
    private const string _systemPrompt = "Use MCP tools.";
    private const string _question = "How many gates are available?";
    private const string _toolName = "get_gate_summary";

    private const string _toolResult =
        """{"total":3,"available":3,"assigned":0,"occupied":0}""";

    private static AgentToolCallResult CreateSuccessToolResult(
        string content)
    {
        return new AgentToolCallResult(
            IsSuccess: true,
            Content: content);
    }

    private static AgentToolCallResult CreateErrorToolResult(
        string content)
    {
        return new AgentToolCallResult(
            IsSuccess: false,
            Content: content);
    }

    private static AgentTool CreateGateSummaryTool()
    {
        return new AgentTool(
            Name: _toolName,
            Description: "Returns the current gate counts.",
            Parameters: new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            });
    }

    private static AgentToolCall CreateGateSummaryCall()
    {
        return new AgentToolCall(
            _toolName,
            new Dictionary<string, object?>());
    }

    private static AgentTool CreateFlightCountTool()
    {
        return new AgentTool(
            Name: "get_flight_count",
            Description: "Counts flights matching filters.",
            Parameters: new
            {
                type = "object",
                properties = new
                {
                    origin = new { type = "string" },
                    destination = new { type = "string" },
                    flightType = new { type = "string" },
                    date = new { type = "string" }
                },
                required = Array.Empty<string>()
            });
    }

    private static void SetupAvailableTools(
        Mock<IAgentToolGateway> gateway)
    {
        AgentTool[] tools = [CreateGateSummaryTool()];

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);
    }

    private static AgentRunner CreateRunner(
        Mock<ILlmClient> llm,
        Mock<IAgentToolGateway> gateway)
    {
        return new AgentRunner(
            llm.Object,
            gateway.Object,
            NullLogger<AgentRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_WhenLlmRequestsUnauthorizedTool_ShouldThrow()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        var unauthorizedCall = new AgentToolCall(
            "delete_flight",
            new Dictionary<string, object?>
            {
                ["flightNumber"] = "UA123"
            });

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                null,
                [unauthorizedCall]));

        var runner = CreateRunner(llm, gateway);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                _agentName,
                _systemPrompt,
                [_toolName],
                "Delete flight UA123"));

        // Assert
        Assert.Contains("not permitted", exception.Message);

        gateway.Verify(x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenLlmAnswersWithoutTool_ShouldThrow()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                "There are 3 available gates.",
                []));

        var runner = CreateRunner(llm, gateway);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                _agentName,
                _systemPrompt,
                [_toolName],
                _question));

        // Assert
        Assert.Contains(
            "must execute an MCP tool",
            exception.Message);

        gateway.Verify(x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenMaxIterationsExceeded_ShouldThrow()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        var toolCall = CreateGateSummaryCall();

        // LLM continually requests the same tool.
        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                null,
                [toolCall]));

        gateway.Setup(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult(_toolResult));

        var runner = CreateRunner(llm, gateway);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                _agentName,
                _systemPrompt,
                [_toolName],
                _question));

        // Assert
        Assert.Contains(
            "maximum number of iterations",
            exception.Message);

        llm.Verify(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        gateway.Verify(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenSameSuccessfulToolCallIsRepeated_ShouldReuseCachedResult()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        var toolCall = CreateGateSummaryCall();

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(null, [toolCall]))
            .ReturnsAsync(new AgentResponse(null, [toolCall]))
            .ReturnsAsync(new AgentResponse("There are 3 available gates.", []));

        gateway.Setup(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult(_toolResult));

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            _agentName,
            _systemPrompt,
            [_toolName],
            _question);

        // Assert
        Assert.Equal("There are 3 available gates.", result.Answer);
        Assert.Equal(2, result.ToolExecutions.Count);
        Assert.Equal(_toolResult, result.ToolExecutions[0].Result);
        Assert.Equal(_toolResult, result.ToolExecutions[1].Result);

        gateway.Verify(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenToolExecutes_ShouldReturnExecutionTrace()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        var toolCall = CreateGateSummaryCall();

        // First LLM response requests a tool.
        // Second LLM response provides the final answer.
        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                null,
                [toolCall]))
            .ReturnsAsync(new AgentResponse(
                "There are 3 available gates.",
                []));

        gateway.Setup(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult(_toolResult));

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            _agentName,
            _systemPrompt,
            [_toolName],
            _question);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.ExecutionId));
        Assert.Equal(
            "There are 3 available gates.",
            result.Answer);

        Assert.Equal(2, result.LlmExecutions.Count);
        Assert.Single(result.ToolExecutions);

        var execution = result.ToolExecutions[0];

        Assert.Equal(_toolName, execution.ToolName);
        Assert.Equal(_toolResult, execution.Result);

        Assert.True(result.LlmExecutions[0].RequestedTools);
        Assert.False(result.LlmExecutions[1].RequestedTools);

        Assert.Contains(
            _toolName,
            result.LlmExecutions[0].RequestedToolNames);

        llm.Verify(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        gateway.Verify(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenToolReturnsRecoverableError_ShouldPassErrorBackToLlmAndAllowRetry()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        var toolCall = CreateGateSummaryCall();
        const string toolError = "gateType must be one of A, B, or C.";
        var llmIteration = 0;

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                IReadOnlyList<AgentMessage> messages,
                IReadOnlyList<AgentTool> tools,
                CancellationToken cancellationToken) =>
            {
                llmIteration++;

                if (llmIteration == 1)
                    return new AgentResponse(null, [toolCall]);

                if (llmIteration == 2)
                {
                    Assert.Contains(messages, m =>
                        m.Role == "tool" &&
                        m.ToolName == _toolName &&
                        m.Content == toolError);

                    return new AgentResponse(null, [toolCall]);
                }

                return new AgentResponse("There are 3 available gates.", []);
            });

        gateway.SetupSequence(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorToolResult(toolError))
            .ReturnsAsync(CreateSuccessToolResult(_toolResult));

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            _agentName,
            _systemPrompt,
            [_toolName],
            _question);

        // Assert
        Assert.Equal("There are 3 available gates.", result.Answer);
        Assert.Equal(2, result.ToolExecutions.Count);
        Assert.Equal(toolError, result.ToolExecutions[0].Result);
        Assert.Equal(_toolResult, result.ToolExecutions[1].Result);
    }

    [Fact]
    public async Task RunAsync_WhenFlightCountDirectionIsReversed_ShouldReturnRecoverableErrorWithoutCallingMcpAndAllowRetry()
    {
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFlightCountTool()]);

        var reversedCall = new AgentToolCall(
            "get_flight_count",
            new Dictionary<string, object?>
            {
                ["flightType"] = "Arrival",
                ["origin"] = "IAH",
                ["date"] = "2026-10-06"
            });

        var correctedCall = new AgentToolCall(
            "get_flight_count",
            new Dictionary<string, object?>
            {
                ["flightType"] = "Arrival",
                ["destination"] = "IAH",
                ["date"] = "2026-10-06"
            });

        var llmIteration = 0;

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                IReadOnlyList<AgentMessage> messages,
                IReadOnlyList<AgentTool> tools,
                CancellationToken cancellationToken) =>
            {
                llmIteration++;

                if (llmIteration == 1)
                    return new AgentResponse(null, [reversedCall]);

                if (llmIteration == 2)
                {
                    Assert.Contains(messages, m =>
                        m.Role == "tool"
                        && m.ToolName == "get_flight_count"
                        && m.Content is not null
                        && m.Content.Contains("likely reversed", StringComparison.OrdinalIgnoreCase));

                    return new AgentResponse(null, [correctedCall]);
                }

                return new AgentResponse("There are 50 arrivals.", []);
            });

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.Is<IReadOnlyDictionary<string, object?>>(args =>
                    args.ContainsKey("destination")
                    && !args.ContainsKey("origin")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult("50"));

        var runner = CreateRunner(llm, gateway);

        var result = await runner.RunAsync(
            "airport-operations",
            "Use MCP tools.",
            ["get_flight_count"],
            "How many arrivals are scheduled at IAH on October 6, 2026?");

        Assert.Equal("There are 50 arrivals.", result.Answer);

        Assert.Equal(2, result.ToolExecutions.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.ToolExecutions[0].Result));
        Assert.Equal("50", result.ToolExecutions[1].Result);

        gateway.Verify(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenAllToolExecutionsFail_ShouldThrowBeforeOperationalAnswer()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        SetupAvailableTools(gateway);

        var toolCall = CreateGateSummaryCall();

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(null, [toolCall]))
            .ReturnsAsync(new AgentResponse("There are 3 available gates.", []));

        gateway.Setup(x => x.CallToolAsync(
                _toolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateErrorToolResult("invalid input."));

        var runner = CreateRunner(llm, gateway);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                _agentName,
                _systemPrompt,
                [_toolName],
                _question));

        // Assert
        Assert.Contains("must execute an MCP tool before answering", exception.Message);
    }

    [Fact]
    public async Task RunAsync_WhenLlmRequestsClarificationWithoutToolCall_ReturnsClarification()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentTool>());

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                "Which year do you mean?",
                Array.Empty<AgentToolCall>(),
                AgentResponseType.Clarification));

        var runner = new AgentRunner(
            llm.Object,
            gateway.Object,
            NullLogger<AgentRunner>.Instance);

        var agent = new AirportOperationsAgent(runner, gateway.Object);

        // Act
        var result = await agent.RunAsync(
            "How many flights on October 6?");

        // Assert
        Assert.Equal(
            "Which year do you mean?",
            result.Answer);

        Assert.Empty(result.ToolExecutions);

        gateway.Verify(
            x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenAirportAgentAsksYearForToday_ShouldRepromptAndExecuteTools()
    {
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        AgentTool[] tools =
        [
            CreateFlightCountTool(),
            new AgentTool(
                Name: "get_simulation_time",
                Description: "Returns simulation time.",
                Parameters: new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                })
        ];

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                "Which year do you mean for today?",
                Array.Empty<AgentToolCall>(),
                AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse(
                null,
                [
                    new AgentToolCall("get_simulation_time", new Dictionary<string, object?>())
                ]))
            .ReturnsAsync(new AgentResponse(
                null,
                [
                    new AgentToolCall(
                        "get_flight_count",
                        new Dictionary<string, object?>
                        {
                            ["origin"] = "IAH",
                            ["destination"] = "LAX",
                            ["flightType"] = "Departure",
                            ["date"] = "2026-10-06"
                        })
                ]))
            .ReturnsAsync(new AgentResponse(
                "There are 5 flights from IAH to LAX today.",
                Array.Empty<AgentToolCall>()));

        gateway.Setup(x => x.CallToolAsync(
                "get_simulation_time",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult("2026-10-06T10:00:00Z"));

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult("5"));

        var runner = CreateRunner(llm, gateway);

        var result = await runner.RunAsync(
            "airport-operations",
            "Use MCP tools.",
            ["get_flight_count", "get_simulation_time"],
            "How many flights are flying from IAH to LAX today?");

        Assert.Equal("There are 5 flights from IAH to LAX today.", result.Answer);

        Assert.Contains(result.ToolExecutions, t => t.ToolName == "get_simulation_time");
        Assert.Contains(result.ToolExecutions, t => t.ToolName == "get_flight_count");

        gateway.Verify(x => x.CallToolAsync(
                "get_simulation_time",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        gateway.Verify(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenAirportAgentAnswersWithoutTool_ShouldRepromptAndExecuteTool()
    {
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        AgentTool[] tools =
        [
            new AgentTool(
                Name: "get_simulation_time",
                Description: "Returns simulation time.",
                Parameters: new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                })
        ];

        gateway.Setup(x => x.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                "There are 8 flights departing from LAX and arriving at IAH today.",
                Array.Empty<AgentToolCall>()))
            .ReturnsAsync(new AgentResponse(
                null,
                [new AgentToolCall("get_simulation_time", new Dictionary<string, object?>())]))
            .ReturnsAsync(new AgentResponse(
                "There are 8 flights departing from LAX and arriving at IAH today.",
                Array.Empty<AgentToolCall>()));

        gateway.Setup(x => x.CallToolAsync(
                "get_simulation_time",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult("2026-10-06T10:00:00Z"));

        var runner = CreateRunner(llm, gateway);

        var result = await runner.RunAsync(
            "airport-operations",
            "Use MCP tools.",
            ["get_simulation_time"],
            "How many flights are departing LAX and arriving at IAH today?");

        Assert.Equal(
            "There are 8 flights departing from LAX and arriving at IAH today.",
            result.Answer);

        Assert.Contains(result.ToolExecutions, t => t.ToolName == "get_simulation_time");

        gateway.Verify(x => x.CallToolAsync(
                "get_simulation_time",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenAirportAgentHitsMaxIterations_ShouldReturnFallbackInsteadOfThrowing()
    {
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentTool>());

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse("Which year do you mean for today?", Array.Empty<AgentToolCall>(), AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse("Please provide the full date with year for today.", Array.Empty<AgentToolCall>(), AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse("Which relative date do you mean for today?", Array.Empty<AgentToolCall>(), AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse("I need the exact date and year for today.", Array.Empty<AgentToolCall>(), AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse("Please confirm the year for today.", Array.Empty<AgentToolCall>(), AgentResponseType.Clarification));

        var runner = CreateRunner(llm, gateway);

        var result = await runner.RunAsync(
            "airport-operations",
            "Use MCP tools.",
            ["get_simulation_time"],
            "How many flights are departing LAX and arriving at IAH today?");

        Assert.Contains("couldn't complete the request", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Last model response", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Please confirm the year for today.", result.Answer, StringComparison.Ordinal);
        Assert.Equal(5, result.LlmExecutions.Count);
    }

    [Fact]
    public async Task RunAsync_WhenQuestionMatchesScheduledAtAirportOnDate_ShouldUseDeterministicDualCounts()
    {
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();
        var capturedCallArguments = new List<IReadOnlyDictionary<string, object?>>();
        var responseQueue = new Queue<AgentToolCallResult>(
        [
            CreateSuccessToolResult("8"),
            CreateSuccessToolResult("10")
        ]);

        gateway.Setup(x => x.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFlightCountTool()]);

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, object?>, CancellationToken>((_, args, _) =>
                capturedCallArguments.Add(args))
            .ReturnsAsync(() => responseQueue.Dequeue());

        var runner = CreateRunner(llm, gateway);

        var result = await runner.RunAsync(
            "airport-operations",
            "Use MCP tools.",
            ["get_flight_count"],
            "How many flights are scheduled at IAH on October 6, 2026?");

        Assert.Equal(
            "There are 18 flights scheduled at IAH on October 6, 2026 (arrivals: 8, departures: 10).",
            result.Answer);
        Assert.Empty(result.LlmExecutions);
        Assert.Equal(2, result.ToolExecutions.Count);

        Assert.Equal(2, capturedCallArguments.Count);

        Assert.Equal("IAH", capturedCallArguments[0]["destination"]);
        Assert.Equal("Arrival", capturedCallArguments[0]["flightType"]);
        Assert.Equal("2026-10-06", capturedCallArguments[0]["date"]);

        Assert.Equal("IAH", capturedCallArguments[1]["origin"]);
        Assert.Equal("Departure", capturedCallArguments[1]["flightType"]);
        Assert.Equal("2026-10-06", capturedCallArguments[1]["date"]);

        llm.Verify(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        gateway.Verify(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_WhenAirportAgentOffersToListInsteadOfListing_ShouldRepromptOnceToCallSearchFlights()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        AgentTool[] tools =
        [
            CreateFlightCountTool(),
            new AgentTool(
                Name: "search_flights",
                Description: "Lists flights matching filters.",
                Parameters: new
                {
                    type = "object",
                    properties = new
                    {
                        destination = new { type = "string" },
                        flightType = new { type = "string" }
                    },
                    required = Array.Empty<string>()
                })
        ];

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);

        var filters = new Dictionary<string, object?>
        {
            ["destination"] = "DEN",
            ["flightType"] = "Departure"
        };

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(null, [new AgentToolCall("get_flight_count", filters)]))
            .ReturnsAsync(new AgentResponse(
                "There are 2 flights to DEN. Would you like me to list the details?",
                []))
            .ReturnsAsync(new AgentResponse(null, [new AgentToolCall("search_flights", filters)]))
            .ReturnsAsync(new AgentResponse(
                "There are 2 flights to DEN: AA1041 and WN1053.",
                []));

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult("2"));

        gateway.Setup(x => x.CallToolAsync(
                "search_flights",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult(
                "[{\"flightNumber\":\"AA1041\"},{\"flightNumber\":\"WN1053\"}]"));

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            "airport-operations",
            "system prompt",
            ["get_flight_count", "search_flights"],
            "How many flights to DEN? If you find any flights to DEN, can you list the details?");

        // Assert
        Assert.Equal("There are 2 flights to DEN: AA1041 and WN1053.", result.Answer);
        Assert.Contains(result.ToolExecutions, execution => execution.ToolName == "search_flights");

        gateway.Verify(x => x.CallToolAsync(
                "search_flights",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenAirportAgentAsksForYearOfCompleteDate_ShouldPushBackOnceAndUseTools()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFlightCountTool()]);

        var arrivals = new Dictionary<string, object?>
        {
            ["origin"] = "LAX",
            ["destination"] = "IAH",
            ["flightType"] = "Arrival",
            ["date"] = "2026-10-06"
        };

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                "Which year do you mean for October 6?",
                [],
                AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse(null, [new AgentToolCall("get_flight_count", arrivals)]))
            .ReturnsAsync(new AgentResponse("There are 8 arrivals from LAX on October 6, 2026.", []));

        gateway.Setup(x => x.CallToolAsync(
                "get_flight_count",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessToolResult("8"));

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            "airport-operations",
            "system prompt",
            ["get_flight_count"],
            "How many flights are arriving from LAX on October 6, 2026?");

        // Assert
        Assert.Equal("There are 8 arrivals from LAX on October 6, 2026.", result.Answer);
        Assert.Equal(3, result.LlmExecutions.Count);
        Assert.Single(result.ToolExecutions);
    }

    [Fact]
    public async Task RunAsync_WhenAirportAgentKeepsAskingAfterPushBack_ShouldReturnItsQuestion()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateFlightCountTool()]);

        llm.SetupSequence(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResponse(
                "Which airline do you mean?",
                [],
                AgentResponseType.Clarification))
            .ReturnsAsync(new AgentResponse(
                "I still need the airline.",
                [],
                AgentResponseType.Clarification));

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            "airport-operations",
            "system prompt",
            ["get_flight_count"],
            "How many flights does the airline operate on October 6, 2026?");

        // Assert
        Assert.Equal("I still need the airline.", result.Answer);
        Assert.Equal(2, result.LlmExecutions.Count);
        Assert.Empty(result.ToolExecutions);
    }
}
