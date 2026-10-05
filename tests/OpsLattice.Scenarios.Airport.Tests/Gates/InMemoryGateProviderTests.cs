using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Scenarios.Airport.Tests.Gates;

public sealed class InMemoryGateProviderTests
{
    [Fact]
    public void GetGates_ShouldReturnGates()
    {
        var provider = new InMemoryGateProvider();

        var gates = provider.GetGates();

        Assert.NotEmpty(gates);
    }

    [Fact]
    public void GetGates_ShouldReturnExpectedGates()
    {
        var provider = new InMemoryGateProvider();

        var gates = provider.GetGates();

        Assert.Contains(gates, gate => gate.Code == "B10");
        Assert.Contains(gates, gate => gate.Code == "B11");
        Assert.Contains(gates, gate => gate.Code == "B12");
    }

    [Fact]
    public void GetGates_ShouldReturnUnassignedGates()
    {
        var provider = new InMemoryGateProvider();

        var gates = provider.GetGates();

        Assert.All(
            gates,
            gate => Assert.False(gate.IsAssigned));
    }
}