// What the mobile app shares with the web dashboard (plan section 35.2). One list, read by Metro, Jest, and ESLint.
//
// Shared modules live under dashboard/src/shared/ and are host agnostic and framework free: no window, document,
// localStorage, navigator, import.meta, react, or react-dom. The app imports them as `@dashboard/<dir>`, for example
// `@dashboard/api` (API client and routes), `@dashboard/i18n/catalog` (translations), and `@dashboard/lib`
// (formatters, chart math, briefing and finding helpers).
//
// The list is empty for now: the dashboard refactor that moves those modules into dashboard/src/shared/ (Phase 16)
// has not landed, so the app keeps its own client in src/api/ and its own catalog in src/i18n/. When a directory
// moves, add it to sharedDirs; the aliases below already resolve it, and `npm run lint:shared` starts checking it.
const fs = require('fs');
const path = require('path');

const dashboardShared = path.resolve(__dirname, '..', 'dashboard', 'src', 'shared');
/** Directories under dashboard/src/shared that the app imports, for example 'api', 'i18n/catalog', 'lib'. */
const sharedDirs = [];

module.exports = {
  sharedRoots: { dashboardShared },
  sharedDirs,
  /** True when the shared folder exists in this checkout (Metro only watches folders that exist). */
  sharedAvailable: fs.existsSync(dashboardShared),
};
