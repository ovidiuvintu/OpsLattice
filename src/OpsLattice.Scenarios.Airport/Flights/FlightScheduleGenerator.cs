
using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Setup;

public sealed class FlightScheduleGenerator
{
    private const string _airportCode = "IAH";

    private static readonly string[] _airlines =
    [
        "United",
        "Delta",
        "American",
        "Southwest"
    ];

    private static readonly string[] _airlineCodes =
    [
        "UA",
        "DL",
        "AA",
        "WN"
    ];

    private static readonly string[] _airports =
    [
        "ORD",
        "ATL",
        "DFW",
        "DEN",
        "LAX",
        "JFK",
        "MIA",
        "PHX"
    ];

    public IReadOnlyCollection<Flight> Generate(
        AirportSetupConfiguration configuration)
    {
        AirportSetupConfigurationValidator.Validate(configuration);

        var random = configuration.RandomSeed.HasValue
            ? new Random(configuration.RandomSeed.Value)
            : new Random();

        var scheduleStart = new DateTimeOffset(
            configuration.ScheduleDate.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var flights = new List<Flight>(configuration.FlightCount);

        for (var i = 0; i < configuration.FlightCount; i++)
        {
            var airlineIndex = random.Next(_airlines.Length);
            var airport = _airports[random.Next(_airports.Length)];

            var type = i % 2 == 0
                ? FlightType.Arrival
                : FlightType.Departure;

            var scheduledTime = scheduleStart.AddMinutes(
                GenerateScheduledMinute(random));

            flights.Add(new Flight
            {
                FlightNumber = $"{_airlineCodes[airlineIndex]}{1000 + i}",
                Airline = _airlines[airlineIndex],
                Origin = type == FlightType.Arrival
                    ? airport
                    : _airportCode,
                Destination = type == FlightType.Arrival
                    ? _airportCode
                    : airport,
                ScheduledTime = scheduledTime,
                Type = type
            });
        }

        return flights
            .OrderBy(f => f.ScheduledTime)
            .ThenBy(f => f.FlightNumber)
            .ToArray();
    }


    private static int GenerateScheduledMinute(Random random)
    {
        var probability = random.Next(100);

        return probability switch
        {
            // Midnight - 4:59 AM (3%)
            < 3 => random.Next(0, 5 * 60),

            // 5:00 AM - 9:59 AM (25%)
            < 28 => random.Next(5 * 60, 10 * 60),

            // 10:00 AM - 3:59 PM (35%)
            < 63 => random.Next(10 * 60, 16 * 60),

            // 4:00 PM - 9:59 PM (32%)
            < 95 => random.Next(16 * 60, 22 * 60),

            // 10:00 PM - 11:59 PM (5%)
            _ => random.Next(22 * 60, 24 * 60)
        };
    }

}

