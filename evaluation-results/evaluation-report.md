# Agent evaluation

Generated: 2026-10-25 14:21:17+00:00
Ollama: qwen2.5:7b-instruct-q4_K_M at http://127.0.0.1:11434
Simulation: 100 flights, schedule date 2026-10-06, seed 42

A run passes when the MCP flight-tool arguments and results match the scenario. Answers are recorded and are not graded by a model.

## Aggregate

| Agent | Passed | Failed | LLM calls | MCP calls | Duration (ms) |
| --- | ---: | ---: | ---: | ---: | ---: |
| airport-operations-planned | 1 | 0 | 1 | 1 | 2970 |
| airport-operations | 0 | 1 | 2 | 1 | 2222 |

Overall: 1 passed, 1 failed, 2 runs.

## lax-to-iah-today

How many flights are flying from LAX to IAH today?

### airport-operations-planned: PASS

- LLM invocations: 1
- MCP invocations: 1
- Duration: 2970 ms

Answer:

> Flight count (flightType=Arrival, origin=LAX, destination=IAH, date=today): 8.

Tool calls:

- get_flight_count (13 ms) date=2026-10-06, destination=IAH, flightType=Arrival, origin=LAX
  - result: 8

Validation errors:

- none

Execution errors:

- none

Assertion failures:

- none

### airport-operations: FAIL

- LLM invocations: 2
- MCP invocations: 1
- Duration: 2222 ms

Answer:

> There are 8 flights flying from LAX to IAH today.

Tool calls:

- get_flight_count (7 ms) date=2026-10-06, destination=IAH, origin=LAX
  - result: 8

Validation errors:

- none

Execution errors:

- none

Assertion failures:

- Expected get_flight_count {date=2026-10-06, destination=IAH, flightType=Arrival, origin=LAX} result 8. No matching MCP call was found. Actual flight calls: get_flight_count {date=2026-10-06, destination=IAH, origin=LAX} result 8.
- Unexpected MCP flight call get_flight_count {date=2026-10-06, destination=IAH, origin=LAX} result 8.
