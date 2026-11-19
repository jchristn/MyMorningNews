import { useEffect, useRef } from 'react';

/** Default poll interval for queued work (runs, briefings being composed, requests running). */
export const POLL_MS = 3000;

/**
 * Call `callback` every `intervalMs` while `active` is true (status polling for queued and running work). The latest
 * callback is always used, and polling stops on unmount or when `active` turns false.
 */
export function usePolling(callback, active, intervalMs = POLL_MS) {
  const saved = useRef(callback);
  useEffect(() => {
    saved.current = callback;
  }, [callback]);
  useEffect(() => {
    if (!active) return undefined;
    const timer = setInterval(() => {
      void saved.current();
    }, intervalMs);
    return () => clearInterval(timer);
  }, [active, intervalMs]);
}
