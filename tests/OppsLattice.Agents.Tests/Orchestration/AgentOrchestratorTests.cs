using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Orchestration;
using Xunit;

public sealed class AgentOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAgentExists_DelegatesExecution()
    {
        // Arrange
        var agent = new Mock<IAgent>();

        agent.SetupGet(x => x.Name)
            .Returns("airport-operations");

        var expected = new AgentExecutionResult(
            "execution-123",
            "There are 2150 scheduled flights.",
            100,
            Array.Empty<AgentLlmExecution>(),
            Array.Empty<AgentToolExecution>());

        agent.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var orchestrator = new AgentOrchestrator(
            new[] { agent.Object });

        // Act
        var result = await orchestrator.ExecuteAsync(
            "airport-operations",
            "How many flights are scheduled?");

        // Assert
        Assert.Same(expected, result);

        agent.Verify(x => x.RunAsync(
            "How many flights are scheduled?",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAgentDoesNotExist_ThrowsException()
    {
        // Arrange
        var orchestrator = new AgentOrchestrator(
            Array.Empty<IAgent>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ExecuteAsync(
                "unknown-agent",
                "Hello"));

        Assert.Contains("not registered", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var agent = new Mock<IAgent>();

        agent.SetupGet(x => x.Name)
            .Returns("airport-operations");

        agent.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                cts.Token))
            .ReturnsAsync(new AgentExecutionResult(
                "execution-123",
                "Done",
                10,
                Array.Empty<AgentLlmExecution>(),
                Array.Empty<AgentToolExecution>()));

        var orchestrator = new AgentOrchestrator(
            new[] { agent.Object });

        // Act
        await orchestrator.ExecuteAsync(
            "airport-operations",
            "Test",
            cts.Token);

        // Assert
        agent.Verify(x => x.RunAsync(
            "Test",
            cts.Token), Times.Once);
    }
}
