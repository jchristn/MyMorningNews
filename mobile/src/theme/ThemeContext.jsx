import React, { createContext, useContext, useMemo } from 'react';
import { useColorScheme } from 'react-native';
import { dark, light } from './palette';

const ThemeContext = createContext(light);

/** Resolve a theme preference ('system', 'light', 'dark') and the system scheme into a palette. */
export function resolvePalette(preference, systemScheme) {
  const mode = preference === 'light' || preference === 'dark' ? preference : systemScheme === 'dark' ? 'dark' : 'light';
  return mode === 'dark' ? dark : light;
}

/** Provides the palette for every screen; follows the system unless the user chose light or dark. */
export function ThemeProvider({ preference, children }) {
  const system = useColorScheme();
  const palette = useMemo(() => resolvePalette(preference, system), [preference, system]);
  return <ThemeContext.Provider value={palette}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  return useContext(ThemeContext);
}
