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
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06"),
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
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
    public async Task ValidateAsync_WhenCountFlightsIncludesRequestedStatus_ShouldPass()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: "Scheduled",
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals currently have status Scheduled at IAH on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal(AirportFlightStatus.Scheduled, result.Plan!.Queries[0].Status);
    }

    [Theory]
    [InlineData("How many flights have landed so far?")]
    [InlineData("How many flights are approaching right now?")]
    [InlineData("How many flights currently have status Scheduled?")]
    public async Task ValidateAsync_WhenStatusRequestedButPlanDropsIt_ShouldReturnInvalid(string question)
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: null,
                Status: null,
                Origin: null,
                Destination: null,
                Date: null)
        ]);

        var result = await _validator.ValidateAsync(question, draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("does not preserve the requested status filter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenScheduledForDateBecomesStatusScheduled_ShouldReturnInvalid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: "Scheduled",
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals are scheduled for October 6, 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("not status=Scheduled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenScheduledForDateBecomesStatusScheduledInMultiPartStatusQuestion_ShouldReturnInvalid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Arrival", "Scheduled", null, "IAH", "2026-10-06"),
            new AirportQueryDraft("CountFlights", null, "Approaching", null, null, null)
        ]);

        var result = await _validator.ValidateAsync(
            "How many arrivals are scheduled for October 6, 2026, and how many flights are approaching right now?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("not status=Scheduled", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("How many departures to DEN on October 6 2026?")]
    [InlineData("How many departures to DEN on October 6th, 2026?")]
    [InlineData("How many departures to DEN on October 6, 2026?")]
    public async Task ValidateAsync_WhenFullDateWrittenWithoutCommaOrWithOrdinal_ShouldNotAskForYear(string question)
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "IAH",
                Destination: "DEN",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(question, draft);

        Assert.False(result.RequiresClarification);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenFullDateWithoutCommaNotPreserved_ShouldReturnInvalid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "IAH",
                Destination: "DEN",
                Date: "2026-10-07")
        ]);

        var result = await _validator.ValidateAsync(
            "How many departures to DEN on October 6 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("explicit date", StringComparison.OrdinalIgnoreCase));
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
    public async Task ValidateAsync_WhenModelLabelsFlightsFromLaxToIahAsDeparture_ShouldCorrectToArrival()
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

        Assert.True(result.IsValid);
        Assert.Equal(AirportFlightType.Arrival, result.Plan!.Queries[0].FlightType);
    }

    [Fact]
    public async Task ValidateAsync_WhenUserExplicitlyAsksForDeparturesFromLaxToIah_ShouldFailDirectionalValidation()
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
            "How many departures from LAX to IAH on October 6, 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("departure flights require origin=IAH", StringComparison.OrdinalIgnoreCase)
            || e.Contains("departure flights cannot use destination=IAH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenModelLabelsFlightsToDenAsArrivals_ShouldCorrectToDepartures()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Arrival", null, null, "DEN", null),
            new AirportQueryDraft("ListFlights", "Arrival", null, null, "DEN", null)
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights to DEN? If you find any flights to DEN, can you list the details?",
            draft);

        Assert.True(result.IsValid);
        Assert.All(result.Plan!.Queries, query =>
        {
            Assert.Equal(AirportFlightType.Departure, query.FlightType);
            Assert.Equal("DEN", query.Destination);
        });
        Assert.Equal(AirportQueryOperation.CountFlights, result.Plan.Queries[0].Operation);
        Assert.Equal(AirportQueryOperation.ListFlights, result.Plan.Queries[1].Operation);
    }

    [Fact]
    public async Task ValidateAsync_WhenArrivalQueryLeaksOtherPartsDestination_ShouldAnchorItToIah()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Arrival", null, "LAX", "ORD", "2026-10-06"),
            new AirportQueryDraft("CountFlights", "Departure", null, "IAH", "ORD", "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are arriving from LAX and departing to ORD on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal("LAX", result.Plan!.Queries[0].Origin);
        Assert.Equal("IAH", result.Plan.Queries[0].Destination);
        Assert.Equal("IAH", result.Plan.Queries[1].Origin);
        Assert.Equal("ORD", result.Plan.Queries[1].Destination);
    }

    [Fact]
    public async Task ValidateAsync_WhenRouteBetweenTwoOtherAirportsWithoutDirectionWords_ShouldLeaveItAlone()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", null, null, "LAX", "ORD", null)
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights go from LAX to ORD?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal("LAX", result.Plan!.Queries[0].Origin);
        Assert.Equal("ORD", result.Plan.Queries[0].Destination);
        Assert.Null(result.Plan.Queries[0].FlightType);
    }

    [Fact]
    public async Task ValidateAsync_WhenFlightTypeMissingButRouteImpliesDirection_ShouldFillItIn()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", null, null, "PHX", null, null)
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights from PHX?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal(AirportFlightType.Arrival, result.Plan!.Queries[0].FlightType);
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
    public async Task ValidateAsync_WhenScheduledAtAirportOnDateBecomesStatusScheduled_ShouldReturnInvalid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: null,
                Status: "Scheduled",
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are scheduled at IAH on October 6, 2026?",
            draft);

        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        Assert.Contains(result.Errors, e =>
            e.Contains("not status=Scheduled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenScheduledAtAirportOnDateOmitsStatus_ShouldPassWithDateOnly()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: null,
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are scheduled at IAH on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
        Assert.Null(result.Plan!.Queries[0].Status);
        Assert.Equal(new DateOnly(2026, 10, 6), result.Plan.Queries[0].Date);
        Assert.Equal("IAH", result.Plan.Queries[0].Destination);
    }

    [Fact]
    public async Task ValidateAsync_WhenDepartureDirectionIsPreserved_ShouldPass()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "IAH",
                Destination: "ORD",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many departures from IAH to ORD on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal(AirportFlightType.Departure, result.Plan!.Queries[0].FlightType);
        Assert.Equal("IAH", result.Plan.Queries[0].Origin);
        Assert.Equal("ORD", result.Plan.Queries[0].Destination);
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

    [Fact]
    public async Task ValidateAsync_WhenDepartingOtherAirportAndArrivingAtPrimary_ShouldCountOneArrival()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "LAX",
                Destination: "IAH",
                Date: "today")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are departing LAX and arriving at IAH today?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal(AirportFlightType.Arrival, result.Plan!.Queries[0].FlightType);
        Assert.Equal("LAX", result.Plan.Queries[0].Origin);
        Assert.Equal("IAH", result.Plan.Queries[0].Destination);
        Assert.Equal("today", result.Plan.Queries[0].DateKeyword);
    }

    [Fact]
    public async Task ValidateAsync_WhenPlanDropsNamedOrigin_ShouldStayInvalid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: "today")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are departing LAX and arriving at IAH today?",
            draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.Contains("origin LAX", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_WhenArrivalUsesPrimaryAsBothEnds_ShouldRefuseWithoutAPlan()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: "IAH",
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are arriving at IAH from IAH on October 6, 2026?",
            draft);

        Assert.True(result.RequiresClarification);
        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        Assert.Contains("contradict", result.Clarification, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WhenWidenedPlanDropsContradictoryOrigin_ShouldStillRefuse()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are arriving at IAH from IAH on October 6, 2026?",
            draft);

        Assert.True(result.RequiresClarification);
        Assert.Null(result.Plan);
    }

    [Fact]
    public async Task ValidateAsync_WhenDeparturesFromPrimaryAndArrivalsAtPrimary_ShouldRemainValid()
    {
        var draft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Departure",
                Status: null,
                Origin: "IAH",
                Destination: null,
                Date: "2026-10-06"),
            new AirportQueryDraft(
                Operation: "CountFlights",
                FlightType: "Arrival",
                Status: null,
                Origin: null,
                Destination: "IAH",
                Date: "2026-10-06")
        ]);

        var result = await _validator.ValidateAsync(
            "How many flights are departing from IAH and arriving at IAH on October 6, 2026?",
            draft);

        Assert.True(result.IsValid);
        Assert.Equal(AirportFlightType.Departure, result.Plan!.Queries[0].FlightType);
        Assert.Equal(AirportFlightType.Arrival, result.Plan.Queries[1].FlightType);
    }
}
