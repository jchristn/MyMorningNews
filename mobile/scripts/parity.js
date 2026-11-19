#!/usr/bin/env node
/**
 * Parity check (plan section 35.3): every dashboard route in MY_MORNING_NEWS_PLAN.md section 25.4 (plus the account
 * routes of section 33.1) is mapped in parity.json to a mobile screen as `implemented`, `planned` (with the plan phase
 * that delivers it), or `not-applicable` (with a reason).
 *
 * Fails on: an unknown status; a duplicate route; an implemented entry without a screen file; a planned entry without
 * a phase, or with a screen path that does not exist; a not-applicable entry without a reason; any route in the plan
 * the manifest does not cover. With --strict (before store submission) it also fails on anything planned.
 *
 * Usage: npm run parity:check   |   npm run parity:strict
 */
const fs = require('fs');
const path = require('path');

const STATUSES = ['implemented', 'planned', 'not-applicable'];
const mobileRoot = path.resolve(__dirname, '..');
const planPath = path.resolve(mobileRoot, '..', 'MY_MORNING_NEWS_PLAN.md');

/** Dashboard routes from the section 25.4 route inventory table (aliases excluded). */
function planRoutes(planText) {
  const start = planText.indexOf('### 25.4 Route inventory');
  if (start < 0) return [];
  const end = planText.indexOf('### 25.5', start);
  const section = planText.slice(start, end < 0 ? undefined : end);
  const routes = new Set();
  for (const line of section.split('\n')) {
    if (!line.startsWith('|')) continue;
    const cells = line.split('|').map((c) => c.trim());
    const routeCell = cells[2] || '';
    const withoutAliases = routeCell.replace(/\(alias `[^`]+`\)/g, '');
    for (const match of withoutAliases.matchAll(/`(\/[^`]*)`/g)) routes.add(match[1]);
  }
  return [...routes];
}

/** Problems with the manifest; an empty list means it passes. */
function checkParity(manifest, { strict = false, plan = [], root = mobileRoot } = {}) {
  const problems = [];
  const entries = Array.isArray(manifest.routes) ? manifest.routes : [];
  const seen = new Set();
  const screenExists = (screen) => fs.existsSync(path.join(root, 'src', screen));
  for (const entry of entries) {
    const id = entry.route || '(no route)';
    if (seen.has(entry.route)) problems.push(id + ': listed twice');
    seen.add(entry.route);
    if (!STATUSES.includes(entry.status)) {
      problems.push(id + ': unknown status "' + entry.status + '" (use ' + STATUSES.join(', ') + ')');
      continue;
    }
    if (entry.status === 'implemented') {
      if (!entry.screen) problems.push(id + ': implemented without a screen');
      else if (!screenExists(entry.screen)) problems.push(id + ': screen file missing: src/' + entry.screen);
    }
    if (entry.status === 'planned') {
      if (!entry.phase) problems.push(id + ': planned without the phase that delivers it');
      if (entry.screen && !screenExists(entry.screen)) problems.push(id + ': scaffold screen file missing: src/' + entry.screen);
      if (strict) problems.push(id + ': still planned (' + entry.phase + '); --strict requires implemented or not-applicable');
    }
    if (entry.status === 'not-applicable' && !(typeof entry.reason === 'string' && entry.reason.length >= 20)) {
      problems.push(id + ': not-applicable needs a reason');
    }
  }
  for (const route of plan) {
    if (!seen.has(route)) problems.push(route + ': dashboard route in the plan is missing from parity.json');
  }
  return problems;
}

function summary(manifest) {
  const counts = { implemented: 0, planned: 0, 'not-applicable': 0 };
  for (const entry of manifest.routes || []) if (counts[entry.status] !== undefined) counts[entry.status] += 1;
  return counts;
}

module.exports = { STATUSES, planRoutes, checkParity, summary };

if (require.main === module) {
  const strict = process.argv.includes('--strict');
  const manifest = JSON.parse(fs.readFileSync(path.join(mobileRoot, 'parity.json'), 'utf8'));
  const plan = fs.existsSync(planPath) ? planRoutes(fs.readFileSync(planPath, 'utf8')) : [];
  if (!plan.length) console.log('MY_MORNING_NEWS_PLAN.md not found; checking the manifest only.');
  const problems = checkParity(manifest, { strict, plan });
  const counts = summary(manifest);
  if (problems.length) {
    for (const problem of problems) console.error('parity: ' + problem);
    process.exit(1);
  }
  console.log('parity.json: ' + counts.implemented + ' implemented, ' + counts.planned + ' planned, ' + counts['not-applicable'] + ' not applicable' + (strict ? ' (strict)' : '') + '; covers all ' + plan.length + ' plan routes.');
}
