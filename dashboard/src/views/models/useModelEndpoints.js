import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext.jsx';

// Every model endpoint in the tenant (for pickers and name lookups). Tenants have a handful, so one page suffices.
export default function useModelEndpoints(refreshSignal = 0) {
  const { apiClient } = useAuth();
  const [endpoints, setEndpoints] = useState([]);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState(null);

  const reload = useCallback(async () => {
    try {
      const page = await apiClient.listModelEndpoints({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' });
      setEndpoints(page && page.items ? page.items : []);
      setError(null);
    } catch (err) {
      setError(err);
    } finally {
      setLoaded(true);
    }
  }, [apiClient]);

  useEffect(() => {
    reload();
  }, [reload, refreshSignal]);

  return { endpoints, loaded, error, reload };
}

export function endpointName(endpoints, id) {
  if (!id) return '';
  const match = endpoints.find((e) => e.id === id);
  return match ? match.name : id;
}
