import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext.jsx';

// The public client configuration (GET /v1.0/api/client-config): whether sign-up is open, the Turnstile site key,
// the minimum age, and the terms version. Resolves to null when the server cannot be reached.
export default function useClientConfig() {
  const { apiClient } = useAuth();
  const [config, setConfig] = useState(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let live = true;
    Promise.resolve()
      .then(() => apiClient.getClientConfig())
      .then((c) => {
        if (live) setConfig(c && typeof c === 'object' && !Array.isArray(c) && !('items' in c) ? c : null);
      })
      .catch(() => {
        if (live) setConfig(null);
      })
      .finally(() => {
        if (live) setLoaded(true);
      });
    return () => {
      live = false;
    };
  }, [apiClient]);

  return { config, loaded };
}
