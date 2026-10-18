using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Scenarios.Airport.Setup;

public sealed class GateGenerator
{
    public IReadOnlyCollection<Gate> Generate(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Gate count must be greater than zero.");
        }

        return Enumerable.Range(10, count)
            .Select(number => new Gate($"B{number}"))
            .ToArray();
    }

    public IReadOnlyCollection<Gate> Generate(
        IEnumerable<TerminalSetupConfiguration> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var items = configurations.ToArray();

        // Validate the terminal configuration before generating gates.
        _ = new TerminalGenerator().Generate(items);

        return items
            .SelectMany(terminal =>
                Enumerable.Range(10, terminal.GateCount)
                    .Select(number =>
                        new Gate(
                            $"{terminal.Code}{number}",
                            terminal.Code)))
            .ToArray();
    }
}
