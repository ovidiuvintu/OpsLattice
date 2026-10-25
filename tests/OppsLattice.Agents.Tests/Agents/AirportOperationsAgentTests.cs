
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Agents;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Orchestration;
using Xunit;

namespace OppsLattice.Agents.Tests.Agents;

public sealed class AirportOperationsAgentTests
{
    [Fact]
    public async Task RunAsync_WhenLlmReturnsFinalAnswerWithoutToolCall_ThrowsException()
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
                "No flights are currently waiting for gates.",
                Array.Empty<AgentToolCall>()));

        var runner = new AgentRunner(
            llm.Object,
            gateway.Object,
            NullLogger<AgentRunner>.Instance);

        var agent = new AirportOperationsAgent(runner, gateway.Object);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RunAsync("Which flights are waiting for gates?"));

        // Assert
        Assert.Contains(
            "must execute an MCP tool",
            exception.Message);

        gateway.Verify(
            x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenQuestionUsesRelativeDate_ShouldAddSimulationDatesToSystemPrompt()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentTool>());

        gateway.Setup(x => x.CallToolAsync(
                "get_simulation_time",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolCallResult(true, "\"2026-10-06T00:00:00+00:00\""));

        IReadOnlyList<AgentMessage>? firstMessages = null;

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<AgentMessage>, IReadOnlyList<AgentTool>, CancellationToken>(
                (messages, _, _) => firstMessages ??= messages.ToArray())
            .ReturnsAsync(new AgentResponse(
                "There are no flights tomorrow.",
                Array.Empty<AgentToolCall>()));

        var runner = new AgentRunner(
            llm.Object,
            gateway.Object,
            NullLogger<AgentRunner>.Instance);

        var agent = new AirportOperationsAgent(runner, gateway.Object);

        // Act (the outcome of the run itself is covered by other tests)
        await Record.ExceptionAsync(
            () => agent.RunAsync("How many flights are scheduled tomorrow?"));

        // Assert
        Assert.NotNull(firstMessages);

        var systemPrompt = firstMessages![0].Content ?? string.Empty;

        Assert.Equal("system", firstMessages[0].Role);
        Assert.Contains("Today (simulation): 2026-10-06", systemPrompt);
        Assert.Contains("Tomorrow (simulation): 2026-10-07", systemPrompt);
        Assert.Contains("Yesterday (simulation): 2026-10-05", systemPrompt);
    }

    [Fact]
    public async Task RunAsync_WhenSimulationTimeLookupFails_ShouldStillRunWithoutContext()
    {
        // Arrange
        var llm = new Mock<ILlmClient>();
        var gateway = new Mock<IAgentToolGateway>();

        gateway.Setup(x => x.ListToolsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentTool>());

        gateway.Setup(x => x.CallToolAsync(
                "get_simulation_time",
                It.IsAny<IReadOnlyDictionary<string, object?>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("mcp unavailable"));

        IReadOnlyList<AgentMessage>? firstMessages = null;

        llm.Setup(x => x.ChatAsync(
                It.IsAny<IReadOnlyList<AgentMessage>>(),
                It.IsAny<IReadOnlyList<AgentTool>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<AgentMessage>, IReadOnlyList<AgentTool>, CancellationToken>(
                (messages, _, _) => firstMessages ??= messages.ToArray())
            .ReturnsAsync(new AgentResponse(
                "There are no flights tomorrow.",
                Array.Empty<AgentToolCall>()));

        var runner = new AgentRunner(
            llm.Object,
            gateway.Object,
            NullLogger<AgentRunner>.Instance);

        var agent = new AirportOperationsAgent(runner, gateway.Object);

        // Act
        await Record.ExceptionAsync(
            () => agent.RunAsync("How many flights are scheduled tomorrow?"));

        // Assert
        Assert.NotNull(firstMessages);
        Assert.DoesNotContain("SIMULATION CONTEXT", firstMessages![0].Content ?? string.Empty);
    }
}
