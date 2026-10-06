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

    public Flight? OccupyingFlight { get; private set; }

    public bool IsOccupied => OccupyingFlight is not null;

    public void Assign(Flight flight)
    {
        if (IsAssigned)
        {
            throw new InvalidOperationException(
                $"Gate {Code} is already assigned.");
        }

        AssignedFlight = flight;
    }

    public void Occupy(Flight flight)
    {
        if (AssignedFlight != flight)
        {
            throw new InvalidOperationException(
                $"Flight {flight.FlightNumber} is not assigned to gate {Code}.");
        }

        if (IsOccupied)
        {
            throw new InvalidOperationException(
                $"Gate {Code} is already occupied.");
        }

        OccupyingFlight = flight;
    }

    public void Release(Flight flight)
    {
        if (AssignedFlight != flight ||
            OccupyingFlight != flight)
        {
            throw new InvalidOperationException(
                $"Flight {flight.FlightNumber} does not occupy gate {Code}.");
        }

        OccupyingFlight = null;
        AssignedFlight = null;
    }
}