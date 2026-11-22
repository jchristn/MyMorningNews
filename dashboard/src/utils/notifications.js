import { BookOpen, Gauge, Headphones, ListChecks, TrendingUp, TriangleAlert } from 'lucide-react';

// What a user can be told about, and where (GET /v1.0/api/notifications/preferences). The order is the server's.
export const NOTIFICATION_KINDS = ['RunResults', 'BriefingReady', 'AudioReady', 'RunFailed', 'CapReached', 'PredictionDue'];
export const NOTIFICATION_CHANNELS = ['Email', 'Push', 'InApp'];
export const NOTIFICATION_REASONS = ['NoEmailAddress', 'EmailNotVerified', 'EmailNotConfigured', 'EmailUnsubscribed', 'PushNotConfigured', 'NoDevice'];

export const NOTIFICATIONS_PATH = '/dashboard/notifications';

const ICONS = { RunResults: ListChecks, BriefingReady: BookOpen, AudioReady: Headphones, RunFailed: TriangleAlert, CapReached: Gauge, PredictionDue: TrendingUp };

export function notificationIcon(kind) {
  return ICONS[kind] || ListChecks;
}

// The page a notification opens.
export function notificationTarget(n) {
  const id = (value) => encodeURIComponent(value);
  if (!n) return '/dashboard/home';
  switch (n.kind) {
    case 'RunResults':
    case 'RunFailed':
      return n.runId ? '/dashboard/runs?id=' + id(n.runId) : '/dashboard/runs';
    case 'BriefingReady':
      return n.briefingId ? '/dashboard/briefings/' + id(n.briefingId) : '/dashboard/briefings';
    case 'AudioReady':
      return n.briefingId ? '/dashboard/briefings/' + id(n.briefingId) + '?listen=1' : '/dashboard/listen';
    case 'CapReached':
      return '/dashboard/profile';
    case 'PredictionDue':
      return n.predictionId ? '/dashboard/predictions?id=' + id(n.predictionId) : '/dashboard/predictions';
    default:
      return '/dashboard/home';
  }
}

// The matrix as { kind: { channel: enabled } }, with false for any cell the server left out.
export function preferenceMatrix(prefs) {
  const matrix = {};
  for (const kind of NOTIFICATION_KINDS) {
    matrix[kind] = {};
    for (const channel of NOTIFICATION_CHANNELS) matrix[kind][channel] = false;
  }
  for (const cell of (prefs && prefs.preferences) || []) {
    if (matrix[cell.kind] && cell.channel in matrix[cell.kind]) matrix[cell.kind][cell.channel] = Boolean(cell.enabled);
  }
  return matrix;
}

// Channel state as { channel: { available, reason } }; a channel the server left out counts as available.
export function channelStates(prefs) {
  const states = {};
  for (const channel of NOTIFICATION_CHANNELS) states[channel] = { available: true, reason: null };
  for (const row of (prefs && prefs.channels) || []) {
    if (states[row.channel]) states[row.channel] = { available: row.available !== false, reason: row.reason || null };
  }
  return states;
}

export function inAppIsOff(prefs) {
  if (!prefs || !Array.isArray(prefs.preferences)) return false;
  const matrix = preferenceMatrix(prefs);
  return NOTIFICATION_KINDS.every((kind) => !matrix[kind].InApp);
}
