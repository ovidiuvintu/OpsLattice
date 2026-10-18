using OpsLattice.Scenarios.Airport.Setup;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Setup;

public sealed class TerminalGeneratorTests
{
    private readonly TerminalGenerator _generator = new();

    [Fact]
    public void Generate_ShouldCreateTerminals()
    {
        var terminals = _generator.Generate(
        [
            new TerminalSetupConfiguration("A", 3),
            new TerminalSetupConfiguration("B", 2)
        ]);

        Assert.Equal(2, terminals.Count);
        Assert.Equal(
            ["A", "B"],
            terminals.Select(t => t.Code));
    }

    [Fact]
    public void Generate_WithDuplicateCodes_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.Generate(
            [
                new TerminalSetupConfiguration("A", 3),
                new TerminalSetupConfiguration("a", 2)
            ]));
    }

    [Fact]
    public void Generate_WithInvalidGateCount_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.Generate(
            [
                new TerminalSetupConfiguration("A", 0)
            ]));
    }

    [Fact]
    public void Generate_WithNoTerminals_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.Generate(
                Array.Empty<TerminalSetupConfiguration>()));
    }
}
