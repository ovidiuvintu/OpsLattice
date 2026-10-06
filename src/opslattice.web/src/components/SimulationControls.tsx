interface SimulationControlsProps {
  currentTime: string | null;
  isAdvancing: boolean;
  onAdvance: (minutes: number) => void;
}

export function SimulationControls({
  currentTime,
  isAdvancing,
  onAdvance,
}: SimulationControlsProps) {
  const formattedTime = currentTime
    ? formatSimulationTime(currentTime)
    : "Loading...";

  return (
    <section className="simulation-controls">
      <div>
        <div className="section-label">
          Simulation Time
        </div>

        <div className="simulation-time">
          {formattedTime}
        </div>
      </div>

      <div className="simulation-actions">
        <button
          type="button"
          disabled={isAdvancing}
          onClick={() => onAdvance(1)}
        >
          +1 min
        </button>

        <button
          type="button"
          disabled={isAdvancing}
          onClick={() => onAdvance(10)}
        >
          +10 min
        </button>

        <button
          type="button"
          disabled={isAdvancing}
          onClick={() => onAdvance(30)}
        >
          +30 min
        </button>

        <button
          type="button"
          disabled={isAdvancing}
          onClick={() => onAdvance(60)}
        >
          +60 min
        </button>
      </div>
    </section>
  );
}

function formatSimulationTime(
  value: string
): string {
  const formatted = new Date(value).toLocaleString(
    "en-US",
    {
      timeZone: "UTC",
      month: "long",
      day: "numeric",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    }
  );

  return `${formatted} UTC`;
}