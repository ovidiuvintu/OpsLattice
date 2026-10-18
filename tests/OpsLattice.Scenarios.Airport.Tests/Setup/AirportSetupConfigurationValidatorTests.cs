
using OpsLattice.Scenarios.Airport.Setup;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Setup;

public sealed class AirportSetupConfigurationValidatorTests
{
    [Fact]
    public void Validate_WithValidConfiguration_ShouldSucceed()
    {
        var configuration = CreateConfiguration();

        AirportSetupConfigurationValidator.Validate(configuration);
    }

    [Fact]
    public void Configuration_ShouldCalculateTotalGateCount()
    {
        var configuration = CreateConfiguration();

        Assert.Equal(22, configuration.GateCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidTerminalGateCount_ShouldThrow(
        int gateCount)
    {
        var configuration = CreateConfiguration(
            terminals:
            [
                new TerminalSetupConfiguration("A", gateCount)
            ]);

        Assert.Throws<ArgumentException>(() =>
            AirportSetupConfigurationValidator.Validate(configuration));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidFlightCount_ShouldThrow(
        int flightCount)
    {
        var configuration = CreateConfiguration(
            flightCount: flightCount);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AirportSetupConfigurationValidator.Validate(configuration));
    }

    [Fact]
    public void Validate_WithNoTerminals_ShouldThrow()
    {
        var configuration = CreateConfiguration(
            terminals: []);

        Assert.Throws<ArgumentException>(() =>
            AirportSetupConfigurationValidator.Validate(configuration));
    }

    [Fact]
    public void Validate_WithNullTerminals_ShouldThrow()
    {
        var configuration = new AirportSetupConfiguration(
            Terminals: null!,
            FlightCount: 250,
            ScheduleDate: new DateOnly(2026, 11, 2));

        Assert.Throws<ArgumentException>(() =>
            AirportSetupConfigurationValidator.Validate(configuration));
    }

    [Fact]
    public void Validate_WithDuplicateTerminalCodes_ShouldThrow()
    {
        var configuration = CreateConfiguration(
            terminals:
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("a", 12)
            ]);

        Assert.Throws<ArgumentException>(() =>
            AirportSetupConfigurationValidator.Validate(configuration));
    }

    [Fact]
    public void Validate_WithEmptyTerminalCode_ShouldThrow()
    {
        var configuration = CreateConfiguration(
            terminals:
            [
                new TerminalSetupConfiguration("", 10)
            ]);

        Assert.Throws<ArgumentException>(() =>
            AirportSetupConfigurationValidator.Validate(configuration));
    }

    [Fact]
    public void Validate_WithNullConfiguration_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AirportSetupConfigurationValidator.Validate(null!));
    }

    private static AirportSetupConfiguration CreateConfiguration(
        IReadOnlyCollection<TerminalSetupConfiguration>? terminals = null,
        int flightCount = 250)
    {
        return new AirportSetupConfiguration(
            Terminals: terminals ??
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("B", 12)
            ],
            FlightCount: flightCount,
            ScheduleDate: new DateOnly(2026, 11, 2),
            RandomSeed: 42);
    }


}
