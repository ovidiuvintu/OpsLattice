using OpsLattice.Simulator.Simulation;
using OpsLattice.Simulator.Time;

namespace OpsLattice.Simulator.Tests.Simulation;

public sealed class SimulationRunnerTests
{
    [Fact]
    public void Advance_ShouldAdvanceSimulationClock()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 20, 0, TimeSpan.Zero);

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
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 20, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var step = new TestSimulationStep();

        var runner = new SimulationRunner(
            clock,
            [step]);

        runner.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(1, step.ExecutionCount);
    }

    [Fact]
    public void Advance_ShouldExecuteScheduledStepAtItsEventTime()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 50, 0, TimeSpan.Zero);

        var eventTime = new DateTimeOffset(
            2026, 10, 4, 15, 15, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        var step = new TestScheduledSimulationStep(
            clock,
            eventTime);

        var runner = new SimulationRunner(
            clock,
            [step]);

        runner.Advance(TimeSpan.FromMinutes(30));

        Assert.Contains(
            eventTime,
            step.ExecutionTimes);

        Assert.Equal(
            startTime.AddMinutes(30),
            clock.CurrentTime);
    }

    private sealed class TestSimulationStep : ISimulationStep
    {
        public int ExecutionCount { get; private set; }

        public void Execute()
        {
            ExecutionCount++;
        }
    }

    private sealed class TestScheduledSimulationStep
        : IScheduledSimulationStep
    {
        private readonly ISimulationClock _clock;
        private readonly DateTimeOffset _eventTime;
        private bool _eventProcessed;

        public TestScheduledSimulationStep(
            ISimulationClock clock,
            DateTimeOffset eventTime)
        {
            _clock = clock;
            _eventTime = eventTime;
        }

        public List<DateTimeOffset> ExecutionTimes { get; } = [];

        public DateTimeOffset? GetNextExecutionTime(
            DateTimeOffset currentTime,
            DateTimeOffset targetTime)
        {
            if (_eventProcessed)
            {
                return null;
            }

            if (_eventTime > currentTime &&
                _eventTime <= targetTime)
            {
                return _eventTime;
            }

            return null;
        }

        public void Execute()
        {
            ExecutionTimes.Add(_clock.CurrentTime);

            if (_clock.CurrentTime == _eventTime)
            {
                _eventProcessed = true;
            }
        }
    }
}