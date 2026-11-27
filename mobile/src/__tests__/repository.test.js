import fs from 'fs';
import path from 'path';

// Repository rules (CLAUDE.md): no em-dashes anywhere, 127.0.0.1 rather than localhost, never port 12345, and the
// release version stays 0.1.0-alpha (store version 0.1.0) unless the owner approves a change.
const mobileRoot = path.join(__dirname, '..', '..');
const repoRoot = path.join(mobileRoot, '..');
const SKIP = new Set(['node_modules', 'dist', 'ios', 'android', '.expo', 'coverage', 'qa', 'assets']);

function files(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((d) => {
    if (SKIP.has(d.name)) return [];
    const full = path.join(dir, d.name);
    if (d.isDirectory()) return files(full);
    return /\.(js|jsx|json|md|yml|yaml)$/.test(d.name) && d.name !== 'package-lock.json' ? [full] : [];
  });
}

const extra = [path.join(repoRoot, 'docs', 'MOBILE_DEVICE_TESTING.md'), path.join(repoRoot, '.github', 'workflows', 'mobile.yml'), path.join(repoRoot, 'APP_STORE_SUBMISSION.md')].filter((f) => fs.existsSync(f));
const all = [...files(mobileRoot), ...extra];

describe('repository rules', () => {
  it('has no em-dashes in code, configuration, or documents', () => {
    const offenders = all.filter((f) => fs.readFileSync(f, 'utf8').includes(String.fromCharCode(0x2014)));
    expect(offenders.map((f) => path.relative(repoRoot, f))).toEqual([]);
  });

  it('uses 127.0.0.1 for loopback and never port 12345', () => {
    const offenders = all.filter((f) => !f.endsWith('repository.test.js') && /\blocalhost\b|:12345\b/.test(fs.readFileSync(f, 'utf8')));
    expect(offenders.map((f) => path.relative(repoRoot, f))).toEqual([]);
  });

  it('keeps the approved versions', () => {
    const pkg = JSON.parse(fs.readFileSync(path.join(mobileRoot, 'package.json'), 'utf8'));
    expect(pkg.version).toBe('0.1.0-alpha');
    const config = require('../../app.config.js')();
    expect(config.version).toBe('0.1.0');
    expect(config.extra.releaseVersion).toBe('0.1.0-alpha');
    expect(config.ios.buildNumber).toBe('12');
    expect(config.android.versionCode).toBe(12);
  });

  it('configures identity, background audio, links, and permissions', () => {
    const config = require('../../app.config.js')();
    expect(config).toMatchObject({ name: 'My Morning News', slug: 'mymorningnews', scheme: 'mymorningnews', orientation: 'default', userInterfaceStyle: 'automatic' });
    expect(config.ios).toMatchObject({ bundleIdentifier: 'ai.latticetech.mymorningnews', supportsTablet: true, associatedDomains: ['applinks:app.mymorningnews.ai'] });
    expect(config.ios.infoPlist.UIBackgroundModes).toEqual(['audio', 'remote-notification']);
    expect(config.ios.infoPlist.NSAppTransportSecurity).toEqual({ NSAllowsLocalNetworking: true });
    expect(config.ios.infoPlist.NSFaceIDUsageDescription).toMatch(/Face ID/);
    expect(config.android.package).toBe('ai.latticetech.mymorningnews');
    expect(config.android.permissions).toEqual(expect.arrayContaining(['android.permission.POST_NOTIFICATIONS', 'android.permission.USE_BIOMETRIC', 'android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK']));
    expect(config.android.intentFilters[0].data[0]).toMatchObject({ scheme: 'https', host: 'app.mymorningnews.ai' });
    // The EAS project exists and the signing certificate is committed, so updates are on and must be code signed.
    expect(config.extra.eas.projectId).toBe('d2070f2d-77b5-4f8f-8961-7fb5d00b6410');
    expect(config.updates).toMatchObject({
      enabled: true,
      url: 'https://u.expo.dev/d2070f2d-77b5-4f8f-8961-7fb5d00b6410',
      codeSigningCertificate: './updates/certificate.pem',
      codeSigningMetadata: { keyid: 'main', alg: 'rsa-v1_5-sha256' }
    });
  });
});
