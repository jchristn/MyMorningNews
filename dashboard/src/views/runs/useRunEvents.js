import { useEffect, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext.jsx';

// Follow a run's server-sent events while active is true. handlers: onRun(run), onStage(stage), onDone(run or null).
// Returns the stream state: 'idle' (not following), 'live' (connected), 'done' (the run finished), or 'failed' (the
// stream could not be used; the caller falls back to polling). A timeout event reconnects; done, unmounting, or
// active turning false stops it.
export default function useRunEvents(runId, active, handlers) {
  const { apiClient } = useAuth();
  const [state, setState] = useState('idle');
  const latest = useRef(handlers);
  useEffect(() => {
    latest.current = handlers;
  });

  useEffect(() => {
    if (!runId || !active) {
      setState((s) => (s === 'live' ? 'idle' : s));
      return undefined;
    }
    if (typeof apiClient.streamRunEvents !== 'function') {
      setState('failed');
      return undefined;
    }
    const controller = new AbortController();
    let stopped = false;
    const onEvent = ({ event, data }) => {
      if (stopped) return;
      const h = latest.current || {};
      if (event === 'run' && data && typeof data === 'object' && h.onRun) h.onRun(data);
      else if (event === 'stage' && data && typeof data === 'object' && h.onStage) h.onStage(data);
      else if (event === 'done' && h.onDone) h.onDone(data && typeof data === 'object' && !data.deleted ? data : null);
    };
    (async () => {
      setState('live');
      for (;;) {
        let last;
        try {
          last = await apiClient.streamRunEvents(runId, { signal: controller.signal, onEvent });
        } catch {
          if (!stopped) setState('failed');
          return;
        }
        if (stopped) return;
        if (last === 'timeout') continue;
        setState(last === 'done' ? 'done' : 'failed');
        return;
      }
    })();
    return () => {
      stopped = true;
      controller.abort();
    };
  }, [apiClient, runId, active]);

  return state;
}
