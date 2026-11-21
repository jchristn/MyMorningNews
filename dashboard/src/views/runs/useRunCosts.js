import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '../../context/AuthContext.jsx';

// A run's cost report (GET /v1.0/api/runs/{id}/costs), read when the run opens and again whenever signature changes
// (see utils/cost.costSignature: a stage finished, or the run's calls, cost, or tokens moved). It is never polled on a
// timer, so a live run costs one read per stage. Returns { report, error, reload }; report stays null when the
// server cannot give one, and the caller falls back to the run's own cost fields.
export default function useRunCosts(runId, signature) {
  const { apiClient } = useAuth();
  const [report, setReport] = useState(null);
  const [error, setError] = useState(null);
  const requestId = useRef(0);

  const reload = useCallback(async () => {
    if (!runId || typeof apiClient.getRunCosts !== 'function') return;
    const id = ++requestId.current;
    try {
      const result = await apiClient.getRunCosts(runId);
      if (id !== requestId.current) return;
      setReport(result && typeof result === 'object' && result.totals ? result : null);
      setError(null);
    } catch (err) {
      if (id === requestId.current) setError(err);
    }
  }, [apiClient, runId]);

  useEffect(() => {
    setReport(null);
    setError(null);
  }, [runId]);

  useEffect(() => {
    if (signature) reload();
  }, [reload, signature]);

  return { report: report && report.runId && report.runId !== runId ? null : report, error, reload };
}
