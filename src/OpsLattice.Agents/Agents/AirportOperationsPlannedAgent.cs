using System.Diagnostics;
using System.Text.Json;
using OpsLattice.Agents.Abstractions;
using OpsLattice.Agents.Models;
using OpsLattice.Agents.Planning;

namespace OpsLattice.Agents.Agents;

public sealed class AirportOperationsPlannedAgent(
    IAirportQueryPlanner planner,
    IAirportQueryValidator validator,
    IAirportQueryExecutor executor,
    IAirportAnswerFormatter formatter) : IAgent
{
    public string Name => "airport-operations-planned";

    public async Task<AgentExecutionResult> RunAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException(
                "Question cannot be empty.",
                nameof(question));

        var executionId = Guid.NewGuid().ToString("N");
        var totalStopwatch = Stopwatch.StartNew();

        var llmExecutions = new List<AgentLlmExecution>();
        var validationDiagnostics = new List<AgentToolExecution>();

        var plannerOutput = await BuildPlanWithTimingAsync(
            question,
            llmExecutions,
            cancellationToken);

        if (plannerOutput.Plan is null)
        {
            totalStopwatch.Stop();
            return new AgentExecutionResult(
                ExecutionId: executionId,
                Answer: plannerOutput.Error ?? "Unable to build a structured query plan.",
                TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                LlmExecutions: llmExecutions,
                ToolExecutions: validationDiagnostics);
        }

        var validation = await validator.ValidateAsync(
            question,
            plannerOutput.Plan,
            cancellationToken);

        if (validation.RequiresClarification)
        {
            totalStopwatch.Stop();
            return new AgentExecutionResult(
                ExecutionId: executionId,
                Answer: validation.Clarification ?? "Additional clarification is required.",
                TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                LlmExecutions: llmExecutions,
                ToolExecutions: validationDiagnostics);
        }

        if (!validation.IsValid || validation.Plan is null)
        {
            validationDiagnostics.Add(CreateValidationDiagnostic(
                attempt: 1,
                plannerOutput.Plan,
                validation.Errors));

            var repairPrompt = BuildRepairPrompt(
                question,
                plannerOutput.Plan,
                validation.Errors);

            var repairedPlannerOutput = await BuildPlanWithTimingAsync(
                repairPrompt,
                llmExecutions,
                cancellationToken);

            if (repairedPlannerOutput.Plan is null)
            {
                totalStopwatch.Stop();
                var failureMessage = string.Join(
                    Environment.NewLine,
                    validation.Errors);

                if (!string.IsNullOrWhiteSpace(repairedPlannerOutput.Error))
                {
                    failureMessage +=
                        $"{Environment.NewLine}Repair attempt failed: {repairedPlannerOutput.Error}";
                }

                return new AgentExecutionResult(
                    ExecutionId: executionId,
                    Answer: failureMessage,
                    TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                    LlmExecutions: llmExecutions,
                    ToolExecutions: validationDiagnostics);
            }

            var repairedValidation = await validator.ValidateAsync(
                question,
                repairedPlannerOutput.Plan,
                cancellationToken);

            if (repairedValidation.RequiresClarification)
            {
                totalStopwatch.Stop();
                return new AgentExecutionResult(
                    ExecutionId: executionId,
                    Answer: repairedValidation.Clarification ?? "Additional clarification is required.",
                    TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                    LlmExecutions: llmExecutions,
                    ToolExecutions: validationDiagnostics);
            }

            if (!repairedValidation.IsValid || repairedValidation.Plan is null)
            {
                validationDiagnostics.Add(CreateValidationDiagnostic(
                    attempt: 2,
                    repairedPlannerOutput.Plan,
                    repairedValidation.Errors));

                totalStopwatch.Stop();
                var failureMessage = string.Join(
                    Environment.NewLine,
                    repairedValidation.Errors);

                return new AgentExecutionResult(
                    ExecutionId: executionId,
                    Answer: $"Plan repair failed after one attempt.{Environment.NewLine}{failureMessage}",
                    TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
                    LlmExecutions: llmExecutions,
                    ToolExecutions: validationDiagnostics);
            }

            validation = repairedValidation;
        }

        var execution = await executor.ExecuteAsync(
            validation.Plan,
            cancellationToken);

        var answer = formatter.Format(
            question,
            execution);

        var toolExecutions = execution.Items
            .OrderBy(item => item.QueryIndex)
            .Select(item => new AgentToolExecution(
                ToolName: item.ToolName,
                Arguments: item.Arguments,
                Result: item.Result,
                DurationMs: item.DurationMs))
            .Concat(validationDiagnostics)
            .ToArray();

        totalStopwatch.Stop();

        return new AgentExecutionResult(
            ExecutionId: executionId,
            Answer: answer,
            TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
            LlmExecutions: llmExecutions,
            ToolExecutions: toolExecutions);
    }

    private async Task<AirportPlannerOutput> BuildPlanWithTimingAsync(
        string plannerInput,
        ICollection<AgentLlmExecution> llmExecutions,
        CancellationToken cancellationToken)
    {
        var plannerStopwatch = Stopwatch.StartNew();
        var plannerOutput = await planner.BuildPlanAsync(
            plannerInput,
            cancellationToken);
        plannerStopwatch.Stop();

        llmExecutions.Add(new AgentLlmExecution(
            Iteration: llmExecutions.Count + 1,
            DurationMs: plannerStopwatch.ElapsedMilliseconds,
            RequestedTools: false,
            RequestedToolNames: Array.Empty<string>()));

        return plannerOutput;
    }

    private static string BuildRepairPrompt(
        string originalQuestion,
        AirportQueryPlanDraft rejectedPlan,
        IReadOnlyList<string> validationErrors)
    {
        var rejectedPlanJson = JsonSerializer.Serialize(rejectedPlan);
        var errorText = string.Join(
            Environment.NewLine,
            validationErrors);

        return $"""
        REPAIR REQUEST
        Original question:
        {originalQuestion}

        Rejected plan JSON:
        {rejectedPlanJson}

        Validation errors:
        {errorText}

        Repair guidance:
        - Keep every filter the user asked for. Never drop a filter just to make the plan pass.
        - Interpret "scheduled on/for <date>" as date intent, not status=Scheduled.
        - Keep requested date/airport filters and split into multiple queries if needed.
        - Flights to an airport other than IAH are Departures; flights from an airport other than IAH are Arrivals.

        Return a corrected plan JSON that satisfies the validation errors.
        Use the same schema and keep all requested parts of the question.
        """;
    }

    private static AgentToolExecution CreateValidationDiagnostic(
        int attempt,
        AirportQueryPlanDraft rejectedPlan,
        IReadOnlyList<string> errors)
    {
        return new AgentToolExecution(
            ToolName: "plan_validation",
            Arguments: new Dictionary<string, object?>
            {
                ["attempt"] = attempt,
                ["plan"] = JsonSerializer.Serialize(rejectedPlan)
            },
            Result: string.Join(Environment.NewLine, errors),
            DurationMs: 0);
    }
}
