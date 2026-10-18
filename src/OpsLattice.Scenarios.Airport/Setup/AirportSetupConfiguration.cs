using OpsLattice.Scenarios.Airport.Setup;

public sealed record AirportSetupConfiguration(
    IReadOnlyCollection<TerminalSetupConfiguration> Terminals,
    int FlightCount,
    DateOnly ScheduleDate,
    int? RandomSeed = null)
{
    public int GateCount => Terminals.Sum(t => t.GateCount);
}
