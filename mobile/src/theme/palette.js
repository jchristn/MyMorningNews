// Design tokens mirroring the dashboard's (dashboard/src/index.css, light and dark), plus the brand colors of
// assets/glyph.png. Component styles use only these values.

/** Brand colors (logo and glyph). */
export const brand = Object.freeze({ orange: '#f5820d', gray: '#646464', splashDark: '#121417' });

export const light = Object.freeze({
  dark: false,
  primary: '#2f6fdf',
  primaryStrong: '#2459b8',
  primarySoft: '#e8f0fd',
  onPrimary: '#ffffff',
  accent: '#6b4fd8',
  accentSoft: '#eeeafc',
  success: '#1f9d55',
  successSoft: '#e6f6ed',
  warning: '#9a6a03',
  warningSoft: '#fdf4dd',
  danger: '#d23f3f',
  dangerSoft: '#fdeaea',
  info: '#0f8fb3',
  infoSoft: '#e3f4fa',
  bg: '#f5f6f8',
  surface: '#ffffff',
  surfaceAlt: '#f0f2f5',
  border: '#dde1e6',
  borderStrong: '#c3c9d1',
  text: '#1b1f24',
  textSecondary: '#4b5563',
  muted: '#6b7380',
  overlay: 'rgba(15, 20, 26, 0.5)',
  brand: brand.orange,
  chart: ['#2f6fdf', '#1f9d55', '#c98a04', '#d23f3f', '#6b4fd8', '#0f8fb3', '#c0569a', '#6c7a89'],
});

export const dark = Object.freeze({
  dark: true,
  primary: '#5b8ff0',
  primaryStrong: '#7aa5f4',
  primarySoft: 'rgba(91, 143, 240, 0.16)',
  onPrimary: '#0b0e12',
  accent: '#9a85f0',
  accentSoft: 'rgba(154, 133, 240, 0.16)',
  success: '#3cc47c',
  successSoft: 'rgba(60, 196, 124, 0.14)',
  warning: '#e3a92b',
  warningSoft: 'rgba(227, 169, 43, 0.14)',
  danger: '#ef6464',
  dangerSoft: 'rgba(239, 100, 100, 0.14)',
  info: '#3bb4d6',
  infoSoft: 'rgba(59, 180, 214, 0.14)',
  bg: '#0f1317',
  surface: '#171c22',
  surfaceAlt: '#1e252d',
  border: '#2b333d',
  borderStrong: '#3a4450',
  text: '#e6edf3',
  textSecondary: '#b7c0cb',
  muted: '#8b95a1',
  overlay: 'rgba(0, 0, 0, 0.6)',
  brand: brand.orange,
  chart: ['#5b8ff0', '#3cc47c', '#e3a92b', '#ef6464', '#9a85f0', '#3bb4d6', '#e07ab8', '#8a98a8'],
});

export const spacing = Object.freeze({ xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 });
export const radius = Object.freeze({ sm: 4, md: 8, lg: 12, pill: 999 });
export const font = Object.freeze({ small: 13, body: 16, title: 18, heading: 24, mono: 13 });

/** Minimum touch target (points), per the Human Interface Guidelines and Material. */
export const MIN_TOUCH = 44;

/** Windows this wide or wider (points) use split views and the persistent mini player. */
export const WIDE_BREAKPOINT = 768;

/** Widest a single column of content gets on tablets: lines stay readable, controls stay grouped. */
export const CONTENT_MAX_WIDTH = 760;
