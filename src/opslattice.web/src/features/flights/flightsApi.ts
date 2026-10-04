import type { Flight } from "./Flight";

const API_URL = "http://localhost:5160";

export async function getFlights(): Promise<Flight[]> {

  console.log("API URL:", API_URL);
  const response = await fetch(`${API_URL}/api/flights`);

  if (!response.ok) {
    throw new Error("Failed to retrieve flights.");
  }

  return response.json();
}