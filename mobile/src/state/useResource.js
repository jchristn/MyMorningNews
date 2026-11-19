import { useCallback, useEffect, useRef, useState } from 'react';
import { isItemNotFound, isNotAvailable } from '@/api/errors';

/**
 * Load data for a screen. Status is `loading`, `ready`, `error`, `notAvailable` (the route does not exist on this
 * server yet), or `notFound` (the route exists but the item does not). `reload()` refetches and sets `refreshing` while
 * the old data stays visible; `poll()` refetches without the refresh indicator (status polling); `setData(next)`
 * replaces the data after an action returned the new state (a value or an updater function).
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
    async (mode) => {
      const mine = ++generation.current;
      if (mode === 'refresh') setState((s) => ({ ...s, refreshing: true }));
      else if (!mode) setState({ status: 'loading', data: null, error: null, refreshing: false });
      try {
        const data = await load();
        if (alive.current && mine === generation.current) setState({ status: 'ready', data, error: null, refreshing: false });
      } catch (error) {
        if (!alive.current || mine !== generation.current) return;
        setState((s) => ({ status: isNotAvailable(error) ? 'notAvailable' : isItemNotFound(error) ? 'notFound' : 'error', data: mode ? s.data : null, error, refreshing: false }));
      }
    },
    [load],
  );

  useEffect(() => {
    alive.current = true;
    void run(null);
    return () => {
      alive.current = false;
    };
  }, [run]);

  const reload = useCallback(() => run('refresh'), [run]);
  const poll = useCallback(() => run('silent'), [run]);
  const setData = useCallback((next) => {
    generation.current += 1;
    setState((s) => ({ ...s, status: 'ready', error: null, data: typeof next === 'function' ? next(s.data) : next }));
  }, []);
  return { ...state, reload, poll, setData };
}
