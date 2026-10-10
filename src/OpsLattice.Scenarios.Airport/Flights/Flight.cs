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

    public DateTimeOffset? LandedAt { get; private set; }

    public DateTimeOffset? ArrivedAtGateAt { get; private set; }

    public required FlightType Type { get; init; }

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

    public void Land(DateTimeOffset landedAt)
    {
        if (Status != FlightStatus.Approaching)
        {
            throw new InvalidOperationException(
                "A flight must be approaching before it can land.");
        }

        LandedAt = landedAt;
        Status = FlightStatus.Landed;
    }
    public void ArriveAtGate(DateTimeOffset arrivedAt)
    {
        if (Status != FlightStatus.Landed)
        {
            throw new InvalidOperationException(
                "A flight must be landed before it can arrive at a gate.");
        }

        ArrivedAtGateAt = arrivedAt;
        Status = FlightStatus.AtGate;
    }

    public void Depart()
    {
        var canDepart = Type switch
        {
            FlightType.Arrival =>
                Status == FlightStatus.AtGate,

            FlightType.Departure =>
                Status == FlightStatus.ReadyForDeparture,

            _ => false
        };

        if (!canDepart)
        {
            throw new InvalidOperationException(
                "Flight is not ready to depart.");
        }

        Status = FlightStatus.Departed;
    }

    public void BeginBoarding()
    {
        if (Type != FlightType.Departure)
        {
            throw new InvalidOperationException(
                "Only departure flights can begin boarding.");
        }

        if (Status != FlightStatus.Scheduled)
        {
            throw new InvalidOperationException(
                "Only scheduled flights can begin boarding.");
        }

        Status = FlightStatus.Boarding;
    }

    public void CompleteBoarding()
    {
        if (Type != FlightType.Departure)
        {
            throw new InvalidOperationException(
                "Only departure flights can complete boarding.");
        }

        if (Status != FlightStatus.Boarding)
        {
            throw new InvalidOperationException(
                "A flight must be boarding before boarding can complete.");
        }

        Status = FlightStatus.ReadyForDeparture;
    }
}