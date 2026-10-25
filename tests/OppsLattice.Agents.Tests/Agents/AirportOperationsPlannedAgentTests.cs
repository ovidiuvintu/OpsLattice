using Moq;
using OpsLattice.Agents.Agents;
using OpsLattice.Agents.Planning;
using Xunit;

namespace OppsLattice.Agents.Tests.Agents;

public sealed class AirportOperationsPlannedAgentTests
{
    [Fact]
    public async Task RunAsync_WhenInitialPlanIsInvalidAndRepairSucceeds_ShouldExecuteAfterRepair()
    {
        var planner = new Mock<IAirportQueryPlanner>();
        var validator = new Mock<IAirportQueryValidator>();
        var executor = new Mock<IAirportQueryExecutor>();
        var formatter = new Mock<IAirportAnswerFormatter>();

        var invalidDraft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Departure", null, "LAX", "IAH", "today")
        ]);

        var repairedDraft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Arrival", null, "LAX", "IAH", "today")
        ]);

        var repairedPlan = new AirportQueryPlan(
        [
            new AirportQuery(
                AirportQueryOperation.CountFlights,
                AirportFlightType.Arrival,
                null,
                "LAX",
                "IAH",
                null,
                "today")
        ]);

        planner.SetupSequence(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AirportPlannerOutput(invalidDraft, null, "{invalid}"))
            .ReturnsAsync(new AirportPlannerOutput(repairedDraft, null, "{repaired}"));

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<AirportQueryPlanDraft>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AirportQueryValidationResult.Invalid(
                ["Query 1: departure flights require origin=IAH when origin is specified; received origin=LAX."]))
            .ReturnsAsync(AirportQueryValidationResult.Success(repairedPlan));

        var executionResult = new AirportQueryExecutionResult(
        [
            new AirportQueryExecutionItem(
                QueryIndex: 0,
                Query: repairedPlan.Queries[0],
                IsSuccess: true,
                ToolName: "get_flight_count",
                Arguments: new Dictionary<string, object?>
                {
                    ["flightType"] = "Arrival",
                    ["origin"] = "LAX",
                    ["destination"] = "IAH",
                    ["date"] = "2026-10-06"
                },
                Result: "12",
                DurationMs: 9)
        ]);

        executor.Setup(x => x.ExecuteAsync(
                repairedPlan,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);

        formatter.Setup(x => x.Format(
                It.IsAny<string>(),
                executionResult))
            .Returns("Flight count (flightType=Arrival, origin=LAX, destination=IAH, date=today): 12.");

        var agent = new AirportOperationsPlannedAgent(
            planner.Object,
            validator.Object,
            executor.Object,
            formatter.Object);

        var result = await agent.RunAsync("How many flights from LAX to IAH today?");

        Assert.Equal("Flight count (flightType=Arrival, origin=LAX, destination=IAH, date=today): 12.", result.Answer);
        Assert.Equal(2, result.LlmExecutions.Count);
        Assert.Contains(result.ToolExecutions, e => e.ToolName == "plan_validation");
        Assert.Contains(result.ToolExecutions, e => e.ToolName == "get_flight_count");

        executor.Verify(x => x.ExecuteAsync(
                repairedPlan,
                It.IsAny<CancellationToken>()),
            Times.Once);

        planner.Verify(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_WhenPlanAndRepairBothFail_ShouldReturnFailureWithoutMcpExecution()
    {
        var planner = new Mock<IAirportQueryPlanner>();
        var validator = new Mock<IAirportQueryValidator>();
        var executor = new Mock<IAirportQueryExecutor>();
        var formatter = new Mock<IAirportAnswerFormatter>();

        var invalidDraft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Departure", null, "LAX", "IAH", "today")
        ]);

        planner.SetupSequence(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AirportPlannerOutput(invalidDraft, null, "{invalid}"))
            .ReturnsAsync(new AirportPlannerOutput(invalidDraft, null, "{still-invalid}"));

        validator.SetupSequence(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<AirportQueryPlanDraft>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AirportQueryValidationResult.Invalid(
                ["Query 1: departure flights require origin=IAH when origin is specified; received origin=LAX."]))
            .ReturnsAsync(AirportQueryValidationResult.Invalid(
                ["Query 1: departure flights require origin=IAH when origin is specified; received origin=LAX."]));

        var agent = new AirportOperationsPlannedAgent(
            planner.Object,
            validator.Object,
            executor.Object,
            formatter.Object);

        var result = await agent.RunAsync("How many flights from LAX to IAH today?");

        Assert.Contains("Plan repair failed after one attempt.", result.Answer);
        Assert.Equal(2, result.LlmExecutions.Count);
        Assert.Equal(2, result.ToolExecutions.Count(e => e.ToolName == "plan_validation"));
        Assert.DoesNotContain(result.ToolExecutions, e => e.ToolName == "get_flight_count");

        executor.Verify(x => x.ExecuteAsync(
                It.IsAny<AirportQueryPlan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        planner.Verify(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_WhenRepairPlannerReturnsNoPlan_ShouldReturnFailureWithoutMcpExecution()
    {
        var planner = new Mock<IAirportQueryPlanner>();
        var validator = new Mock<IAirportQueryValidator>();
        var executor = new Mock<IAirportQueryExecutor>();
        var formatter = new Mock<IAirportAnswerFormatter>();

        var invalidDraft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Departure", null, "LAX", "IAH", "today")
        ]);

        planner.SetupSequence(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AirportPlannerOutput(invalidDraft, null, "{invalid}"))
            .ReturnsAsync(new AirportPlannerOutput(null, "malformed", "not-json"));

        validator.Setup(x => x.ValidateAsync(
                It.IsAny<string>(),
                invalidDraft,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AirportQueryValidationResult.Invalid(
                ["Query 1: departure flights require origin=IAH when origin is specified; received origin=LAX."]));

        var agent = new AirportOperationsPlannedAgent(
            planner.Object,
            validator.Object,
            executor.Object,
            formatter.Object);

        var result = await agent.RunAsync("How many flights from LAX to IAH today?");

        Assert.Contains("Repair attempt failed", result.Answer);
        Assert.Equal(2, result.LlmExecutions.Count);
        Assert.Single(result.ToolExecutions, e => e.ToolName == "plan_validation");

        executor.Verify(x => x.ExecuteAsync(
                It.IsAny<AirportQueryPlan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        planner.Verify(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_WhenValidationFailsForCountFlightsStatus_ShouldIncludeRepairGuidanceInSecondPlannerPrompt()
    {
        var planner = new Mock<IAirportQueryPlanner>();
        var validator = new Mock<IAirportQueryValidator>();
        var executor = new Mock<IAirportQueryExecutor>();
        var formatter = new Mock<IAirportAnswerFormatter>();

        var invalidDraft = new AirportQueryPlanDraft(
        [
            new AirportQueryDraft("CountFlights", "Arrival", "Scheduled", null, "IAH", "2026-10-06")
        ]);

        planner.SetupSequence(x => x.BuildPlanAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AirportPlannerOutput(invalidDraft, null, "{invalid}"))
            .ReturnsAsync(new AirportPlannerOutput(null, "malformed", "not-json"));

        validator.Setup(x => x.ValidateAsync(
                It.IsAny<string>(),
                It.IsAny<AirportQueryPlanDraft>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AirportQueryValidationResult.Invalid(
                ["Query 1: CountFlights does not support status filtering with current MCP tools."]));

        var agent = new AirportOperationsPlannedAgent(
            planner.Object,
            validator.Object,
            executor.Object,
            formatter.Object);

        var _ = await agent.RunAsync("How many flights are scheduled to arrive at IAH on October 6, 2026?");

        planner.Verify(x => x.BuildPlanAsync(
                It.Is<string>(prompt =>
                    prompt.Contains("Never drop a filter just to make the plan pass.", StringComparison.Ordinal) &&
                    prompt.Contains("scheduled on/for <date>", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
