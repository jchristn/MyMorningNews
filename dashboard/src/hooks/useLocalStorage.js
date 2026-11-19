import { useCallback, useState } from 'react';

// JSON value persisted in localStorage. Failures (private mode, bad JSON) fall back to the initial value.
export default function useLocalStorage(key, initialValue) {
  const [value, setValue] = useState(() => {
    try {
      const raw = window.localStorage.getItem(key);
      return raw === null ? initialValue : JSON.parse(raw);
    } catch {
      return initialValue;
    }
  });

  const update = useCallback(
    (next) => {
      setValue((current) => {
        const resolved = typeof next === 'function' ? next(current) : next;
        try {
          window.localStorage.setItem(key, JSON.stringify(resolved));
        } catch {
          // Ignore storage failures.
        }
        return resolved;
      });
    },
    [key]
  );

  return [value, update];
}
