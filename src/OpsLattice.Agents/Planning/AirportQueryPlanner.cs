using System.Text.Json;
using OpsLattice.Agents.Abstractions;

namespace OpsLattice.Agents.Planning;

public sealed class AirportQueryPlanner(
    ILlmClient llmClient) : IAirportQueryPlanner
{
    private const string _plannerPrompt = """
        You are a query planner for an airport simulation assistant.

        Convert the user question into a strict JSON object with this shape:
        {
          "queries": [
            {
              "operation": "CountFlights|ListFlights|FlightSummary|GetSimulationTime",
              "flightType": "Arrival" | "Departure" | null,
              "status": "Scheduled" | "Approaching" | "Landed" | "AtGate" | "Boarding" | "ReadyForDeparture" | "Departed" | "Delayed" | "Cancelled" | null,
              "origin": "IATA code or null",
              "destination": "IATA code or null",
              "date": "yyyy-MM-dd|today|tomorrow|yesterday|null"
            }
          ]
        }

        Rules:
        - Return JSON only. No markdown.
        - Include every requested sub-query in the question.
        - Do not invent MCP tool names.
        - If a value is unknown, set it to null.
        - "scheduled on/for/at <date>" and "scheduled at <airport> on <date>"
          refer to the scheduled date, not operational status=Scheduled.
          Leave status null for those questions.
        - Set status only when the user explicitly asks for current
          operational state (for example, "currently have status Scheduled",
          "how many have landed", "which flights are approaching").
        - status works with both CountFlights and ListFlights.
        - Use CountFlights for "how many" questions.
        - Use ListFlights when the user asks to list, show or name flights.
        - For enum fields (operation, flightType, status), output ONE value only.
          Never output pipe-separated option strings like "Arrival|Departure".
        - For explicit dates, preserve the exact date in yyyy-MM-dd.
        - For relative dates, use only today, tomorrow, or yesterday.
        - Direction rules for this simulation:
          - A flight from LAX to IAH is an Arrival.
          - A flight from IAH to LAX is a Departure.
          - For arrivals at the primary airport, prefer destination=IAH.
          - For departures from the primary airport, prefer origin=IAH.
          - "Flights to DEN" (any airport other than IAH) are Departures
            with destination=DEN. Never use flightType=Arrival with a
            destination other than IAH.
          - "Flights from LAX" (any airport other than IAH) are Arrivals
            with origin=LAX.
          - "Departing LAX and arriving at IAH" is one Arrival with
            origin=LAX and destination=IAH, not a Departure.
          - Never use the same airport as both origin and destination.
            An arrival from IAH to IAH is contradictory. Do not drop
            an airport to force a count.
        - If the user asks how many AND asks to list or show details, return
          both a CountFlights query and a ListFlights query with the same
          filters, even when the listing is conditional ("if there are any").

        Example for a multi-part request:
        {
          "queries": [
            {
              "operation": "CountFlights",
              "flightType": "Arrival",
              "status": null,
              "origin": null,
              "destination": "IAH",
              "date": "2026-10-06"
            },
            {
              "operation": "CountFlights",
              "flightType": "Departure",
              "status": null,
              "origin": "IAH",
              "destination": null,
              "date": "2026-10-06"
            }
          ]
        }
        """;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AirportPlannerOutput> BuildPlanAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new AirportPlannerOutput(
                Plan: null,
                Error: "Question cannot be empty.",
                RawJson: string.Empty);
        }

        var prompt = string.Join(
            Environment.NewLine,
            _plannerPrompt,
            string.Empty,
            "USER QUESTION:",
            question);

        var raw = await llmClient.GenerateAsync(
            prompt,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AirportPlannerOutput(
                Plan: null,
                Error: "Planner returned an empty response.",
                RawJson: string.Empty);
        }

        try
        {
            var draft = JsonSerializer.Deserialize<AirportQueryPlanDraft>(
                raw,
                _jsonOptions);

            if (draft?.Queries is null || draft.Queries.Count == 0)
            {
                return new AirportPlannerOutput(
                    Plan: null,
                    Error: "Planner returned no queries.",
                    RawJson: raw);
            }

            return new AirportPlannerOutput(
                Plan: draft,
                Error: null,
                RawJson: raw);
        }
        catch (JsonException ex)
        {
            return new AirportPlannerOutput(
                Plan: null,
                Error: $"Planner returned malformed JSON: {ex.Message}",
                RawJson: raw);
        }
    }
}
