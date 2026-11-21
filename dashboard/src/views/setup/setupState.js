// Pure helpers that read a setup status (GET /v1.0/api/setup) into the Setup page's steps.

// The required model checks: one per chat endpoint in use, plus "model:none" when a task has no endpoint at all.
export function modelChecks(status) {
  const checks = status && Array.isArray(status.checks) ? status.checks : [];
  return checks.filter((c) => c.kind === 'Model' && c.required);
}

export function searchCheck(status) {
  const checks = status && Array.isArray(status.checks) ? status.checks : [];
  return checks.find((c) => c.kind === 'Search') || null;
}

export function optionalChecks(status) {
  const checks = status && Array.isArray(status.checks) ? status.checks : [];
  return checks.filter((c) => !c.required);
}

// True when the check is about an endpoint someone set up that is not working: a failed test, a CLI that is not
// installed, a missing base URL, or the operator's shared key. A seeded endpoint that never got its secret is not.
export function isBrokenCheck(check) {
  if (!check || check.state === 'Ready' || check.state === 'Unverified') return false;
  if (check.state === 'Failed') return true;
  if (!check.resourceId) return false;
  if (check.usesPlatformKey) return true;
  return !(check.needsApiKey && !check.hasApiKey);
}

// Step 1's state: 'ready' (every model check passed), 'broken' (a configured endpoint needs fixing), 'checking' (a
// stored sign-in has not been tested yet), or 'empty' (nothing is connected, so the provider choice opens).
export function modelStepState(checks) {
  const list = Array.isArray(checks) ? checks : [];
  if (list.length > 0 && list.every((c) => c.state === 'Ready')) return 'ready';
  if (list.some(isBrokenCheck)) return 'broken';
  if (list.some((c) => c.state === 'Unverified')) return 'checking';
  return 'empty';
}

// The model check a connect attempt is about: the one for the chosen provider, else the first that is not ready.
export function connectedCheck(checks, provider) {
  const list = Array.isArray(checks) ? checks : [];
  return list.find((c) => c.provider === provider) || list.find((c) => c.state !== 'Ready') || list[0] || null;
}

// True when a required check has what it needs (a secret, or none is needed) but has not been checked with a live
// call, so the page checks it once on open.
export function needsAutoVerify(checks) {
  return (Array.isArray(checks) ? checks : []).some((c) => c.required && c.state === 'Unverified' && (c.hasApiKey || !c.needsApiKey));
}

// The progress state of a step for the checklist: 'done', 'attention', or 'todo'.
export function progressState(stepState) {
  if (stepState === 'ready' || stepState === 'Ready') return 'done';
  if (stepState === 'broken' || stepState === 'Failed' || stepState === 'Missing') return 'attention';
  return 'todo';
}

// True for an http or https URL.
export function isHttpUrl(value) {
  try {
    const url = new URL(String(value || '').trim());
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}
