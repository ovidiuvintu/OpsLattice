using ClosedXML.Excel;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Scheduling;

namespace OpsLattice.Api.Infrastructure.Scheduling;

public sealed class ExcelFlightScheduleLoader : IFlightScheduleLoader
{
    private readonly string _filePath;

    public ExcelFlightScheduleLoader(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyCollection<Flight> Load(
        DateOnly scheduleDate)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException(
                "Flight schedule file was not found.",
                _filePath);
        }

        using var workbook = new XLWorkbook(_filePath);

        var worksheet = workbook.Worksheet(
            "Daily Flight Schedule");

        var flights = new List<Flight>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var scheduledTime = row
                .Cell(6)
                .GetDateTime();

            var flightDate = DateOnly.FromDateTime(
                scheduledTime);

            if (flightDate != scheduleDate)
            {
                continue;
            }

            var typeText = row.Cell(3).GetString().Trim();

            if (!Enum.TryParse<FlightType>(
                    typeText,
                    ignoreCase: true,
                    out var flightType))
            {
                throw new InvalidDataException(
                    $"Invalid flight type '{typeText}' at row {row.RowNumber()}.");
            }

            var flight = new Flight
            {
                FlightNumber = row.Cell(1).GetString().Trim(),
                Airline = row.Cell(2).GetString().Trim(),
                Type = flightType,
                Origin = row.Cell(4).GetString().Trim(),
                Destination = row.Cell(5).GetString().Trim(),

                ScheduledTime = new DateTimeOffset(
                    scheduledTime,
                    TimeSpan.Zero)
            };

            flights.Add(flight);
        }

        return flights;
    }
}