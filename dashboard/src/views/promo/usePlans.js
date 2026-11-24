import { enumerateObjects } from '../../utils/enumeration.js';
import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext.jsx';

// Plans ([{ plan, features }]) for filters and forms; [] until loaded or when the call fails.
export default function usePlans() {
  const { apiClient } = useAuth();
  const [plans, setPlans] = useState([]);
  const [error, setError] = useState(null);
  const [loaded, setLoaded] = useState(false);
  const reload = useCallback(() => {
    enumerateObjects((query) => apiClient.listPlans(query))
      .then((p) => {
        setPlans(Array.isArray(p) ? p : []);
        setError(null);
      })
      .catch((err) => {
        setPlans([]);
        setError(err);
      })
      .finally(() => setLoaded(true));
  }, [apiClient]);
  useEffect(() => {
    reload();
  }, [reload]);
  return { plans, error, loaded, reload };
}
