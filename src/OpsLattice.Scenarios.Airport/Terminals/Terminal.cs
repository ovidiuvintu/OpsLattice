namespace OpsLattice.Scenarios.Airport.Terminals;

public sealed class Terminal
{
    public Terminal(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Terminal code is required.",
                nameof(code));

        Code = code;
    }

    public string Code { get; }
}
