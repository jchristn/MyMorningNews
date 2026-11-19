#!/usr/bin/env node
// Fails when npm audit reports an advisory that is not reviewed in audit-allowlist.json, and reports allowlist entries
// that are no longer needed.
const { execSync } = require('child_process');
const path = require('path');
const allow = require(path.join(__dirname, '..', 'audit-allowlist.json'));

let report;
try {
  report = JSON.parse(execSync('npm audit --json', { cwd: path.join(__dirname, '..'), stdio: ['ignore', 'pipe', 'ignore'] }).toString());
} catch (error) {
  report = JSON.parse(error.stdout.toString());
}
const allowed = new Set(allow.advisories.map((a) => a.id));
const found = new Map();
for (const vuln of Object.values(report.vulnerabilities ?? {})) {
  for (const via of vuln.via) {
    if (typeof via === 'object' && via.url) found.set(via.url.split('/').pop(), via.title + ' (' + via.name + ', ' + via.severity + ')');
  }
}
const unreviewed = [...found].filter(([id]) => !allowed.has(id));
const stale = [...allowed].filter((id) => !found.has(id));
for (const id of stale) console.log('Allowlist entry no longer reported, remove it: ' + id);
if (unreviewed.length > 0) {
  for (const [id, title] of unreviewed) console.error('Unreviewed advisory ' + id + ': ' + title);
  process.exit(1);
}
console.log('npm audit: ' + found.size + ' advisories, all reviewed in audit-allowlist.json.');
