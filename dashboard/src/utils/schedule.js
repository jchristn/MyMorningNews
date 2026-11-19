// Pure schedule helpers shared by the ScheduleEditor, the topic Schedule tab, and the wizard Delivery step.
// The server mask is Sunday 1, Monday 2, Tuesday 4, Wednesday 8, Thursday 16, Friday 32, Saturday 64, so the bit
// index equals JavaScript's Date.getDay().

export const DAY_BITS = [1, 2, 4, 8, 16, 32, 64];
export const WEEKDAYS_MASK = 62;
export const WEEKENDS_MASK = 65;
export const EVERY_DAY_MASK = 127;
export const LAST_DAY_OF_MONTH = -1;

// Inclusive bounds for the interval value per type (EveryNMinutes uses the server minimum, default 5).
export const INTERVAL_BOUNDS = {
  EveryNMinutes: [5, 1440],
  EveryNHours: [1, 168],
  EveryNDays: [1, 365],
  EveryNWeeks: [1, 52]
};

export function hasDay(mask, dayIndex) {
  return (Number(mask) & DAY_BITS[dayIndex]) !== 0;
}

export function toggleDay(mask, dayIndex) {
  return (Number(mask) || 0) ^ DAY_BITS[dayIndex];
}

export function maskFromDays(dayIndexes) {
  return (dayIndexes || []).reduce((mask, d) => mask | DAY_BITS[d], 0);
}

export function daysFromMask(mask) {
  return DAY_BITS.map((bit, i) => ((Number(mask) & bit) !== 0 ? i : -1)).filter((i) => i >= 0);
}

// Which inputs the editor shows for an interval type.
export function fieldsFor(type) {
  const fields = new Set();
  switch (type) {
    case 'EveryNMinutes':
      fields.add('intervalValue');
      break;
    case 'EveryNHours':
      fields.add('intervalValue').add('minuteOffset');
      break;
    case 'EveryNDays':
      fields.add('intervalValue').add('timesOfDay');
      break;
    case 'EveryNWeeks':
      fields.add('intervalValue').add('daysOfWeek').add('timesOfDay');
      break;
    case 'DaysOfWeek':
      fields.add('daysOfWeek').add('timesOfDay');
      break;
    case 'DaysOfMonth':
      fields.add('daysOfMonth').add('timesOfDay');
      break;
    case 'Once':
      fields.add('runOnce');
      break;
    case 'Cron':
      fields.add('cronExpression');
      break;
    default:
      break;
  }
  if (type !== 'Manual') fields.add('timeZone').add('missedRunPolicy');
  if (type !== 'Manual' && type !== 'Once') fields.add('window');
  return fields;
}

export function browserTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

// IANA zones the browser knows, always including UTC and the given current value.
export function timeZoneOptions(current) {
  let zones = [];
  try {
    zones = typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : [];
  } catch {
    zones = [];
  }
  const set = new Set(['UTC', ...zones]);
  if (current) set.add(current);
  return Array.from(set).sort((a, b) => (a === 'UTC' ? -1 : b === 'UTC' ? 1 : a.localeCompare(b)));
}

function zonedParts(date, timeZone) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).formatToParts(date);
  const get = (type) => Number(parts.find((p) => p.type === type).value);
  return { year: get('year'), month: get('month'), day: get('day'), hour: get('hour') % 24, minute: get('minute'), second: get('second') };
}

// Minutes the zone is ahead of UTC at an instant.
export function zoneOffsetMinutes(date, timeZone) {
  const p = zonedParts(date, timeZone);
  const asUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second);
  return Math.round((asUtc - Math.floor(date.getTime() / 1000) * 1000) / 60000);
}

const LOCAL_PATTERN = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/;

// A wall-clock time ('YYYY-MM-DDTHH:mm') in an IANA zone, as an ISO UTC string. Returns null for blank input.
export function zonedLocalToUtc(local, timeZone) {
  const m = LOCAL_PATTERN.exec(String(local || ''));
  if (!m) return null;
  const guess = Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]), Number(m[4]), Number(m[5]));
  const zone = timeZone || 'UTC';
  const first = zoneOffsetMinutes(new Date(guess), zone);
  let utc = guess - first * 60000;
  const second = zoneOffsetMinutes(new Date(utc), zone);
  if (second !== first) utc = guess - second * 60000;
  return new Date(utc).toISOString();
}

// An ISO instant as a wall-clock 'YYYY-MM-DDTHH:mm' in an IANA zone, for datetime-local inputs.
export function utcToZonedLocal(iso, timeZone) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const p = zonedParts(d, timeZone || 'UTC');
  const pad = (n) => String(n).padStart(2, '0');
  return p.year + '-' + pad(p.month) + '-' + pad(p.day) + 'T' + pad(p.hour) + ':' + pad(p.minute);
}

export function defaultScheduleForm(timeZone = browserTimeZone()) {
  return {
    name: '',
    intervalType: 'DaysOfWeek',
    intervalValue: 1,
    minuteOffset: 0,
    daysOfWeekMask: WEEKDAYS_MASK,
    daysOfMonth: [1],
    timesOfDay: ['07:00'],
    cronExpression: '',
    timeZone,
    startLocal: '',
    endLocal: '',
    runOnceLocal: '',
    missedRunPolicy: 'Skip',
    enabled: true
  };
}

export function formFromSchedule(schedule) {
  if (!schedule) return defaultScheduleForm();
  const tz = schedule.timeZone || 'UTC';
  return {
    name: schedule.name || '',
    intervalType: schedule.intervalType || 'Manual',
    intervalValue: schedule.intervalValue || 1,
    minuteOffset: schedule.minuteOffset || 0,
    daysOfWeekMask: schedule.daysOfWeekMask || 0,
    daysOfMonth: Array.isArray(schedule.daysOfMonth) && schedule.daysOfMonth.length > 0 ? schedule.daysOfMonth : [1],
    timesOfDay: Array.isArray(schedule.timesOfDay) && schedule.timesOfDay.length > 0 ? schedule.timesOfDay : ['07:00'],
    cronExpression: schedule.cronExpression || '',
    timeZone: tz,
    startLocal: utcToZonedLocal(schedule.startUtc, tz),
    endLocal: utcToZonedLocal(schedule.endUtc, tz),
    runOnceLocal: utcToZonedLocal(schedule.runOnceUtc, tz),
    missedRunPolicy: schedule.missedRunPolicy || 'Skip',
    enabled: schedule.enabled !== false
  };
}

// The ScheduleUpsertRequest for a form: only the fields the interval type uses, plus the common ones.
export function scheduleBody(form) {
  const fields = fieldsFor(form.intervalType);
  const body = {
    name: form.name || '',
    intervalType: form.intervalType,
    timeZone: form.timeZone || 'UTC',
    enabled: form.enabled !== false,
    missedRunPolicy: form.missedRunPolicy || 'Skip'
  };
  if (fields.has('intervalValue')) body.intervalValue = Number(form.intervalValue) || 1;
  if (fields.has('minuteOffset')) body.minuteOffset = Math.max(0, Math.min(59, Number(form.minuteOffset) || 0));
  if (fields.has('daysOfWeek')) body.daysOfWeekMask = Number(form.daysOfWeekMask) || 0;
  if (fields.has('daysOfMonth')) body.daysOfMonth = [...new Set((form.daysOfMonth || []).map(Number))].sort((a, b) => (a === LAST_DAY_OF_MONTH ? 1 : b === LAST_DAY_OF_MONTH ? -1 : a - b));
  if (fields.has('timesOfDay')) body.timesOfDay = [...new Set((form.timesOfDay || []).filter(Boolean))].sort();
  if (fields.has('cronExpression')) body.cronExpression = form.cronExpression || '';
  if (fields.has('runOnce')) {
    const once = zonedLocalToUtc(form.runOnceLocal, body.timeZone);
    if (once) body.runOnceUtc = once;
  }
  if (fields.has('window')) {
    const start = zonedLocalToUtc(form.startLocal, body.timeZone);
    const end = zonedLocalToUtc(form.endLocal, body.timeZone);
    if (start) body.startUtc = start;
    if (end) body.endUtc = end;
  }
  return body;
}
