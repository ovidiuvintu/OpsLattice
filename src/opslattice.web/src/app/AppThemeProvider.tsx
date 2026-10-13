
import {
  createContext,
  useContext,
  useMemo,
  useState,
} from "react";

import type { ReactNode } from "react";

import {
  ThemeProvider,
  CssBaseline,
  createTheme,
} from "@mui/material";

// Supported theme modes
type ThemeMode = "light" | "dark";

// Context interface
interface ThemeContextValue {
  mode: ThemeMode;
  toggleTheme: () => void;
}

// Create theme context
const ThemeContext = createContext<
  ThemeContextValue | undefined
>(undefined);

// Hook used by other components
export function useAppTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error(
      "useAppTheme must be used within AppThemeProvider"
    );
  }

  return context;
}

// Application theme provider
export function AppThemeProvider({
  children,
}: {
  children: ReactNode;
}) {
  // Restore the user's preferred theme
  const [mode, setMode] = useState<ThemeMode>(() => {
    const saved = localStorage.getItem(
      "opslattice-theme"
    );

    return saved === "light" || saved === "dark"
      ? saved
      : "dark";
  });

  // Switch between light and dark
  const toggleTheme = () => {
    setMode((current) => {
      const next =
        current === "dark" ? "light" : "dark";

      localStorage.setItem(
        "opslattice-theme",
        next
      );

      return next;
    });
  };

  // Create Material UI theme
  const theme = useMemo(
    () =>
      createTheme({
        palette: {
          mode,

          primary: {
            main:
              mode === "dark"
                ? "#60a5fa"
                : "#1976d2",
          },

          background: {
            default:
              mode === "dark"
                ? "#0f172a"
                : "#f3f6fa",

            paper:
              mode === "dark"
                ? "#1e293b"
                : "#ffffff",
          },
        },

        typography: {
          fontFamily:
            "Inter, Roboto, Arial, sans-serif",
        },

        shape: {
          borderRadius: 10,
        },

        components: {
          MuiPaper: {
            styleOverrides: {
              root: {
                backgroundImage: "none",
              },
            },
          },
        },
      }),
    [mode]
  );

  return (
    <ThemeContext.Provider
      value={{ mode, toggleTheme }}
    >
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ThemeContext.Provider>
  );
}
