import type { Flight } from "../types/Flight";

const API_URL = "http://localhost:5160";

export async function getFlights(): Promise<Flight[]> {
  const response = await fetch(`${API_URL}/api/flights`);

  if (!response.ok) {
    throw new Error("Failed to load flights.");
  }

  return response.json();
}