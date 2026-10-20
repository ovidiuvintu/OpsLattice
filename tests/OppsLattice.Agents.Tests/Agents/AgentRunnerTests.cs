
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
            .ReturnsAsync(_toolResult);

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
            Times.Exactly(5));
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
            .ReturnsAsync(_toolResult);

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

        var agent = new AirportOperationsAgent(runner);

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
}
