using OpsLattice.Scenarios.Airport.Setup;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Setup;

public sealed class TerminalGateGeneratorTests
{
    [Fact]
    public void Generate_ShouldAssociateGatesWithTerminals()
    {
        var generator = new GateGenerator();

        var gates = generator.Generate(
        [
            new TerminalSetupConfiguration("A", 2),
            new TerminalSetupConfiguration("B", 3)
        ]);

        Assert.Equal(5, gates.Count);

        Assert.Equal(
            ["A10", "A11", "B10", "B11", "B12"],
            gates.Select(g => g.Code));

        Assert.Equal(
            ["A", "A", "B", "B", "B"],
            gates.Select(g => g.TerminalCode));
    }

    [Fact]
    public void Generate_ShouldCreateAvailableGates()
    {
        var generator = new GateGenerator();

        var gates = generator.Generate(
        [
            new TerminalSetupConfiguration("A", 2)
        ]);

        Assert.All(gates, gate =>
        {
            Assert.False(gate.IsAssigned);
            Assert.False(gate.IsOccupied);
        });
    }
}
