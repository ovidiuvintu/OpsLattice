using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Simulator.Tests.Simulation;

public sealed class SimulationRunnerTests
{
    [Fact]
    public void Advance_ShouldAdvanceSimulationClock()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 0, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var runner = new SimulationRunner(
            clock,
            []);

        runner.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(
            startTime.AddMinutes(10),
            clock.CurrentTime);
    }

    [Fact]
    public void Advance_ShouldExecuteSimulationSteps()
    {
        var clock = new SimulationClock(
            new DateTimeOffset(
                2026, 10, 4, 14, 0, 0, TimeSpan.Zero));

        var step = new TestSimulationStep();

        var runner = new SimulationRunner(
            clock,
            [step]);

        runner.Advance(TimeSpan.FromMinutes(1));

        Assert.True(step.WasExecuted);
    }

    private sealed class TestSimulationStep : ISimulationStep
    {
        public bool WasExecuted { get; private set; }

        public void Execute()
        {
            WasExecuted = true;
        }
    }
}
