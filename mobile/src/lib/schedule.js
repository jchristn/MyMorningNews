// Pure schedule helpers (plan section 14), the same rules as dashboard/src/utils/schedule.js. The server's day mask is
// Sunday 1, Monday 2, Tuesday 4, Wednesday 8, Thursday 16, Friday 32, Saturday 64, so the bit index equals
// Date.getDay().

export const DAY_BITS = [1, 2, 4, 8, 16, 32, 64];
export const WEEKDAYS_MASK = 62;
export const EVERY_DAY_MASK = 127;

/** Interval types the mobile editor offers: every type the server has, most used first. */
export const EDITABLE_INTERVALS = ['DaysOfWeek', 'EveryNDays', 'EveryNWeeks', 'DaysOfMonth', 'EveryNHours', 'EveryNMinutes', 'Once', 'Cron', 'Manual'];

/** What a schedule does with a run it missed while the server was down. */
export const MISSED_RUN_POLICIES = ['RunOnceOnRecovery', 'Skip'];

export const LAST_DAY_OF_MONTH = -1;

const WITH_DAYS = ['DaysOfWeek', 'EveryNWeeks'];
const WITH_TIMES = ['DaysOfWeek', 'EveryNDays', 'EveryNWeeks', 'DaysOfMonth'];

/** True when the interval type runs on chosen days of the week. */
export const usesDays = (type) => WITH_DAYS.includes(type);

/** True when the interval type runs at chosen times of day. */
export const usesTimes = (type) => WITH_TIMES.includes(type);

/** True when the interval type can be limited to a start and end (every repeating type). */
export const usesWindow = (type) => type !== 'Manual' && type !== 'Once';

/** Inclusive bounds for the interval value per type. */
export const INTERVAL_BOUNDS = { EveryNMinutes: [5, 1440], EveryNHours: [1, 168], EveryNDays: [1, 365], EveryNWeeks: [1, 52] };

export function hasDay(mask, dayIndex) {
  return (Number(mask) & DAY_BITS[dayIndex]) !== 0;
}

export function toggleDay(mask, dayIndex) {
  return (Number(mask) || 0) ^ DAY_BITS[dayIndex];
}

/** The device's IANA time zone, or UTC. */
export function deviceTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

/** "07:00, 18:30" as sorted, unique HH:mm values; invalid entries are dropped. */
export function parseTimes(text) {
  const times = String(text || '')
    .split(/[\s,;]+/)
    .map((s) => s.trim())
    .map((s) => {
      const m = /^(\d{1,2}):(\d{2})$/.exec(s);
      if (!m) return null;
      const h = Number(m[1]);
      const min = Number(m[2]);
      if (h > 23 || min > 59) return null;
      return String(h).padStart(2, '0') + ':' + String(min).padStart(2, '0');
    })
    .filter(Boolean);
  return [...new Set(times)].sort();
}

/** "1, 15, -1" (or "last") as sorted, unique days of the month, the last day (-1) at the end; invalid entries are dropped. */
export function parseMonthDays(text) {
  const days = String(text || '')
    .split(/[\s,;]+/)
    .map((s) => s.trim().toLowerCase())
    .filter(Boolean)
    .map((s) => (s === 'last' ? LAST_DAY_OF_MONTH : /^-?\d+$/.test(s) ? Number(s) : NaN))
    .filter((d) => d === LAST_DAY_OF_MONTH || (d >= 1 && d <= 31));
  return [...new Set(days)].sort((a, b) => (a === LAST_DAY_OF_MONTH ? 1 : b === LAST_DAY_OF_MONTH ? -1 : a - b));
}

const LOCAL = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{1,2}):(\d{2})$/;

/** True for a "YYYY-MM-DD HH:mm" wall-clock value. */
export function isLocalDateTime(text) {
  const m = LOCAL.exec(String(text || '').trim());
  return Boolean(m && Number(m[2]) >= 1 && Number(m[2]) <= 12 && Number(m[3]) >= 1 && Number(m[3]) <= 31 && Number(m[4]) <= 23 && Number(m[5]) <= 59);
}

function zoneOffsetMinutes(date, timeZone) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' }).formatToParts(date);
  const get = (type) => Number(parts.find((p) => p.type === type).value);
  const asUtc = Date.UTC(get('year'), get('month') - 1, get('day'), get('hour') % 24, get('minute'), get('second'));
  return Math.round((asUtc - Math.floor(date.getTime() / 1000) * 1000) / 60000);
}

/**
 * A wall-clock "YYYY-MM-DD HH:mm" in an IANA zone as an ISO UTC string, or null. Falls back to the device's own zone
 * when the runtime cannot format other zones.
 */
export function localToUtc(text, timeZone) {
  const m = LOCAL.exec(String(text || '').trim());
  if (!m || !isLocalDateTime(text)) return null;
  const [y, mo, d, h, mi] = [Number(m[1]), Number(m[2]), Number(m[3]), Number(m[4]), Number(m[5])];
  try {
    const guess = Date.UTC(y, mo - 1, d, h, mi);
    const zone = timeZone || 'UTC';
    const first = zoneOffsetMinutes(new Date(guess), zone);
    let utc = guess - first * 60000;
    const second = zoneOffsetMinutes(new Date(utc), zone);
    if (second !== first) utc = guess - second * 60000;
    return new Date(utc).toISOString();
  } catch {
    return new Date(y, mo - 1, d, h, mi).toISOString();
  }
}

/** An ISO instant as "YYYY-MM-DD HH:mm" in an IANA zone (the device zone when it cannot format others). */
export function utcToLocal(iso, timeZone) {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  try {
    const shifted = new Date(date.getTime() + zoneOffsetMinutes(date, timeZone || 'UTC') * 60000);
    return shifted.getUTCFullYear() + '-' + pad(shifted.getUTCMonth() + 1) + '-' + pad(shifted.getUTCDate()) + ' ' + pad(shifted.getUTCHours()) + ':' + pad(shifted.getUTCMinutes());
  } catch {
    return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()) + ' ' + pad(date.getHours()) + ':' + pad(date.getMinutes());
  }
}

/** The editor's form for a schedule (or a new one: weekdays at 07:00 in the device zone). */
export function scheduleForm(schedule, timeZone = deviceTimeZone()) {
  if (!schedule) {
    return { intervalType: 'DaysOfWeek', intervalValue: 1, minuteOffset: 0, daysOfWeekMask: WEEKDAYS_MASK, times: '07:00', monthDays: '1', cronExpression: '', runOnce: '', start: '', end: '', timeZone, enabled: true, missedRunPolicy: 'RunOnceOnRecovery' };
  }
  const zone = schedule.timeZone || 'UTC';
  return {
    intervalType: schedule.intervalType || 'Manual',
    intervalValue: schedule.intervalValue || 1,
    minuteOffset: schedule.minuteOffset || 0,
    daysOfWeekMask: schedule.daysOfWeekMask || 0,
    times: (Array.isArray(schedule.timesOfDay) && schedule.timesOfDay.length ? schedule.timesOfDay : ['07:00']).join(', '),
    monthDays: (Array.isArray(schedule.daysOfMonth) && schedule.daysOfMonth.length ? schedule.daysOfMonth : [1]).join(', '),
    cronExpression: schedule.cronExpression || '',
    runOnce: utcToLocal(schedule.runOnceUtc, zone),
    start: utcToLocal(schedule.startUtc, zone),
    end: utcToLocal(schedule.endUtc, zone),
    timeZone: zone,
    enabled: schedule.enabled !== false,
    missedRunPolicy: schedule.missedRunPolicy || 'RunOnceOnRecovery',
  };
}

/** The ScheduleUpsertRequest for a form: only the fields its interval type uses. */
export function scheduleBody(form, owner) {
  const type = form.intervalType;
  const body = { intervalType: type, timeZone: form.timeZone || 'UTC', enabled: form.enabled !== false };
  if (owner) {
    body.ownerType = owner.ownerType;
    body.ownerId = owner.ownerId;
  }
  const bounds = INTERVAL_BOUNDS[type];
  if (bounds) body.intervalValue = Math.min(bounds[1], Math.max(bounds[0], Math.round(Number(form.intervalValue) || bounds[0])));
  if (type === 'EveryNHours') body.minuteOffset = Math.min(59, Math.max(0, Math.round(Number(form.minuteOffset) || 0)));
  if (usesDays(type)) body.daysOfWeekMask = Number(form.daysOfWeekMask) || 0;
  if (type === 'DaysOfMonth') body.daysOfMonth = parseMonthDays(form.monthDays);
  if (usesTimes(type)) body.timesOfDay = parseTimes(form.times);
  if (type === 'Cron') body.cronExpression = String(form.cronExpression || '').trim();
  if (type === 'Once') {
    const once = localToUtc(form.runOnce, body.timeZone);
    if (once) body.runOnceUtc = once;
  }
  if (usesWindow(type)) {
    const start = localToUtc(form.start, body.timeZone);
    const end = localToUtc(form.end, body.timeZone);
    if (start) body.startUtc = start;
    if (end) body.endUtc = end;
  }
  if (type !== 'Manual' && form.missedRunPolicy) body.missedRunPolicy = form.missedRunPolicy;
  return body;
}

/** Why a form cannot be saved yet (a catalog key under mobile.schedule.problems), or null. */
export function scheduleProblem(form) {
  const type = form.intervalType;
  if (usesDays(type) && !(Number(form.daysOfWeekMask) > 0)) return 'days';
  if (type === 'DaysOfMonth' && parseMonthDays(form.monthDays).length === 0) return 'monthDays';
  if (usesTimes(type) && parseTimes(form.times).length === 0) return 'times';
  if (type === 'Cron' && String(form.cronExpression || '').trim().split(/\s+/).filter(Boolean).length < 5) return 'cron';
  if (type === 'Once' && !isLocalDateTime(form.runOnce)) return 'once';
  if (usesWindow(type) && [form.start, form.end].some((v) => String(v || '').trim() && !isLocalDateTime(v))) return 'window';
  return null;
}
