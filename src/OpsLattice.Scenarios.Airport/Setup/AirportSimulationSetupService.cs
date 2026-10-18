
namespace OpsLattice.Scenarios.Airport.Setup;

public sealed class AirportSimulationSetupService
{
    private readonly TerminalGenerator _terminalGenerator;
    private readonly GateGenerator _gateGenerator;
    private readonly FlightScheduleGenerator _flightScheduleGenerator;

    public AirportSimulationSetupService(
        TerminalGenerator terminalGenerator,
        GateGenerator gateGenerator,
        FlightScheduleGenerator flightScheduleGenerator)
    {
        _terminalGenerator = terminalGenerator;
        _gateGenerator = gateGenerator;
        _flightScheduleGenerator = flightScheduleGenerator;
    }

    public AirportSimulationDefinition Build(
        AirportSetupConfiguration configuration)
    {
        AirportSetupConfigurationValidator.Validate(configuration);

        var terminals = _terminalGenerator.Generate(
            configuration.Terminals);

        var gates = _gateGenerator.Generate(
            configuration.Terminals);

        var flights = _flightScheduleGenerator.Generate(
            configuration);

        var initialTime = new DateTimeOffset(
            configuration.ScheduleDate.ToDateTime(
                TimeOnly.MinValue),
            TimeSpan.Zero);

        return new AirportSimulationDefinition(
            terminals,
            gates,
            flights,
            initialTime);
    }
}
