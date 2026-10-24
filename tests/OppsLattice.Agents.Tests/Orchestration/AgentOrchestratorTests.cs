using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Orchestration;
using Xunit;

public sealed class AgentOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAgenticAirportAgentSelected_UsesExactNameMatch()
    {
        var agentic = new Mock<IAgent>();
        var planned = new Mock<IAgent>();

        agentic.SetupGet(x => x.Name)
            .Returns("airport-operations");

        planned.SetupGet(x => x.Name)
            .Returns("airport-operations-planned");

        agentic.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult(
                "id-agentic",
                "agentic",
                10,
                Array.Empty<AgentLlmExecution>(),
                Array.Empty<AgentToolExecution>()));

        var orchestrator = new AgentOrchestrator(
            [agentic.Object, planned.Object]);

        var result = await orchestrator.ExecuteAsync(
            "airport-operations",
            "question");

        Assert.Equal("agentic", result.Answer);

        agentic.Verify(x => x.RunAsync(
            "question",
            It.IsAny<CancellationToken>()), Times.Once);

        planned.Verify(x => x.RunAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlannedAirportAgentSelected_UsesExactNameMatch()
    {
        var agentic = new Mock<IAgent>();
        var planned = new Mock<IAgent>();

        agentic.SetupGet(x => x.Name)
            .Returns("airport-operations");

        planned.SetupGet(x => x.Name)
            .Returns("airport-operations-planned");

        planned.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentExecutionResult(
                "id-planned",
                "planned",
                20,
                Array.Empty<AgentLlmExecution>(),
                Array.Empty<AgentToolExecution>()));

        var orchestrator = new AgentOrchestrator(
            [agentic.Object, planned.Object]);

        var result = await orchestrator.ExecuteAsync(
            "airport-operations-planned",
            "question");

        Assert.Equal("planned", result.Answer);

        planned.Verify(x => x.RunAsync(
            "question",
            It.IsAny<CancellationToken>()), Times.Once);

        agentic.Verify(x => x.RunAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

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
