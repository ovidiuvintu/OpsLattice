
import AirportAssistant from "./components/AirportAssistant";
import ControlTowerPage from "./pages/ControlTowerPage";

export default function App() {
  return (
    <main
      style={{
        maxWidth: 1600,
        margin: "0 auto",
        padding: "12px 20px",
        fontFamily: "Arial, sans-serif",
      }}
    >
      <header style={{ marginBottom: 12 }}>
        <h1 style={{ margin: "0 0 4px", fontSize: 24 }}>
          OpsLattice
        </h1>
        <p style={{ margin: 0, fontSize: 13, color: "#666" }}>
          Airport Operations Simulation
        </p>
      </header>

      <ControlTowerPage />

      <section style={{ marginTop: 16 }}>
        <AirportAssistant />
      </section>
    </main>
  );
}
