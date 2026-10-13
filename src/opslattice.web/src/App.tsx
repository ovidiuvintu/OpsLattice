
import {
  Box,
  Typography,
  Paper,
  Button,
} from "@mui/material";

import { useAppTheme } from "./app/AppThemeProvider";

export default function App() {
  const { mode, toggleTheme } = useAppTheme();

  return (
    <Box sx={{ p: 4 }}>
      <Typography variant="h4" gutterBottom>
        OpsLattice
      </Typography>

      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Airport Operations Dashboard
        </Typography>

        <Typography color="text.secondary" sx={{ mb: 3 }}>
          Welcome to OpsLattice.
        </Typography>

        <Button variant="contained" onClick={toggleTheme}>
          Switch to {mode === "dark" ? "Light" : "Dark"} Mode
        </Button>
      </Paper>
    </Box>
  );
}