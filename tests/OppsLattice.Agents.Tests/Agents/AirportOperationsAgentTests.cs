
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Agents;
using OpsLattice.Agents.Models;
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

        var agent = new AirportOperationsAgent(
            llm.Object,
            gateway.Object,
            NullLogger<AirportOperationsAgent>.Instance);

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
}
