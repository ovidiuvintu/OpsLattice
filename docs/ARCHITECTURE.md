# OpsLattice Architecture

## Purpose

OpsLattice is a generic real-time operations and simulation platform.
Its architecture is intentionally domain-neutral: the platform provides
reusable simulation capabilities, while individual scenarios provide
domain-specific state, rules, and behavior.

The first implemented scenario is airport operations.

A core architectural principle is:

> OpsLattice knows nothing about airports. The Airport scenario knows
> about OpsLattice.

This keeps the simulation infrastructure reusable for future scenarios
without embedding airport concepts into the platform.

## Solution Structure

``` text
OpsLattice.slnx

src/
    OpsLattice.Core
    OpsLattice.Simulator
    OpsLattice.Scenarios.Airport
    OpsLattice.Api
    OpsLattice.Web

tests/
    OpsLattice.Core.Tests
    OpsLattice.Simulator.Tests
    OpsLattice.Scenarios.Airport.Tests
    OpsLattice.Architecture.Tests
```

## Project Responsibilities

### OpsLattice.Core

Contains generic domain concepts that are independent of infrastructure
and individual scenarios.

The project is intentionally small. Generic abstractions should be
introduced only when concrete vertical slices demonstrate that they are
needed.

### OpsLattice.Simulator

Contains the generic simulation engine.

Current responsibilities include:

-   simulation clock
-   advancing simulation time
-   simulation steps
-   scheduled simulation steps
-   event-aware time progression

The simulator has no dependency on airport concepts.

### OpsLattice.Scenarios.Airport

Contains airport-specific behavior, including:

-   flights
-   flight lifecycle
-   flight arrival processing
-   gates
-   gate assignment
-   gate occupancy
-   gate departure and release
-   airport-specific timing rules

This project depends on the generic simulation abstractions.

### OpsLattice.Api

ASP.NET Core API that exposes simulation state and commands.

Current endpoints include flight retrieval and simulation time
advancement.

### OpsLattice.Web

React + TypeScript + Vite frontend for viewing and interacting with the
simulation.

## Dependency Direction

The intended dependency direction is toward generic abstractions:

``` text
OpsLattice.Scenarios.Airport
             |
             v
      OpsLattice.Simulator
             |
             v
        OpsLattice.Core
```

Direct project references should exist only when types from another
project are actually required. A dependency chain should not be created
merely for symmetry.

The generic projects must never reference
`OpsLattice.Scenarios.Airport`.

## Architectural Principles

### Scenario isolation

Scenario-specific concepts stay inside the scenario.

For example, the generic simulator does not know about:

-   flights
-   gates
-   taxi time
-   approach duration
-   turnaround time

### Explicit boundaries

Projects communicate through deliberate public abstractions rather than
by exposing internal implementation details.

### Business rules have clear owners

Timing rules belong to the airport processors that implement the
corresponding behavior.

Examples:

``` text
Flight arrival processor     -> approach and landing rules
Gate assignment processor    -> assignment horizon
Gate occupancy processor     -> taxi duration
Gate departure processor     -> turnaround duration
```

Simulation steps coordinate collections of entities and expose upcoming
event times to the generic simulator.

### Extract abstractions when proven

OpsLattice avoids creating generic frameworks in anticipation of future
requirements.

A reusable abstraction should be extracted when a real vertical slice
demonstrates the need for it.

## Current Technology

-   .NET 10
-   C#
-   ASP.NET Core controllers
-   React
-   TypeScript
-   Vite
-   xUnit
-   PowerShell/.NET CLI development workflow

## Current State

The airport scenario currently supports a deterministic flight lifecycle
with event-aware time progression.

The simulation can advance across multiple domain events in a single
operation while preserving the correct event timestamps.

The current architecture provides a foundation for additional
operational scenarios without coupling the simulation engine to the
airport domain.
