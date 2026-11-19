#!/usr/bin/env node
// Runs ESLint over the shared dashboard modules (sharing.config.js) with eslint.dashboard-shared.config.js. While the
// shared list is empty, or dashboard/src/shared does not exist yet, there is nothing to check and it says so.
const childProcess = require('child_process');
const path = require('path');
const { sharedDirs, sharedAvailable } = require('../sharing.config');

if (sharedDirs.length === 0 || !sharedAvailable) {
  process.stdout.write('lint:shared: no shared dashboard modules yet (sharing.config.js sharedDirs is empty).\n');
  process.exit(0);
}
const mobile = path.resolve(__dirname, '..');
const repo = path.resolve(mobile, '..');
const result = childProcess.spawnSync(
  process.execPath,
  [path.join(mobile, 'node_modules', 'eslint', 'bin', 'eslint.js'), '--max-warnings', '0', '--config', path.join(mobile, 'eslint.dashboard-shared.config.js'), ...sharedDirs.map((d) => path.join('dashboard', 'src', 'shared', d))],
  { cwd: repo, stdio: 'inherit' },
);
process.exit(result.status === null ? 1 : result.status);
