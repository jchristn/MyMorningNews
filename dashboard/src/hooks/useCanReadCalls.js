import { useAuth } from '../context/AuthContext.jsx';

// Whether the caller may read the tenant-wide model call log and usage report. Decided from the session's role flags
// (tenant administrators and system administrators) rather than by probing an endpoint, so a member's page view never
// produces a 403 or an access-denied audit record. Auditors also hold this permission on the server, but the session
// does not say which roles it carries, so they see the member view here.
export default function useCanReadCalls() {
  const { isTenantAdmin } = useAuth();
  return isTenantAdmin;
}
