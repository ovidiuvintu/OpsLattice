namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class InMemoryGateProvider : IGateProvider
{
    private readonly List<Gate> _gates =
    [
        new Gate("B10"),
        new Gate("B11"),
        new Gate("B12")
    ];

    public IReadOnlyCollection<Gate> GetGates()
    {
        return _gates.AsReadOnly();
    }
}
