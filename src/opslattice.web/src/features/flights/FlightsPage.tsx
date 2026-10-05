import { useEffect, useState } from "react";
import type { Flight } from "./Flight";
import { getFlights } from "./flightsApi";
import { advanceSimulation } from "../simulation/simulationApi";

export function FlightsPage() {
  const [flights, setFlights] = useState<Flight[]>([]);
  const [simulationTime, setSimulationTime] =
    useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isAdvancing, setIsAdvancing] = useState(false);

  async function loadFlights() {
    const data = await getFlights();
    setFlights(data);
  }

  useEffect(() => {
    async function load() {
      try {
        await loadFlights();
      } catch {
        setError("Unable to load flights.");
      }
    }

    load();
  }, []);

  async function handleAdvance() {
    try {
      setIsAdvancing(true);
      setError(null);

      const state = await advanceSimulation(10);

      setSimulationTime(state.currentTime);

      await loadFlights();
    } catch {
      setError("Unable to advance simulation.");
    } finally {
      setIsAdvancing(false);
    }
  }

  return (
    <main>
      <h1>Flights</h1>

      <button
        onClick={handleAdvance}
        disabled={isAdvancing}
      >
        {isAdvancing
          ? "Advancing..."
          : "Advance 10 Minutes"}
      </button>

      {simulationTime && (
        <p>
          Simulation Time:{" "}
          {new Date(simulationTime).toLocaleString()}
        </p>
      )}

      {error && <p>{error}</p>}

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
                {new Date(
                  flight.scheduledTime
                ).toLocaleString()}
              </td>
              <td>{flight.status}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </main>
  );
}