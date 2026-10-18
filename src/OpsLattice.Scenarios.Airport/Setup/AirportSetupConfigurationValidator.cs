
namespace OpsLattice.Scenarios.Airport.Setup;

public static class AirportSetupConfigurationValidator
{
    public static void Validate(AirportSetupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Terminals is null)
        {
            throw new ArgumentException(
                "At least one terminal is required.",
                nameof(configuration));
        }

        // Validates:
        // - At least one terminal exists
        // - Terminal codes are not empty
        // - Terminal codes are unique (case-insensitive)
        // - Each terminal has a positive gate count
        _ = new TerminalGenerator().Generate(configuration.Terminals);

        if (configuration.FlightCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration.FlightCount),
                "Flight count must be greater than zero.");
        }
    }
}
