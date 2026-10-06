import type { Gate } from "../types/Gate";

const API_URL = "http://localhost:5160";

export async function getGates(): Promise<Gate[]> {
  const response = await fetch(`${API_URL}/api/gates`);

  if (!response.ok) {
    throw new Error("Failed to load gates.");
  }

  return response.json();
}