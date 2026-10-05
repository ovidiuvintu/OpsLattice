namespace OpsLattice.Scenarios.Airport.Flights;

public sealed class Flight
{
    public required string FlightNumber { get; init; }

    public required string Airline { get; init; }

    public required string Origin { get; init; }

    public required string Destination { get; init; }

    public DateTimeOffset ScheduledTime { get; init; }

    public FlightStatus Status { get; private set; } = FlightStatus.Scheduled;

    public DateTimeOffset? ApproachStartedAt { get; private set; }

    public void BeginApproach(DateTimeOffset startedAt)
    {
        if (Status != FlightStatus.Scheduled)
        {
            throw new InvalidOperationException(
                "Only a scheduled flight can begin its approach.");
        }

        ApproachStartedAt = startedAt;
        Status = FlightStatus.Approaching;
    }

    public void Land()
    {
        if (Status != FlightStatus.Approaching)
        {
            throw new InvalidOperationException(
                "A flight must be approaching before it can land.");
        }

        Status = FlightStatus.Landed;
    }
}