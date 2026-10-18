
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Setup;
using Xunit;

namespace OpsLattice.Scenarios.Airport.Tests.Setup;

public sealed class FlightScheduleGeneratorTests
{
    private readonly FlightScheduleGenerator _generator = new();

    private static AirportSetupConfiguration CreateConfiguration(
        int flightCount = 250,
        int? randomSeed = 42)
    {
        return new AirportSetupConfiguration(
            Terminals:
            [
                new TerminalSetupConfiguration("A", 10),
                new TerminalSetupConfiguration("B", 12)
            ],
            FlightCount: flightCount,
            ScheduleDate: new DateOnly(2026, 11, 2),
            RandomSeed: randomSeed);
    }

    [Fact]
    public void Generate_ShouldCreateRequestedNumberOfFlights()
    {
        var flights = _generator.Generate(CreateConfiguration());

        Assert.Equal(250, flights.Count);
    }

    [Fact]
    public void Generate_ShouldCreateUniqueFlightNumbers()
    {
        var flights = _generator.Generate(CreateConfiguration());

        Assert.Equal(
            flights.Count,
            flights.Select(f => f.FlightNumber)
                .Distinct()
                .Count());
    }

    [Fact]
    public void Generate_ShouldCreateArrivalsAndDepartures()
    {
        var flights = _generator.Generate(CreateConfiguration());

        Assert.Equal(
            125,
            flights.Count(f => f.Type == FlightType.Arrival));

        Assert.Equal(
            125,
            flights.Count(f => f.Type == FlightType.Departure));
    }

    [Fact]
    public void Generate_ShouldUseConfiguredScheduleDate()
    {
        var flights = _generator.Generate(CreateConfiguration());

        Assert.All(flights, flight =>
        {
            Assert.Equal(
                new DateOnly(2026, 11, 2),
                DateOnly.FromDateTime(
                    flight.ScheduledTime.UtcDateTime));
        });
    }

    [Fact]
    public void Generate_ShouldInitializeFlightsAsScheduled()
    {
        var flights = _generator.Generate(CreateConfiguration());

        Assert.All(flights, flight =>
        {
            Assert.Equal(FlightStatus.Scheduled, flight.Status);
            Assert.Null(flight.ApproachStartedAt);
            Assert.Null(flight.LandedAt);
            Assert.Null(flight.ArrivedAtGateAt);
        });
    }

    [Fact]
    public void Generate_WithSameSeed_ShouldProduceSameSchedule()
    {
        var configuration = CreateConfiguration(randomSeed: 42);

        var first = _generator.Generate(configuration).ToArray();
        var second = _generator.Generate(configuration).ToArray();

        Assert.Equal(
            first.Select(f => (
                f.FlightNumber,
                f.Airline,
                f.Origin,
                f.Destination,
                f.ScheduledTime,
                f.Type)),
            second.Select(f => (
                f.FlightNumber,
                f.Airline,
                f.Origin,
                f.Destination,
                f.ScheduledTime,
                f.Type)));
    }

    [Fact]
    public void Generate_ShouldSortFlightsChronologically()
    {
        var flights = _generator.Generate(CreateConfiguration()).ToArray();

        Assert.Equal(
            flights.OrderBy(f => f.ScheduledTime)
                .Select(f => f.ScheduledTime),
            flights.Select(f => f.ScheduledTime));
    }

    [Fact]
    public void Generate_ShouldUseIAHForArrivalsAndDepartures()
    {
        var flights = _generator.Generate(CreateConfiguration());

        Assert.All(flights, flight =>
        {
            if (flight.Type == FlightType.Arrival)
            {
                Assert.Equal("IAH", flight.Destination);
            }
            else
            {
                Assert.Equal("IAH", flight.Origin);
            }
        });
    }

    [Fact]
    public void Generate_ShouldConcentrateFlightsDuringDaytime()
    {
        var configuration = CreateConfiguration(
            flightCount: 10_000,
            randomSeed: 42);

        var flights = _generator.Generate(configuration);

        var daytimeFlights = flights.Count(f =>
            f.ScheduledTime.Hour >= 5 &&
            f.ScheduledTime.Hour < 23);

        var daytimePercentage =
            (double)daytimeFlights / flights.Count;

        Assert.InRange(daytimePercentage, 0.90, 0.99);
    }
}
