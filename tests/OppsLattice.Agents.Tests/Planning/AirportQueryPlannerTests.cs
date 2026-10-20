using Moq;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Planning;
using Xunit;

namespace OppsLattice.Agents.Tests.Planning;

public sealed class AirportQueryPlannerTests
{
    [Fact]
    public async Task BuildPlanAsync_WhenJsonIsMalformed_ShouldReturnError()
    {
        var llm = new Mock<ILlmClient>();

        llm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{ not-json }");

        var planner = new AirportQueryPlanner(llm.Object);

        var result = await planner.BuildPlanAsync("How many flights today?");

        Assert.Null(result.Plan);
        Assert.NotNull(result.Error);
        Assert.Contains("malformed JSON", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
