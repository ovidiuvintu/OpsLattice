
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Orchestration;
using Xunit;

namespace OppsLattice.Agents.Tests.Execution;

public sealed class AgentRunnerTests
{
    private const string AgentName = "gate";
    private const string SystemPrompt = "Use MCP tools.";
    private const string Question = "How many gates are available?";
    private const string ToolName = "get_gate_summary";

    private const string ToolResult =
        """{"total":3,"available":3,"assigned":0,"occupied":0}""";

    private static AgentTool CreateGateSummaryTool()
    {
        return new AgentTool(
            Name: ToolName,
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
            ToolName,
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
                AgentName,
                SystemPrompt,
                [ToolName],
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
                AgentName,
                SystemPrompt,
                [ToolName],
                Question));

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
                ToolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult);

        var runner = CreateRunner(llm, gateway);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                AgentName,
                SystemPrompt,
                [ToolName],
                Question));

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
                ToolName,
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
                ToolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult);

        var runner = CreateRunner(llm, gateway);

        // Act
        var result = await runner.RunAsync(
            AgentName,
            SystemPrompt,
            [ToolName],
            Question);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.ExecutionId));
        Assert.Equal(
            "There are 3 available gates.",
            result.Answer);

        Assert.Equal(2, result.LlmExecutions.Count);
        Assert.Single(result.ToolExecutions);

        var execution = result.ToolExecutions[0];

        Assert.Equal(ToolName, execution.ToolName);
        Assert.Equal(ToolResult, execution.Result);

        Assert.True(result.LlmExecutions[0].RequestedTools);
        Assert.False(result.LlmExecutions[1].RequestedTools);

        Assert.Contains(
            ToolName,
            result.LlmExecutions[0].RequestedToolNames);

        llm.Verify(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        gateway.Verify(x => x.CallToolAsync(
                ToolName,
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
