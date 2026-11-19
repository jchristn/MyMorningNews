import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { DEFAULT_PREFS, loadPrefs, savePrefs } from '@/storage/prefs';

const PrefsContext = createContext({ prefs: DEFAULT_PREFS, loaded: false, update: () => undefined });

/** Loads non-secret preferences once and persists every change. */
export function PrefsProvider({ children, initial }) {
  const [prefs, setPrefs] = useState(initial || DEFAULT_PREFS);
  const [loaded, setLoaded] = useState(Boolean(initial));

  useEffect(() => {
    if (initial) return undefined;
    let alive = true;
    void loadPrefs().then((p) => {
      if (!alive) return;
      setPrefs(p);
      setLoaded(true);
    });
    return () => {
      alive = false;
    };
  }, [initial]);

  const update = useCallback((patch) => {
    setPrefs((current) => {
      const next = { ...current, ...patch };
      void savePrefs(next);
      return next;
    });
  }, []);

  const value = useMemo(() => ({ prefs, loaded, update }), [prefs, loaded, update]);
  return <PrefsContext.Provider value={value}>{children}</PrefsContext.Provider>;
}

export function usePrefs() {
  return useContext(PrefsContext);
}
