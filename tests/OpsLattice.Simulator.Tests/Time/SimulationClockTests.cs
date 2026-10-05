using OpsLattice.Simulator.Time;

namespace OpsLattice.Simulator.Tests.Time;

public sealed class SimulationClockTests
{
    [Fact]
    public void CurrentTime_ShouldInitiallyBeStartTime()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 0, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        Assert.Equal(startTime, clock.CurrentTime);
    }

    [Fact]
    public void Advance_ShouldMoveCurrentTimeForward()
    {
        var startTime = new DateTimeOffset(
            2026, 10, 4, 14, 0, 0, TimeSpan.Zero);

        var clock = new SimulationClock(startTime);

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(
            startTime.AddMinutes(10),
            clock.CurrentTime);
    }

    [Fact]
    public void Advance_WithNonPositiveDuration_ShouldThrow()
    {
        var clock = new SimulationClock(
            DateTimeOffset.UtcNow);

        var action = () =>
            clock.Advance(TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }
}