namespace OpsLattice.Scenarios.Airport.Gates;

public interface IGateProvider
{
    IReadOnlyCollection<Gate> GetGates();
}