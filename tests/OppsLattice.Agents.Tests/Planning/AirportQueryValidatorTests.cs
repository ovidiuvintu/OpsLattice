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
}
