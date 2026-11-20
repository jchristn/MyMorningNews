// The Home greeting: the part of the day in the user's time zone and the name to greet them by.

// The hour (0 to 23) of a moment in a time zone, or in the browser's zone when none is given or it is not valid.
export function hourIn(date, timeZone) {
  const d = date instanceof Date ? date : new Date(date);
  if (timeZone) {
    try {
      const parts = new Intl.DateTimeFormat('en-US', { hour: 'numeric', hourCycle: 'h23', timeZone }).formatToParts(d);
      const hour = parts.find((p) => p.type === 'hour');
      if (hour) return Number(hour.value) % 24;
    } catch {
      // Fall through to the browser's zone.
    }
  }
  return d.getHours();
}

// 'morning' from 05:00 to 11:59, 'afternoon' from 12:00 to 17:59, else 'evening'.
export function partOfDay(date = new Date(), timeZone = null) {
  const hour = hourIn(date, timeZone);
  if (hour >= 5 && hour < 12) return 'morning';
  if (hour >= 12 && hour < 18) return 'afternoon';
  return 'evening';
}

// The first name from the profile, trimmed, or '' when the profile has none.
export function greetingName(profile) {
  return profile && typeof profile.firstName === 'string' ? profile.firstName.trim() : '';
}

// The greeting message id under home.greeting and its values: 'morning' with { name }, or 'morningPlain' when the
// profile has no first name.
export function greeting(profile, date = new Date(), timeZone = null) {
  const part = partOfDay(date, timeZone);
  const name = greetingName(profile);
  return name ? { id: part, values: { name } } : { id: part + 'Plain', values: {} };
}
