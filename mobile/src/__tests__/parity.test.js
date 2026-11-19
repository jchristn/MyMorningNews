import fs from 'fs';
import path from 'path';

const { checkParity, planRoutes } = require('../../scripts/parity');

const root = path.join(__dirname, '..', '..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'parity.json'), 'utf8'));
const planFile = path.join(root, '..', 'MY_MORNING_NEWS_PLAN.md');
const plan = fs.existsSync(planFile) ? planRoutes(fs.readFileSync(planFile, 'utf8')) : [];

describe('parity with the dashboard', () => {
  it('reads the route inventory from the plan', () => {
    if (!plan.length) return;
    expect(plan).toEqual(expect.arrayContaining(['/today', '/briefings/:id', '/insights/usage', '/profile', '/admin/tenants']));
    expect(plan).not.toContain('/request-history');
  });

  it('maps every plan route, with valid statuses, screens, phases, and reasons', () => {
    expect(checkParity(manifest, { plan })).toEqual([]);
  });

  it('fails on an unknown status, a missing screen, and a missing reason', () => {
    const bad = {
      routes: [
        { route: '/a', status: 'done' },
        { route: '/b', status: 'implemented', screen: 'app/does-not-exist.jsx' },
        { route: '/c', status: 'not-applicable' },
        { route: '/d', status: 'planned' },
      ],
    };
    const problems = checkParity(bad, { plan: ['/a', '/b', '/c', '/d', '/e'] });
    expect(problems.join('\n')).toMatch(/\/a: unknown status/);
    expect(problems.join('\n')).toMatch(/\/b: screen file missing/);
    expect(problems.join('\n')).toMatch(/\/c: not-applicable needs a reason/);
    expect(problems.join('\n')).toMatch(/\/d: planned without the phase/);
    expect(problems.join('\n')).toMatch(/\/e: dashboard route in the plan is missing/);
  });

  it('fails on planned entries in strict mode', () => {
    const planned = { routes: [{ route: '/x', status: 'planned', phase: 'Phase 7' }] };
    expect(checkParity(planned, {})).toEqual([]);
    expect(checkParity(planned, { strict: true })).toHaveLength(1);
  });

  it('points every implemented route at a real screen', () => {
    for (const entry of manifest.routes.filter((r) => r.status === 'implemented')) {
      expect([entry.route, fs.existsSync(path.join(root, 'src', entry.screen))]).toEqual([entry.route, true]);
    }
  });
});
