
import { useEffect, useState } from "react";
import AirportAssistant from "./components/AirportAssistant";

interface Flight {
  flightNumber: string;
  airline: string;
  origin: string;
  destination: string;
  scheduledTime: string;
  status: string;
}

const API_BASE_URL = "http://localhost:5160";

async function fetchFlightsData(signal?: AbortSignal): Promise<Flight[]> {
  const response = await fetch(`${API_BASE_URL}/api/flights`, { signal });

  if (!response.ok) {
    throw new Error(`Failed to load flights: HTTP ${response.status}`);
  }

  const data: Flight[] = await response.json();
  return data;
}

export default function App() {
  const [flights, setFlights] = useState<Flight[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    (async () => {
      try {
        const data = await fetchFlightsData(controller.signal);
        if (!controller.signal.aborted) {
          setFlights(data);
          setError(null);
        }
      } catch (err) {
        if (controller.signal.aborted) return;
        setError(err instanceof Error ? err.message : "Failed to load flights");
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();

    return () => controller.abort();
  }, []);

  async function refreshFlights() {
      setLoading(true);
      setError(null);

      try {
        const data = await fetchFlightsData();
        setFlights(data);
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Failed to load flights"
        );
      } finally {
        setLoading(false);
      }
    }

  return (
    <main
      style={{
        maxWidth: 1200,
        margin: "0 auto",
        padding: 24,
        fontFamily: "Arial, sans-serif",
      }}
    >
      <header style={{ marginBottom: 32 }}>
        <h1>OpsLattice</h1>
        <p>Airport Operations Simulation</p>
      </header>


      <AirportAssistant />

      <section style={{ marginBottom: 32 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 12,
          }}
        >
          <h2>Flights</h2>

          <button
            type="button"
            onClick={refreshFlights}
            disabled={loading}
          >
            {loading ? "Loading..." : "Refresh Flights"}
          </button>
        </div>

        {loading && <p>Loading flights...</p>}

        {error && (
          <p role="alert" style={{ color: "crimson" }}>
            {error}
          </p>
        )}

        {!loading && !error && (
          flights.length > 0 ? (
            <table
              style={{
                width: "100%",
                borderCollapse: "collapse",
                textAlign: "left",
              }}
            >
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
          ) : (
            <p>No flights available.</p>
          )
        )}
      </section>
    </main>
  );
}


