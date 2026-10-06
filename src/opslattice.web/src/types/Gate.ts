export interface Gate {
  code: string;
  status: string;
  assignedFlight: string | null;
  occupyingFlight: string | null;
}