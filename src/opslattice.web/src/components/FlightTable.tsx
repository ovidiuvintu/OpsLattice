import type { Flight } from "../types/Flight";

interface FlightTableProps {
  flights: Flight[];
}



const cellStyle: React.CSSProperties = {
  padding: "6px 10px",
  borderBottom: "1px solid #e5e5e5",
};

export function FlightTable({
  flights,
}: FlightTableProps) {
  return (
    <section className="flights-section">
      <h2>Flights</h2>

      <table
          style={{
            width: "100%",
            borderCollapse: "collapse",
            fontSize: 13,
            textAlign: "left",
      }}
    >        <thead>
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
              <td style={cellStyle}>{flight.flightNumber}</td>

              <td style={cellStyle}>{flight.airline}</td>

              <td style={cellStyle}>
                {flight.origin} → {flight.destination}
              </td>

              <td style={cellStyle}>
                {formatTime(flight.scheduledTime)}
              </td>

              <td style={cellStyle}>{flight.status}</td>

              <td style={cellStyle}>{flight.gate ?? "—"}</td>
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
