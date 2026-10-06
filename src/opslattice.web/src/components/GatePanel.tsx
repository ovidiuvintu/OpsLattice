import type { Gate } from "../types/Gate";

interface GatePanelProps {
  gates: Gate[];
}

export function GatePanel({
  gates,
}: GatePanelProps) {
  return (
    <section className="gates-section">
      <h2>Gates</h2>

      <div className="gate-grid">
        {gates.map((gate) => (
          <div
            key={gate.code}
            className="gate-card"
          >
            <div className="gate-code">
              {gate.code}
            </div>

            <div className="gate-status">
              {gate.status}
            </div>

            {gate.assignedFlight && (
              <div className="gate-flight">
                Assigned: {gate.assignedFlight}
              </div>
            )}

            {gate.occupyingFlight && (
              <div className="gate-flight">
                Occupying: {gate.occupyingFlight}
              </div>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}