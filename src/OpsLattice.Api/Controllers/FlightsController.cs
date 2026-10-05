using Microsoft.AspNetCore.Mvc;
using OpsLattice.Api.Models;
using OpsLattice.Scenarios.Airport.Flights;
using OpsLattice.Scenarios.Airport.Gates;

namespace OpsLattice.Api.Controllers;

[ApiController]
[Route("api/flights")]
public sealed class FlightsController : ControllerBase
{
    private readonly IFlightProvider _flightProvider;
    private readonly IGateProvider _gateProvider;

    public FlightsController(
        IFlightProvider flightProvider,
        IGateProvider gateProvider)
    {
        _flightProvider = flightProvider;
        _gateProvider = gateProvider;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<FlightResponse>> GetFlights()
    {
        var gates = _gateProvider.GetGates();

        var flights = _flightProvider
            .GetFlights()
            .Select(flight =>
            {
                var gate = gates.FirstOrDefault(
                    gate => gate.AssignedFlight == flight);

                return new FlightResponse(
                    flight.FlightNumber,
                    flight.Airline,
                    flight.Origin,
                    flight.Destination,
                    flight.ScheduledTime,
                    flight.Status.ToString(),
                    flight.ApproachStartedAt,
                    gate?.Code);
            })
            .ToArray();

        return Ok(flights);
    }
}