import { useCallback, useEffect, useRef, useState } from 'react';
import { isNotAvailable } from '@/api/errors';

/**
 * Load data for a screen. Status is `loading`, `ready`, `error`, or `notAvailable` (the route does not exist on this
 * server yet: 404, 405, or 501). `reload()` refetches and sets `refreshing` while the old data stays visible.
 *
 * @param {() => Promise<any>} loader Called on mount and whenever `deps` change.
 * @param {any[]} deps
 */
export function useResource(loader, deps) {
  const [state, setState] = useState({ status: 'loading', data: null, error: null, refreshing: false });
  const alive = useRef(true);
  const generation = useRef(0);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const load = useCallback(loader, deps);

  const run = useCallback(
    async (refreshing) => {
      const mine = ++generation.current;
      setState((s) => (refreshing ? { ...s, refreshing: true } : { status: 'loading', data: null, error: null, refreshing: false }));
      try {
        const data = await load();
        if (alive.current && mine === generation.current) setState({ status: 'ready', data, error: null, refreshing: false });
      } catch (error) {
        if (!alive.current || mine !== generation.current) return;
        setState((s) => ({ status: isNotAvailable(error) ? 'notAvailable' : 'error', data: refreshing ? s.data : null, error, refreshing: false }));
      }
    },
    [load],
  );

  useEffect(() => {
    alive.current = true;
    void run(false);
    return () => {
      alive.current = false;
    };
  }, [run]);

  const reload = useCallback(() => run(true), [run]);
  return { ...state, reload };
}
