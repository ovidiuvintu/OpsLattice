# OpsLattice Simulation Engine

## Overview

`OpsLattice.Simulator` provides a lightweight, deterministic,
event-aware simulation engine.

The original implementation advanced the clock directly to the requested
target time and then executed all simulation steps once.

That approach was simple, but it caused events inside a large time
interval to occur late.

For example:

``` text
Current time: 14:50
Flight event: 15:15
Advance by:   30 minutes
Target time:  15:20
```

The original behavior executed the flight event at 15:20 instead of
15:15.

The simulator now advances through meaningful event times.

## Simulation Clock

`ISimulationClock` exposes the current simulation time:

``` csharp
public interface ISimulationClock
{
    DateTimeOffset CurrentTime { get; }
}
```

`SimulationClock` owns mutation of that time and requires advances to be
positive.

## Simulation Steps

A normal simulation step implements:

``` csharp
public interface ISimulationStep
{
    void Execute();
}
```

A scheduled step additionally implements:

``` csharp
public interface IScheduledSimulationStep : ISimulationStep
{
    DateTimeOffset? GetNextExecutionTime(
        DateTimeOffset currentTime,
        DateTimeOffset targetTime);
}
```

The interface is intentionally domain-neutral.

A scheduled step tells the simulator when its next meaningful event
occurs. It does not tell the simulator what the event means.

## Event-Aware Progression

For an advance from 14:50 to 15:20 with an event at 15:15:

``` text
14:50
  |
  | discover next event
  v
15:15
  |
  | execute all simulation steps
  | state may create new events
  v
discover next event again
  |
  v
15:20
  |
  | execute all simulation steps
  v
advance complete
```

The runner repeatedly:

1.  Calculates the target time.
2.  Asks scheduled steps for their next event within the current window.
3.  Chooses the earliest event.
4.  Advances the clock to that event.
5.  Executes all configured steps in order.
6.  Queries scheduled steps again because state changes may have created
    new events.
7.  Continues until the requested target time is reached.

If no scheduled event exists before the target, the runner advances
directly to the target and executes the steps once.

## Dynamically Created Events

A key property of the design is that all future events do not need to be
known when `Advance` begins.

For example:

``` text
15:15  Flight begins approach
          |
          | state changes to Approaching
          v
15:25  Landing event becomes discoverable
```

After executing the 15:15 event, the runner asks the scheduled steps for
the next event again. The Airport scenario can then report the newly
relevant 15:25 landing event.

## Step Ordering

All simulation steps execute at each meaningful simulation time.

The Airport API currently configures them in this order:

``` text
1. Gate assignment
2. Flight arrival
3. Gate occupancy
4. Gate departure
```

Explicit ordering allows state produced by one step to be observed by
later steps at the same simulation time.

## Separation of Responsibilities

``` text
SimulationRunner
    |
    | owns HOW time advances
    v
IScheduledSimulationStep
    |
    | reports WHEN scenario work is meaningful
    v
Scenario processor
    |
    | owns domain timing and behavior
    v
Domain entity
```

The simulator therefore knows how to move through events without knowing
what a flight, gate, arrival, or departure is.

## Current Scope

The implementation is intentionally lightweight.

It does not currently use:

-   a global event queue
-   a priority queue
-   persisted event streams
-   distributed simulation workers
-   wall-clock synchronization

The current scan-based approach is appropriate for the size of the
scenario and keeps the architecture understandable. More sophisticated
scheduling should be introduced only when a concrete requirement
justifies it.
