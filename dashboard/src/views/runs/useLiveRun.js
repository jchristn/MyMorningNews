import { enumerateObjects } from '../../utils/enumeration.js';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext.jsx';
import useInterval from '../../hooks/useInterval.js';
import { isActiveRun, mergeStage } from './runParts.js';
import useRunEvents from './useRunEvents.js';

// One run, kept current: read once, then followed over server-sent events while it is queued or running, with polling
// as the fallback when the stream cannot be used. initialRun (the run a POST returned) shows at once. onFinished(run)
// runs once when the run is seen in a final status. Callers key the component by run ID, so runId does not change.
// Returns { run, stages, error, live }.
export default function useLiveRun(runId, { initialRun = null, onFinished } = {}) {
  const { apiClient } = useAuth();
  const [run, setRun] = useState(initialRun);
  const [stages, setStages] = useState(null);
  const [error, setError] = useState(null);
  const finished = useRef(false);
  const onFinishedRef = useRef(onFinished);
  useEffect(() => {
    onFinishedRef.current = onFinished;
  });

  const reload = useCallback(async () => {
    if (!runId) return;
    try {
      const [r, s] = await Promise.all([apiClient.getRun(runId), enumerateObjects((query) => apiClient.listRunStages(runId, query)).catch(() => null)]);
      if (r) setRun(r);
      if (s) setStages(Array.isArray(s) ? s : s.objects || []);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, runId]);

  useEffect(() => {
    reload();
  }, [reload]);

  const active = Boolean(runId) && isActiveRun(run);
  const stream = useRunEvents(runId, active, {
    onRun: (r) => setRun(r),
    onStage: (stage) => setStages((current) => mergeStage(current, stage)),
    onDone: (r) => {
      if (r) setRun(r);
      else reload();
    }
  });
  const live = active && stream === 'live';
  useInterval(reload, active && !live ? 3000 : 0);

  useEffect(() => {
    if (!run || isActiveRun(run) || finished.current) return;
    finished.current = true;
    if (onFinishedRef.current) onFinishedRef.current(run);
  }, [run]);

  return { run, stages, error, live };
}
