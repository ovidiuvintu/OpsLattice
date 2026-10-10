using OpsLattice.Scenarios.Airport.Flights;

namespace OpsLattice.Scenarios.Airport.Scheduling;

public interface IFlightScheduleLoader
{
    IReadOnlyCollection<Flight> Load(
        DateOnly scheduleDate);
}