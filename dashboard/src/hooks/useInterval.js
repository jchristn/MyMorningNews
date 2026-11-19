import { useEffect, useRef } from 'react';

// Call a function every delayMs while delayMs is a positive number.
export default function useInterval(callback, delayMs) {
  const saved = useRef(callback);
  useEffect(() => {
    saved.current = callback;
  }, [callback]);
  useEffect(() => {
    if (!delayMs || delayMs <= 0) return undefined;
    const handle = window.setInterval(() => saved.current(), delayMs);
    return () => window.clearInterval(handle);
  }, [delayMs]);
}
