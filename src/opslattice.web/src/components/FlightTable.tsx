import type { Flight } from "../types/Flight";

interface FlightTableProps {
  flights: Flight[];
}

export function FlightTable({
  flights,
}: FlightTableProps) {
  return (
    <section className="flights-section">
      <h2>Flights</h2>

      <table>
        <thead>
          <tr>
            <th>Flight</th>
            <th>Airline</th>
            <th>Route</th>
            <th>Scheduled</th>
            <th>Status</th>
            <th>Gate</th>
          </tr>
        </thead>

        <tbody>
          {flights.map((flight) => (
            <tr key={flight.flightNumber}>
              <td>{flight.flightNumber}</td>

              <td>{flight.airline}</td>

              <td>
                {flight.origin} → {flight.destination}
              </td>

              <td>
                {formatTime(flight.scheduledTime)}
              </td>

              <td>{flight.status}</td>

              <td>{flight.gate ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function formatTime(value: string): string {
  return new Date(value).toLocaleTimeString(
    "en-US",
    {
      timeZone: "UTC",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    }
  );
}