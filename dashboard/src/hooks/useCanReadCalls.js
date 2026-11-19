import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext.jsx';

// Whether the caller may read the model call log (TenantAdmin and Auditor). Members get 403, so pages hide what
// depends on it. null while the probe is in flight.
export default function useCanReadCalls() {
  const { apiClient } = useAuth();
  const [allowed, setAllowed] = useState(null);
  useEffect(() => {
    let cancelled = false;
    apiClient
      .listLlmCalls({ pageSize: 1 })
      .then(() => !cancelled && setAllowed(true))
      .catch((err) => !cancelled && setAllowed(!(err && (err.status === 403 || err.status === 401))));
    return () => {
      cancelled = true;
    };
  }, [apiClient]);
  return allowed;
}
