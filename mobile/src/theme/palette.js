// Design tokens mirroring the dashboard's (dashboard/src/index.css, light and dark), plus the brand colors (blue
// primary, the sunrise gold of the sun in assets/glyph.png, and its gray). Component styles use only these values.

/** Brand colors: blue primary (lighter on dark surfaces), the logo's sunrise gold and gray, and the dark splash navy. */
export const brand = Object.freeze({
  blue: '#2563eb',
  blueOnDark: '#60a5fa',
  gold: '#f2b705',
  gray: '#646464',
  splashDark: '#0f172a',
});

export const light = Object.freeze({
  dark: false,
  primary: '#2563eb',
  primaryStrong: '#1d4ed8',
  primarySoft: '#dbeafe',
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
  bg: '#f8fafc',
  surface: '#ffffff',
  surfaceAlt: '#f1f5f9',
  border: '#e2e8f0',
  borderStrong: '#cbd5e1',
  text: '#0f172a',
  textSecondary: '#475569',
  muted: '#5b6b82',
  overlay: 'rgba(15, 23, 42, 0.5)',
  brand: brand.blue,
  chart: ['#2563eb', '#1f9d55', '#c98a04', '#d23f3f', '#6b4fd8', '#0f8fb3', '#c0569a', '#64748b'],
});

export const dark = Object.freeze({
  dark: true,
  primary: '#60a5fa',
  primaryStrong: '#93c5fd',
  primarySoft: 'rgba(96, 165, 250, 0.16)',
  onPrimary: '#0b1224',
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
  bg: '#0f172a',
  surface: '#111c33',
  surfaceAlt: '#1a2742',
  border: '#26344f',
  borderStrong: '#384a6b',
  text: '#e2e8f0',
  textSecondary: '#cbd5e1',
  muted: '#94a3b8',
  overlay: 'rgba(2, 6, 23, 0.65)',
  brand: brand.blueOnDark,
  chart: ['#60a5fa', '#3cc47c', '#e3a92b', '#ef6464', '#9a85f0', '#3bb4d6', '#e07ab8', '#94a3b8'],
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
