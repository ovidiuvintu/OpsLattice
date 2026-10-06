# OpsLattice Airport Scenario

## Overview

The Airport scenario is the first concrete scenario implemented on the
OpsLattice simulation platform.

It demonstrates how domain-specific operational rules can be layered on
top of the generic simulation engine without introducing airport
knowledge into `OpsLattice.Simulator`.

## Flight Lifecycle

The current flight lifecycle is:

``` text
Scheduled
    |
    | BeginApproach
    v
Approaching
    |
    | Land
    v
Landed
    |
    | ArriveAtGate
    v
AtGate
    |
    | Depart
    v
Departed
```

The `Flight` entity records important lifecycle timestamps:

-   `ApproachStartedAt`
-   `LandedAt`
-   `ArrivedAtGateAt`

## Current Sample Flights

The in-memory scenario contains:

  Flight   Airline             Route         Scheduled
  -------- ------------------- ------------- ----------------------
  UA123    United Airlines     ORD -\> IAH   2026-10-04 14:30 UTC
  DL456    Delta Air Lines     ATL -\> IAH   2026-10-04 15:15 UTC
  AA789    American Airlines   DFW -\> IAH   2026-10-04 16:00 UTC

## Gates

The in-memory gate provider currently contains:

``` text
B10
B11
B12
```

A gate distinguishes between two concepts:

### Assigned flight

The flight planned to use the gate.

### Occupying flight

The flight physically at the gate.

This distinction allows a gate to be assigned before the aircraft
arrives.

A gate is released after the occupying flight departs.

## Timing Rules

The current simplified airport rules are:

  Event                         Rule
  ----------------------------- -----------------------------
  Gate assignment eligibility   Scheduled time - 60 minutes
  Begin approach                Scheduled time
  Landing                       Approach start + 10 minutes
  Arrive at gate                Landing + 10 minutes
  Departure                     Gate arrival + 30 minutes

These values are simulation rules for the current scenario. They are not
intended to model every real airport procedure.

## Example Timeline

For a flight scheduled at 15:15:

``` text
14:15  Gate assignment becomes eligible

15:15  Scheduled -> Approaching
       ApproachStartedAt = 15:15

15:25  Approaching -> Landed
       LandedAt = 15:25

15:35  Landed -> AtGate
       ArrivedAtGateAt = 15:35
       Assigned gate becomes occupied

16:05  AtGate -> Departed
       Gate is released
```

The event-aware simulator preserves these timestamps even when the
caller advances the simulation by a large interval in a single
operation.

## Gate Assignment

`GateAssignmentProcessor` owns the 60-minute assignment horizon.

The current simplified behavior:

-   only scheduled flights are considered
-   a flight becomes eligible when it enters the assignment horizon
-   a flight already assigned to a gate is not assigned again
-   the first available unassigned gate is selected
-   if no gate is available, no assignment occurs

## Flight Arrival

`FlightArrivalProcessor` owns arrival state transitions.

Current rules:

-   a scheduled flight begins approach at its scheduled time
-   an approaching flight lands 10 minutes after approach begins

## Gate Occupancy

`GateOccupancyProcessor` owns the taxi-to-gate timing rule.

A landed flight reaches its assigned gate 10 minutes after landing.

At that time:

-   the gate becomes occupied by the flight
-   the flight transitions to `AtGate`
-   `ArrivedAtGateAt` is recorded

## Departure and Gate Release

`GateDepartureProcessor` owns the turnaround rule.

Thirty minutes after arrival at the gate:

-   the flight transitions to `Departed`
-   the gate releases the flight
-   the gate becomes available for future assignment

## API Representation

The flight API currently exposes information including:

-   flight number
-   airline
-   origin
-   destination
-   scheduled time
-   status
-   approach start time
-   landing time
-   gate arrival time
-   assigned gate

After departure and release, the current gate field becomes empty
because the flight is no longer assigned to a gate.

## Current Limitations

The Airport scenario is deliberately small.

It does not yet model concepts such as:

-   runways
-   taxiways
-   gate compatibility
-   aircraft types
-   departure schedules independent of turnaround duration
-   delays
-   cancellations
-   gate conflicts beyond basic availability
-   weather
-   crews
-   passengers
-   baggage
-   maintenance

These should be added as vertical slices only when they provide useful
operational behavior.
