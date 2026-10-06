# OpsLattice Development Guide

## Development Approach

OpsLattice is being developed architecture-first but incrementally.

The preferred workflow is:

``` text
small vertical requirement
        |
        v
write a focused test
        |
        v
observe the failing behavior
        |
        v
implement the smallest useful change
        |
        v
run focused tests
        |
        v
run full solution tests
        |
        v
commit on a feature branch
```

## Test-First Example

The event-aware simulation feature was developed by first demonstrating
the defect.

A simulation advanced from 14:50 to 15:20 while DL456 had an event at
15:15.

The initial test showed:

``` text
Expected event execution: 15:15
Actual event execution:   15:20
```

The generic simulator was then changed to discover and visit
intermediate event times.

Further tests proved dynamically created events:

``` text
15:15  approach
15:25  landing
15:35  gate occupancy
16:05  departure and gate release
```

## Test Organization

``` text
tests/
    OpsLattice.Core.Tests
    OpsLattice.Simulator.Tests
    OpsLattice.Scenarios.Airport.Tests
    OpsLattice.Architecture.Tests
```

At the latest validated point:

``` text
45 tests passed
0 failed
```

That count was recorded before the subsequent gate-assignment scheduling
test was added. The Airport scenario test suite later reached 40 passing
tests.

`OpsLattice.Core.Tests` and `OpsLattice.Architecture.Tests` currently
contain no discovered tests, so the full test command reports warnings
for those projects.

## Useful Commands

Run all tests:

``` powershell
dotnet test
```

Run simulator tests:

``` powershell
dotnet test tests\OpsLattice.Simulator.Tests
```

Run Airport scenario tests:

``` powershell
dotnet test tests\OpsLattice.Scenarios.Airport.Tests
```

Run the API:

``` powershell
dotnet run --project src\OpsLattice.Api
```

The API currently runs locally at:

``` text
http://localhost:5160
```

Advance the simulation by 10 minutes:

``` powershell
Invoke-RestMethod `
    -Uri "http://localhost:5160/api/simulation/advance" `
    -Method Post `
    -ContentType "application/json" `
    -Body '{"minutes":10}'
```

## Branch Naming

Current conventions:

``` text
chore/...      setup and tooling
feat/...       functionality
fix/...        defect fixes
refactor/...   structural improvements
```

Examples used during development include:

``` text
chore/solution-bootstrap
feat/view-flights
feat/flight-arrival
feat/gate-assignment
feat/gate-occupancy
feat/gate-release
feat/simulation-time-progression
feat/gate-assignment-scheduling
```

## Commit Style

Commits use concise conventional-style messages.

Examples:

``` text
feat: add event-aware simulation time progression
feat: add event-aware gate assignment scheduling
```

## Build Configuration

Shared build settings are defined in `Directory.Build.props`:

``` xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Test projects additionally identify themselves as test projects and are
not packable.

## Design Discipline

When extending OpsLattice:

1.  Keep generic simulation code free of scenario terminology.
2.  Put business rules in the scenario that owns them.
3.  Prefer explicit dependencies and boundaries.
4.  Do not create shared abstractions merely because multiple future
    scenarios might need them.
5.  Add abstractions when actual implementation demonstrates common
    behavior.
6.  Preserve deterministic behavior before introducing AI-driven
    behavior.
7.  Add AI or agent capabilities later as consumers of facts and tools
    rather than as owners of deterministic business rules.

## Near-Term Direction

The current deterministic simulation foundation is sufficient to support
additional operational behavior.

Future work should generally favor new vertical slices over additional
simulator infrastructure unless a concrete scenario exposes a simulator
limitation.

Potential future areas include richer airport operations, additional
scenarios, architecture enforcement tests, and eventually tool/AI
integration over deterministic operational state.
