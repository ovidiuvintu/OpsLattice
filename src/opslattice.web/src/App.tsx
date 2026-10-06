import { useCallback, useEffect, useState } from "react";
import "./App.css";

import { getFlights } from "./api/flightsApi";
import { getGates } from "./api/gatesApi";
import { advanceSimulation, getSimulation } from "./api/simulationApi";

import { FlightTable } from "./components/FlightTable";
import { GatePanel } from "./components/GatePanel";
import { SimulationControls } from "./components/SimulationControls";

import type { Flight } from "./types/Flight";
import type { Gate } from "./types/Gate";

function App() {
  const [flights, setFlights] = useState<Flight[]>([]);
  const [gates, setGates] = useState<Gate[]>([]);
  const [currentTime, setCurrentTime] =
    useState<string | null>(null);
  const [isAdvancing, setIsAdvancing] =
    useState(false);
  const [error, setError] =
    useState<string | null>(null);

  const loadDashboard = useCallback(async () => {
    try {
      const [simulation, flightData, gateData] =
        await Promise.all([
          getSimulation(),
          getFlights(),
          getGates(),
        ]);

      setCurrentTime(simulation.currentTime);
      setFlights(flightData);
      setGates(gateData);
      setError(null);
    } catch {
      setError("Unable to load simulation data.");
    }
  }, []);

  useEffect(() => {
    void loadDashboard();
  }, [loadDashboard]);

  async function handleAdvance(minutes: number) {
    try {
      setIsAdvancing(true);
      setError(null);

      await advanceSimulation(minutes);
      await loadDashboard();
    } catch {
      setError("Unable to advance simulation.");
    } finally {
      setIsAdvancing(false);
    }
  }

  return (
    <main className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>OpsLattice</h1>
          <p>Real-Time Operations Simulation</p>
        </div>

        <div className="scenario-name">
          Airport Operations
        </div>
      </header>

      <SimulationControls
        currentTime={currentTime}
        isAdvancing={isAdvancing}
        onAdvance={handleAdvance}
      />

      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      <FlightTable flights={flights} />

      <GatePanel gates={gates} />
    </main>
  );
}

export default App;