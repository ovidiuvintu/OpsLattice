import { useEffect, useState } from "react";
import type { Flight } from "./Flight";
import { getFlights } from "./flightsApi";

export function FlightsPage() {
  const [flights, setFlights] = useState<Flight[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadFlights() {
      try {
        const result = await getFlights();
        setFlights(result);
      } catch {
        setError("Unable to load flights.");
      }
    }

    loadFlights();
  }, []);

  if (error) {
    return <p>{error}</p>;
  }

  return (
    <main>
      <h1>Flights</h1>

      <table>
        <thead>
          <tr>
            <th>Flight</th>
            <th>Airline</th>
            <th>Origin</th>
            <th>Destination</th>
            <th>Scheduled</th>
            <th>Status</th>
          </tr>
        </thead>

        <tbody>
          {flights.map((flight) => (
            <tr key={flight.flightNumber}>
              <td>{flight.flightNumber}</td>
              <td>{flight.airline}</td>
              <td>{flight.origin}</td>
              <td>{flight.destination}</td>
              <td>
                {new Date(flight.scheduledTime).toLocaleString()}
              </td>
              <td>{flight.status}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}