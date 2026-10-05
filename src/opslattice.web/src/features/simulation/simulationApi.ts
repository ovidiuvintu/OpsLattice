export interface SimulationState {
  currentTime: string;
}

const API_URL = "http://localhost:5160";

export async function advanceSimulation(
  minutes: number
): Promise<SimulationState> {
  const response = await fetch(
    `${API_URL}/api/simulation/advance`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ minutes }),
    }
  );

  if (!response.ok) {
    throw new Error("Failed to advance simulation.");
  }

  return response.json();
}export interface SimulationState {
  currentTime: string;
}