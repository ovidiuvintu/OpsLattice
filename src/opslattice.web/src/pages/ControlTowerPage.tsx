
import { useCallback, useEffect, useState } from "react";

type Flight = {
  flightNumber: string;
  airline: string;
  type: string;
  origin: string;
  destination: string;
  scheduledTime: string;
  status: string;
  gate: string | null;
};

type Gate = {
  code: string;
  status: string;
  assignedFlightNumber: string | null;
  occupyingFlightNumber: string | null;
};

type Simulation = {
  currentTime: string;
};

type View = "air" | "ground";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export default function ControlTowerPage() {
  const [view, setView] = useState<View>("air");
  const [flights, setFlights] = useState<Flight[]>([]);
  const [gates, setGates] = useState<Gate[]>([]);
  const [simulation, setSimulation] = useState<Simulation | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const [simulationResponse, flightsResponse, gatesResponse] =
        await Promise.all([
          fetch(`${API_BASE_URL}/api/simulation`),
          fetch(`${API_BASE_URL}/api/flights`),
          fetch(`${API_BASE_URL}/api/gates`),
        ]);

      if (
        !simulationResponse.ok ||
        !flightsResponse.ok ||
        !gatesResponse.ok
      ) {
        throw new Error("Failed to retrieve airport simulation data.");
      }

      const [simulationData, flightsData, gatesData] = await Promise.all([
        simulationResponse.json() as Promise<Simulation>,
        flightsResponse.json() as Promise<Flight[]>,
        gatesResponse.json() as Promise<Gate[]>,
      ]);

      setSimulation(simulationData);
      setFlights(flightsData);
      setGates(gatesData);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unexpected error."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const availableGates = gates.filter(
    (gate) => gate.status === "Available"
  ).length;

  const approachingFlights = flights.filter(
    (flight) => flight.status === "Approaching"
  ).length;

  const landedFlights = flights.filter(
    (flight) => flight.status === "Landed"
  ).length;

  return (
    <main style={{ padding: 24, fontFamily: "Arial, sans-serif" }}>
      <header style={{ marginBottom: 24 }}>
        <h1>OpsLattice Control Tower</h1>
        <p>Airport Operations</p>

        <button onClick={() => void refresh()} disabled={loading}>
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </header>

      {error && (
        <p role="alert" style={{ color: "crimson" }}>
          {error}
        </p>
      )}

      <section
        style={{
          display: "flex",
          gap: 16,
          flexWrap: "wrap",
          marginBottom: 24,
        }}
      >
        <Metric label="Total Flights" value={flights.length} />
        <Metric label="Approaching" value={approachingFlights} />
        <Metric label="Landed" value={landedFlights} />
        <Metric label="Available Gates" value={availableGates} />
        <Metric
          label="Simulation Time"
          value={
            simulation
              ? new Date(simulation.currentTime).toLocaleString()
              : "—"
          }
        />
      </section>

      <nav style={{ display: "flex", gap: 12, marginBottom: 20 }}>
        <button
          onClick={() => setView("air")}
          aria-pressed={view === "air"}
        >
          Air Operations
        </button>

        <button
          onClick={() => setView("ground")}
          aria-pressed={view === "ground"}
        >
          Ground Operations
        </button>
      </nav>

      {view === "air" ? (
        <section>
          <h2>Flight Operations</h2>

          <table
            style={{ width: "100%", borderCollapse: "collapse" }}
          >
            <thead>
              <tr>
                <th>Flight</th>
                <th>Airline</th>
                <th>Type</th>
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
                  <td>{flight.type}</td>
                  <td>
                    {flight.origin} → {flight.destination}
                  </td>
                  <td>
                    {new Date(flight.scheduledTime).toLocaleTimeString()}
                  </td>
                  <td>{flight.status}</td>
                  <td>{flight.gate ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ) : (
        <section>
          <h2>Ground Operations</h2>

          <table
            style={{ width: "100%", borderCollapse: "collapse" }}
          >
            <thead>
              <tr>
                <th>Gate</th>
                <th>Status</th>
                <th>Assigned Flight</th>
                <th>Occupying Flight</th>
              </tr>
            </thead>
            <tbody>
              {gates.map((gate) => (
                <tr key={gate.code}>
                  <td>{gate.code}</td>
                  <td>{gate.status}</td>
                  <td>{gate.assignedFlightNumber ?? "—"}</td>
                  <td>{gate.occupyingFlightNumber ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </main>
  );
}

function Metric({
  label,
  value,
}: {
  label: string;
  value: string | number;
}) {
  return (
    <div
      style={{
        border: "1px solid #ccc",
        borderRadius: 8,
        padding: 16,
        minWidth: 150,
      }}
    >
      <div style={{ fontSize: 12, color: "#666" }}>{label}</div>
      <strong style={{ fontSize: 20 }}>{value}</strong>
    </div>
  );
}
