// Which chat endpoints the tenant's research would use, and whether each has the secret its sign-in method needs (an
// API key, a subscription token, or a sign-in file). Mirrors the
// server's ModelGateway.ResolveAsync: a tenant-wide active assignment for the task wins when its endpoint is active,
// else the oldest active endpoint that supports chat. Pure functions so they are tested without rendering.

import { MODEL_TASKS } from './enums.js';
import { effectiveAuthMethod, needsSecret } from './modelAuth.js';

// The platform key reference. The server resolves it from its own environment, so the dashboard treats it as set.
export const PLATFORM_CREDENTIAL_ID = 'platform:anthropic';

export const CHAT_TASKS = MODEL_TASKS.filter((task) => task !== 'Embedding');

function byCreated(a, b) {
  return String(a.createdUtc || '').localeCompare(String(b.createdUtc || ''));
}

// What the server would use for a task, optionally for one topic: { endpoint, assignment, scope } where scope is
// 'topic' (the topic's own assignment), 'tenant' (the tenant-wide assignment), or 'fallback' (the oldest active
// endpoint able to serve the task). endpoint is null when nothing can serve it.
export function resolveTaskEndpoint(endpoints, assignments, task, topicId = null) {
  const list = Array.isArray(endpoints) ? endpoints : [];
  const active = (assignments || []).filter((a) => a.task === task && a.active !== false);
  const topicAssignment = topicId ? active.find((a) => a.topicId === topicId) : null;
  const assignment = topicAssignment || active.find((a) => !a.topicId) || null;
  if (assignment) {
    const assigned = list.find((e) => e.id === assignment.modelEndpointId);
    if (assigned && assigned.active !== false) return { endpoint: assigned, assignment, scope: topicAssignment ? 'topic' : 'tenant' };
  }
  const embedding = task === 'Embedding';
  const fallback =
    list
      .filter((e) => e.active !== false && (embedding ? e.supportsEmbeddings : e.supportsChat))
      .slice()
      .sort(byCreated)[0] || null;
  return { endpoint: fallback, assignment: null, scope: 'fallback' };
}

// The endpoint the server would use for a tenant-wide chat task, or null when none can serve it.
export function resolveChatEndpoint(endpoints, assignments, task) {
  return resolveTaskEndpoint(endpoints, assignments, task, null).endpoint;
}

// Why an endpoint's secret cannot be used: 'missing' (no credential), 'notFound', 'inactive', or 'expired'; null when
// it looks usable or the sign-in method needs none (a sign-in on the server, or a keyless local server). credentials
// is the tenant's vault list (no secrets), or null when it could not be read, in which case a stored reference is
// trusted. platformKeySet is the server's client-config flag; only an explicit false reports the platform key as
// missing.
export function keyProblem(endpoint, credentials, now = new Date(), platformKeySet = undefined) {
  if (!endpoint || !needsSecret(effectiveAuthMethod(endpoint))) return null;
  const id = endpoint.credentialId;
  if (!id) return 'missing';
  if (id === PLATFORM_CREDENTIAL_ID) return platformKeySet === false ? 'platformMissing' : null;
  if (!Array.isArray(credentials)) return null;
  const credential = credentials.find((c) => c.id === id);
  if (!credential) return 'notFound';
  if (credential.active === false) return 'inactive';
  if (credential.expiresUtc && new Date(credential.expiresUtc).getTime() < now.getTime()) return 'expired';
  return null;
}

// The chat endpoints in use whose key is missing or unusable: [{ endpoint, problem, tasks }], one entry per endpoint.
// Pass tasks to check only some roles (the wizard checks Refinement).
export function modelKeyProblems({
  endpoints,
  assignments,
  credentials,
  tasks = CHAT_TASKS,
  now = new Date(),
  platformKeySet = undefined
}) {
  const found = new Map();
  for (const task of tasks) {
    const endpoint = resolveChatEndpoint(endpoints, assignments, task);
    if (!endpoint) continue;
    const problem = keyProblem(endpoint, credentials, now, platformKeySet);
    if (!problem) continue;
    if (!found.has(endpoint.id)) found.set(endpoint.id, { endpoint, problem, tasks: [] });
    found.get(endpoint.id).tasks.push(task);
  }
  return Array.from(found.values());
}
