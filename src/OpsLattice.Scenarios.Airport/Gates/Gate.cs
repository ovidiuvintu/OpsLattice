using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Gates;

public sealed class Gate
{
    public Gate(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Gate code is required.",
                nameof(code));
        }

        Code = code;
    }

    public string Code { get; }

    public Flight? AssignedFlight { get; private set; }

    public bool IsAssigned => AssignedFlight is not null;

    public void Assign(Flight flight)
    {
        if (IsAssigned)
        {
            throw new InvalidOperationException(
                $"Gate {Code} is already assigned.");
        }

        AssignedFlight = flight;
    }
}