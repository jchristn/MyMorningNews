// Version comparison for the minimum app version the server publishes in client config (plan section 35). Pure, so
// the tests cover it directly. Versions follow semantic versioning: `MAJOR.MINOR.PATCH`, an optional prerelease after
// a dash (`0.1.0-alpha`, `1.0.0-rc.2`), and optional build metadata after a plus, which never affects the order.

const VERSION = /^v?(\d+(?:\.\d+)*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/;

/**
 * Parse a version into `{ numbers, prerelease }`, or null when the text is not a version. Missing parts count as
 * zero (`1.2` is `1.2.0`), and a leading `v` is allowed.
 */
export function parseVersion(input) {
  if (typeof input !== 'string') return null;
  const match = VERSION.exec(input.trim());
  if (!match) return null;
  const numbers = match[1].split('.').map((part) => Number(part));
  if (numbers.some((n) => !Number.isSafeInteger(n))) return null;
  const prerelease = match[2] ? match[2].split('.') : [];
  if (prerelease.some((part) => part === '')) return null;
  return { numbers, prerelease };
}

function compareIdentifiers(a, b) {
  const aNumeric = /^\d+$/.test(a);
  const bNumeric = /^\d+$/.test(b);
  if (aNumeric && bNumeric) return Math.sign(Number(a) - Number(b));
  // Numeric identifiers sort before alphanumeric ones.
  if (aNumeric) return -1;
  if (bNumeric) return 1;
  return a < b ? -1 : a > b ? 1 : 0;
}

/**
 * Compare two versions: -1 when `a` is older, 1 when it is newer, 0 when they are the same, and null when either is
 * not a version. A prerelease is older than its release (`0.1.0-alpha` is older than `0.1.0`), and prereleases order
 * by their dot-separated identifiers (`alpha` before `alpha.1` before `beta` before `rc.1`).
 */
export function compareVersions(a, b) {
  const left = parseVersion(a);
  const right = parseVersion(b);
  if (!left || !right) return null;
  const length = Math.max(left.numbers.length, right.numbers.length);
  for (let i = 0; i < length; i += 1) {
    const diff = (left.numbers[i] || 0) - (right.numbers[i] || 0);
    if (diff !== 0) return Math.sign(diff);
  }
  if (left.prerelease.length === 0 || right.prerelease.length === 0) {
    if (left.prerelease.length === right.prerelease.length) return 0;
    return left.prerelease.length === 0 ? 1 : -1;
  }
  const parts = Math.max(left.prerelease.length, right.prerelease.length);
  for (let i = 0; i < parts; i += 1) {
    if (left.prerelease[i] === undefined) return -1;
    if (right.prerelease[i] === undefined) return 1;
    const diff = compareIdentifiers(left.prerelease[i], right.prerelease[i]);
    if (diff !== 0) return diff;
  }
  return 0;
}

/**
 * True only when both versions parse and the installed one is older than the minimum. A missing or unreadable value
 * on either side answers false, so a server that sends nothing (or nonsense) never locks anyone out.
 */
export function isUpdateRequired(installed, minimum) {
  return compareVersions(installed, minimum) === -1;
}

const STORE_SCHEMES = /^(https|itms-apps|market):/i;

/**
 * The store listing to send an outdated app to, or null when none is known (the screen then shows no button).
 * Client config may carry `links.appStore` and `links.playStore`; on Android the Play listing also follows from the
 * application ID in the app config. iOS needs the numeric App Store ID, which only the server's link can supply.
 */
export function storeUrl({ platform, links, androidPackage }) {
  const fromConfig = links && (platform === 'ios' ? links.appStore : platform === 'android' ? links.playStore : null);
  if (typeof fromConfig === 'string' && STORE_SCHEMES.test(fromConfig.trim())) return fromConfig.trim();
  if (platform === 'android' && typeof androidPackage === 'string' && /^[A-Za-z][\w.]*$/.test(androidPackage)) {
    return 'https://play.google.com/store/apps/details?id=' + androidPackage;
  }
  return null;
}
