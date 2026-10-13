namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class InMemoryGateProvider : IGateProvider
{
    private readonly IReadOnlyCollection<Gate> _gates;

    public InMemoryGateProvider()
        : this(
        [
            new Gate("B10"),
            new Gate("B11"),
            new Gate("B12")
        ])
    {
    }

    public InMemoryGateProvider(IEnumerable<Gate> gates)
    {
        ArgumentNullException.ThrowIfNull(gates);

        _gates = Array.AsReadOnly(gates.ToArray());
    }

    public IReadOnlyCollection<Gate> GetGates()
    {
        return _gates;
    }
}