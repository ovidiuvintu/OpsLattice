using OpsLattice.Scenarios.Airport.Terminals;

namespace OpsLattice.Scenarios.Airport.Setup;

public sealed class TerminalGenerator
{
    public IReadOnlyCollection<Terminal> Generate(
        IEnumerable<TerminalSetupConfiguration> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);

        var items = configurations.ToArray();

        if (items.Length == 0)
        {
            throw new ArgumentException(
                "At least one terminal is required.",
                nameof(configurations));
        }

        if (items.Any(x =>
            string.IsNullOrWhiteSpace(x.Code) ||
            x.GateCount <= 0))
        {
            throw new ArgumentException(
                "Each terminal must have a code and a positive gate count.",
                nameof(configurations));
        }

        if (items.Select(x => x.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != items.Length)
        {
            throw new ArgumentException(
                "Terminal codes must be unique.",
                nameof(configurations));
        }

        return items
            .Select(x => new Terminal(x.Code))
            .ToArray();
    }
}
