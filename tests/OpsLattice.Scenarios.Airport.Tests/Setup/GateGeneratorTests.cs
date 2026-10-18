using OpsLattice.Scenarios.Airport.Setup;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Setup;

public sealed class GateGeneratorTests
{
    private readonly GateGenerator _generator = new();

    [Fact]
    public void Generate_ShouldCreateRequestedNumberOfGates()
    {
        var gates = _generator.Generate(12);

        Assert.Equal(12, gates.Count);
    }

    [Fact]
    public void Generate_ShouldAssignSequentialGateCodes()
    {
        var gates = _generator.Generate(3);

        Assert.Equal(
            ["B10", "B11", "B12"],
            gates.Select(g => g.Code));
    }

    [Fact]
    public void Generate_ShouldCreateAvailableGates()
    {
        var gates = _generator.Generate(3);

        Assert.All(gates, gate =>
        {
            Assert.False(gate.IsAssigned);
            Assert.False(gate.IsOccupied);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Generate_WithInvalidCount_ShouldThrow(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _generator.Generate(count));
    }

    [Fact]
    public void Generate_ShouldCreateIndependentGateInstances()
    {
        var first = _generator.Generate(3).ToArray();
        var second = _generator.Generate(3).ToArray();

        Assert.NotSame(first[0], second[0]);
    }
}
