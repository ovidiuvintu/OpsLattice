using OpsLattice.Agents.Planning;
using Xunit;

namespace OppsLattice.Agents.Tests.Planning;

public sealed class AirportQueryValidatorTests
{
    private readonly AirportQueryValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenMonthDayWithoutYear_ShouldRequireClarification()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: null)
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals on October 6?",
            draft);

        Assert.False(result.IsValid);
        Assert.True(result.RequiresClarification);
        Assert.Equal("Which year do you mean for that date?", result.Clarification);
    }

    [Fact]
    public async Task ValidateAsync_WhenExplicitDateNotPreserved_ShouldReturnInvalid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-07")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals to IAH on October 6, 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.False(result.RequiresClarification);
        Assert.Contains(result.Errors, e =>
            e.Contains("explicit date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenArrivalsAndDeparturesQueryIsSplit_ShouldPass()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: "Scheduled",
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06"),
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: "Scheduled",
                Origin: "IAH",
                Destination: null,
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals and departures are scheduled at IAH on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Plan);
        Assert.Equal(2, result.Plan!.Queries.Count);
    }

    [Fact]
    public async Task ValidateAsync_WhenArrivalFromLaxToIah_ShouldPass()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: "LAX",
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals from LAX to IAH on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDepartureFromIahToLax_ShouldPass()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "IAH",
                Destination: "LAX",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many departures from IAH to LAX on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDepartureFromLaxToIah_ShouldFailDirectionalValidation()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "LAX",
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights from LAX to IAH on October 6, 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("departure flights require origin=IAH", StringComparison.OrdinalIgnoreCase)
            || e.Contains("departure flights cannot use destination=IAH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenArrivalFromIahToLax_ShouldFailDirectionalValidation()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: "IAH",
                Destination: "LAX",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals from IAH to LAX on October 6, 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("arrival flights require destination=IAH", StringComparison.OrdinalIgnoreCase)
            || e.Contains("arrival flights cannot use origin=IAH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenOptionalOriginDestinationAreOmitted_ShouldRemainValid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: null,
                Destination: null,
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals are scheduled on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
    }
}
