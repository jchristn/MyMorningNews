#!/usr/bin/env node
/**
 * My Morning News JavaScript SDK test harness.
 *
 * Runs every resource group of the SDK against a live server, creates its own data (names start with `SDK-js-`),
 * and deletes everything it created. Cases that would trigger real research runs or model calls are skipped unless
 * `--with-run` is given.
 *
 * Usage:
 *   node test-harness.js [--base-url URL] [--email EMAIL] [--password PASSWORD] [--tenant TENANT] [--with-run] [--verbose]
 *
 * Environment: MYMORNINGNEWS_BASE_URL, MYMORNINGNEWS_EMAIL, MYMORNINGNEWS_PASSWORD, MYMORNINGNEWS_TENANT.
 *
 * (c) 2026 Lattice AI LLC. Released under the MIT license.
 */

import {
    MyMorningNewsClient, MyMorningNewsError, TenantSelectionRequiredError, RunsResource, paginate, SDK_VERSION, DEFAULT_BASE_URL,
    signRequest, stringToSign, SIGNATURE_ALGORITHM
} from './mymorningnews-sdk.js';

// ---------------------------------------------------------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------------------------------------------------------

function parseArgs(argv) {
    const opts = {
        baseUrl: process.env.MYMORNINGNEWS_BASE_URL || DEFAULT_BASE_URL,
        email: process.env.MYMORNINGNEWS_EMAIL || 'admin@mymorningnews',
        password: process.env.MYMORNINGNEWS_PASSWORD || 'password',
        tenant: process.env.MYMORNINGNEWS_TENANT || 'default',
        demoEmail: process.env.MYMORNINGNEWS_DEMO_EMAIL || 'demo@mymorningnews',
        demoPassword: process.env.MYMORNINGNEWS_DEMO_PASSWORD || 'password',
        withRun: false,
        verbose: false
    };
    for (let i = 0; i < argv.length; i++) {
        const a = argv[i];
        const next = () => {
            if (i + 1 >= argv.length) throw new Error(a + ' needs a value.');
            return argv[++i];
        };
        if (a === '--base-url') opts.baseUrl = next();
        else if (a === '--email') opts.email = next();
        else if (a === '--password') opts.password = next();
        else if (a === '--tenant') opts.tenant = next();
        else if (a === '--with-run') opts.withRun = true;
        else if (a === '--verbose' || a === '-v') opts.verbose = true;
        else if (a === '--help' || a === '-h') {
            console.log('Usage: node test-harness.js [--base-url URL] [--email EMAIL] [--password PASSWORD] [--tenant TENANT] [--with-run] [--verbose]');
            process.exit(0);
        } else throw new Error('Unknown argument: ' + a);
    }
    opts.baseUrl = opts.baseUrl.replace(/\/+$/, '');
    return opts;
}

const OPTS = parseArgs(process.argv.slice(2));
const STAMP = Date.now().toString(36);
const PREFIX = 'SDK-js-';
const name = (what) => PREFIX + what + '-' + STAMP;

// ---------------------------------------------------------------------------------------------------------------------
// Harness plumbing
// ---------------------------------------------------------------------------------------------------------------------

class SkipCase extends Error {
    constructor(reason) {
        super(reason);
        this.name = 'SkipCase';
    }
}

const results = [];
const cleanups = [];

function log(line) {
    process.stdout.write(line + '\n');
}

function verbose(line) {
    if (OPTS.verbose) log('        ' + line);
}

function redact(text) {
    return String(text).replace(/[A-Za-z0-9_-]{60,}/g, '<redacted>');
}

async function run(caseName, fn, options = {}) {
    if (options.skip) {
        results.push({ name: caseName, status: 'SKIP', ms: 0, reason: options.skip });
        log('SKIP  ' + caseName.padEnd(58) + '     0 ms  (' + options.skip + ')');
        return undefined;
    }
    const started = performance.now();
    try {
        const value = await fn();
        const ms = Math.round(performance.now() - started);
        results.push({ name: caseName, status: 'PASS', ms });
        log('PASS  ' + caseName.padEnd(58) + String(ms).padStart(6) + ' ms');
        return value;
    } catch (err) {
        const ms = Math.round(performance.now() - started);
        if (err instanceof SkipCase) {
            results.push({ name: caseName, status: 'SKIP', ms, reason: err.message });
            log('SKIP  ' + caseName.padEnd(58) + String(ms).padStart(6) + ' ms  (' + err.message + ')');
            return undefined;
        }
        results.push({ name: caseName, status: 'FAIL', ms, reason: err?.message });
        log('FAIL  ' + caseName.padEnd(58) + String(ms).padStart(6) + ' ms');
        log('        ' + redact(describe(err)));
        if (OPTS.verbose && err?.stack) verbose(redact(err.stack));
        return undefined;
    }
}

function describe(err) {
    if (err instanceof MyMorningNewsError) {
        return err.name + ': ' + err.message + ' [status ' + err.status + ', code ' + err.code + ', requestId ' + err.requestId + ', ' + err.method + ' ' + err.path + ']';
    }
    return err?.message ?? String(err);
}

function skip(reason) {
    throw new SkipCase(reason);
}

function assert(condition, message) {
    if (!condition) throw new Error('Assertion failed: ' + message);
}

function assertEqual(actual, expected, message) {
    if (actual !== expected) throw new Error('Assertion failed: ' + message + ' (expected ' + JSON.stringify(expected) + ', got ' + JSON.stringify(actual) + ')');
}

function assertPage(page, what) {
    assert(page && Array.isArray(page.items), what + ' returns a page with items');
    assert(typeof page.pageNumber === 'number' && typeof page.pageSize === 'number', what + ' has pageNumber and pageSize');
    assert(typeof page.totalCount === 'number' && typeof page.totalPages === 'number', what + ' has totalCount and totalPages');
}

async function expectError(promise, status, code) {
    try {
        await promise;
    } catch (err) {
        assert(err instanceof MyMorningNewsError, 'error is a MyMorningNewsError (got ' + (err?.name ?? typeof err) + ': ' + err?.message + ')');
        if (status !== undefined) assertEqual(err.status, status, 'error status');
        if (code !== undefined) assertEqual(err.code, code, 'error code');
        if (err.status > 0) assert(typeof err.requestId === 'string' && err.requestId.length > 0, 'error carries a request ID');
        return err;
    }
    throw new Error('Expected a ' + status + ' ' + (code ?? '') + ' error, but the call succeeded.');
}

function onCleanup(label, fn) {
    cleanups.push({ label, fn });
}

async function runCleanups() {
    let failures = 0;
    while (cleanups.length > 0) {
        const { label, fn } = cleanups.pop();
        try {
            await fn();
            verbose('cleaned up ' + label);
        } catch (err) {
            if (err instanceof MyMorningNewsError && (err.status === 404 || err.status === 410)) continue;
            failures++;
            log('WARN  cleanup ' + label + ' failed: ' + redact(describe(err)));
        }
    }
    return failures;
}

async function poll(fn, until, timeoutMs, intervalMs = 1000) {
    const deadline = Date.now() + timeoutMs;
    let last = await fn();
    while (!until(last)) {
        if (Date.now() > deadline) return last;
        await new Promise((r) => setTimeout(r, intervalMs));
        last = await fn();
    }
    return last;
}

const text = (buf) => new TextDecoder('utf-8').decode(buf);

// ---------------------------------------------------------------------------------------------------------------------
// Template content used by the template cases
// ---------------------------------------------------------------------------------------------------------------------

function templateMarkdown(title, extra = '') {
    return [
        '---',
        'name: ' + title,
        'description: Created by the JavaScript SDK harness.',
        'tone: plain',
        'length: short',
        'audience: practitioner',
        'citations: numbered',
        '---',
        '',
        '# {{edition.name}} for {{date.long}}',
        '',
        '## Top of the news',
        '<!-- mmn:section source=findings tier=MustRead limit=3 order=relevance -->',
        'One paragraph per item.' + extra,
        '',
        '## Sources',
        '<!-- mmn:section source=citations -->',
        ''
    ].join('\n');
}

// ---------------------------------------------------------------------------------------------------------------------
// Cases
// ---------------------------------------------------------------------------------------------------------------------

async function main() {
    log('My Morning News JavaScript SDK ' + SDK_VERSION + ' test harness');
    log('Server ' + OPTS.baseUrl + ', user ' + OPTS.email + ', tenant ' + OPTS.tenant + (OPTS.withRun ? ', run cases ON' : ', run cases skipped'));
    log('');

    const client = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, timeoutMs: 60000 });
    const S = {};
    const withRun = OPTS.withRun ? null : 'needs --with-run (real runs and model calls)';
    const bogus = (prefix) => prefix + '_sdkjsdoesnotexist000000000';

    // ---- Server and auth -----------------------------------------------------------------------------------------

    await run('health (anonymous)', async () => {
        const h = await client.health();
        assertEqual(h.status, 'Healthy', 'health status');
        assert(typeof h.version === 'string', 'health has a version');
        assert(h.components && h.components.database === 'Healthy', 'database component is healthy');
    });

    await run('clientConfig (anonymous)', async () => {
        const c = await client.clientConfig();
        assertEqual(c.productName, 'My Morning News', 'product name');
        assert(typeof c.serverVersion === 'string', 'server version present');
    });

    await run('unauthenticated request is a typed 401', async () => {
        const err = await expectError(client.me(), 401, 'Unauthorized');
        assert(err.message.length > 0, 'error has a message');
    });

    await run('login with a wrong password is a typed 401', async () => {
        const c = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        await expectError(c.login(OPTS.email, 'definitely-not-the-password-' + STAMP, { tenantId: OPTS.tenant }), 401, 'Unauthorized');
        assertEqual(c.token, null, 'no token stored after a failed login');
    });

    const login = await run('login (email and password)', async () => {
        const t = await client.login(OPTS.email, OPTS.password, { tenantId: OPTS.tenant });
        assert(typeof t.token === 'string' && t.token.length > 20, 'token returned');
        assertEqual(client.token, t.token, 'token stored on the client');
        assertEqual(t.tenantId, OPTS.tenant, 'tenant');
        assert(typeof t.userId === 'string', 'user ID');
        S.userId = t.userId;
        return t;
    });
    if (!login) {
        log('');
        log('Login failed; nothing else can run.');
        return;
    }

    await run('tokenDetails', async () => {
        const d = await client.tokenDetails();
        assertEqual(d.valid, true, 'valid');
        assertEqual(d.userId, S.userId, 'user');
        assertEqual(String(d.email).toLowerCase(), OPTS.email.toLowerCase(), 'email');
        assert(d.expiresUtc, 'expiry present');
        S.isAdmin = d.isAdmin || d.isTenantAdmin;
    });

    await run('validateToken', async () => {
        const d = await client.validateToken();
        assertEqual(d.valid, true, 'valid');
    });

    await run('me', async () => {
        const me = await client.me();
        assertEqual(String(me.email).toLowerCase(), OPTS.email.toLowerCase(), 'email');
    });

    await run('serverInfo', async () => {
        const info = await client.serverInfo();
        assertEqual(info.product, 'My Morning News', 'product');
        assert(typeof info.version === 'string', 'version');
    });

    await run('settings.get (admin)', async () => {
        if (!S.isAdmin) skip('signed-in user is not an administrator');
        const s = await client.settings.get();
        assert(s.settings && typeof s.settings === 'object', 'settings object');
        assert(Array.isArray(s.fields), 'field descriptors');
    });

    await run('paginate walks every page', async () => {
        const first = await client.prompts.list({ pageSize: 3 });
        assertPage(first, 'prompts.list');
        const ids = [];
        for await (const p of paginate(client.prompts.list, { pageSize: 3 })) ids.push(p.id);
        assertEqual(ids.length, first.totalCount, 'items yielded across pages');
        assertEqual(new Set(ids).size, ids.length, 'no duplicates across pages');
        let limited = 0;
        for await (const _ of client.paginate(client.prompts.list, { pageSize: 2 }, { maxItems: 3 })) limited++;
        assertEqual(limited, Math.min(3, first.totalCount), 'maxItems stops early');
    });

    await run('request rejects a bad range before sending', async () => {
        let threw = false;
        try {
            await client.artifacts.content('art_x', { range: { start: 5, end: 1 } });
        } catch (err) {
            threw = err instanceof TypeError;
        }
        assert(threw, 'TypeError for an inverted range');
    });

    // ---- Access keys -----------------------------------------------------------------------------------------------

    await run('access key: direct headers and session exchange', async () => {
        if (!S.isAdmin) skip('signed-in user cannot create credentials');
        let created;
        try {
            created = await client.request('POST', '/v1.0/tenants/' + encodeURIComponent(OPTS.tenant) + '/credentials', {
                body: { userId: S.userId, name: name('credential'), authMode: 'Hybrid' }
            });
        } catch (err) {
            if (err instanceof MyMorningNewsError && (err.status === 403 || err.status === 404)) skip('credential creation not permitted (' + err.status + ')');
            throw err;
        }
        const credId = created.credential.id;
        onCleanup('credential', () => client.request('DELETE', '/v1.0/tenants/' + encodeURIComponent(OPTS.tenant) + '/credentials/' + encodeURIComponent(credId)));
        const direct = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, accessKey: created.credential.accessKey, secretKey: created.secretKey, tenantId: OPTS.tenant });
        const me = await direct.me();
        assertEqual(me.id, S.userId, 'direct access key acts as the user');
        const exchanged = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, tenantId: OPTS.tenant });
        const t = await exchanged.loginWithAccessKey(created.credential.accessKey, created.secretKey);
        assert(typeof t.token === 'string', 'session token from access key');
        assertEqual(t.credentialId, credId, 'token names the credential');
        const d = await exchanged.tokenDetails();
        assertEqual(d.credentialId, credId, 'token details name the credential');
        await exchanged.logout();
        assertEqual(exchanged.token, null, 'logout clears the token');
        await expectError(new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, accessKey: created.credential.accessKey, secretKey: 'wrong-' + STAMP, tenantId: OPTS.tenant }).me(), 401);
    });

    await run('signed requests: known vector (offline)', async () => {
        const input = { method: 'GET', pathAndQuery: '/v1.0/api/topics?pageSize=1', tenantId: 'default', date: '2026-01-01T00:00:00Z', nonce: 'nonce-12345678', body: '' };
        assertEqual(signRequest({ ...input, secretKey: 'secret_test' }), '230d17d0e29816b3bec0fd251a730c14a192f0994792062dbebb7512589aae24', 'known signature');
        const lines = stringToSign(input).split('\n');
        assertEqual(lines.length, 7, 'seven lines');
        assertEqual(lines[0], SIGNATURE_ALGORITHM, 'algorithm line');
        assertEqual(lines[6], 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', 'empty body hash');
        assertEqual(signRequest({ ...input, method: 'get', secretKey: 'secret_test' }), signRequest({ ...input, secretKey: 'secret_test' }), 'method is uppercased');
        assert(signRequest({ ...input, secretKey: 'secret_other' }) !== signRequest({ ...input, secretKey: 'secret_test' }), 'secret changes the signature');
    });

    await run('signed requests: client headers (offline)', async () => {
        let threw = false;
        try {
            new MyMorningNewsClient({ accessKey: 'ak', secretKey: 'sk', signedRequests: true });
        } catch (err) {
            threw = err instanceof TypeError;
        }
        assert(threw, 'signedRequests without tenantId is a TypeError');
        const seen = [];
        const fake = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1', accessKey: 'ak_test', secretKey: 'secret_test', tenantId: 'default', signedRequests: true,
            fetch: async (url, init) => {
                seen.push({ url, init });
                return new Response('{"items":[],"pageNumber":1,"pageSize":1,"totalCount":0,"totalPages":0,"durationMs":0}', { status: 200, headers: { 'content-type': 'application/json' } });
            }
        });
        await fake.topics.list({ pageSize: 1, search: 'a b' });
        await fake.schedules.preview({ intervalType: 'EveryNDays', intervalValue: 1 });
        await fake.topics.list({ pageSize: 1 });
        for (const { url, init } of seen) {
            const h = init.headers;
            assert(!('x-secret-key' in h), 'secret is not sent');
            assertEqual(h['x-access-key'], 'ak_test', 'access key sent');
            assertEqual(h['x-tenant-guid'], 'default', 'tenant sent');
            assert(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/.test(h['x-date']), 'x-date is ISO 8601 UTC');
            assert(h['x-nonce'].length >= 8 && h['x-nonce'].length <= 128, 'nonce length');
            const u = new URL(url);
            const expected = signRequest({ method: init.method, pathAndQuery: u.pathname + u.search, tenantId: 'default', date: h['x-date'], nonce: h['x-nonce'], body: init.body ?? '', secretKey: 'secret_test' });
            assertEqual(h['x-signature'], expected, 'signature over ' + init.method + ' ' + u.pathname + u.search);
        }
        assert(seen[0].init.headers['x-nonce'] !== seen[2].init.headers['x-nonce'], 'nonces are unique');
    });

    await run('signed requests: live credential', async () => {
        if (!S.isAdmin) skip('signed-in user cannot create credentials');
        const created = await client.request('POST', '/v1.0/tenants/' + encodeURIComponent(OPTS.tenant) + '/credentials', {
            body: { userId: S.userId, name: name('signed-credential'), authMode: 'SignedRequest' }
        });
        const credId = created.credential.id;
        const credPath = '/v1.0/tenants/' + encodeURIComponent(OPTS.tenant) + '/credentials/' + encodeURIComponent(credId);
        onCleanup('signed credential', () => client.request('DELETE', credPath));
        const accessKey = created.credential.accessKey;
        const signed = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, accessKey, secretKey: created.secretKey, tenantId: OPTS.tenant, signedRequests: true });
        assertEqual((await signed.me()).id, S.userId, 'signed GET acts as the user');
        assertEqual((await signed.tokenDetails()).credentialId, credId, 'principal is the credential');
        const page = await signed.topics.list({ pageSize: 1, search: 'SDK js signed' });
        assertPage(page, 'signed GET with a query');
        const preview = await signed.schedules.preview({ intervalType: 'EveryNDays', intervalValue: 1, timesOfDay: ['07:00'], previewCount: 2 });
        assertEqual(preview.valid, true, 'signed POST with a body');

        const path = '/v1.0/api/me';
        const date = new Date().toISOString().replace(/\.\d{3}Z$/, 'Z');
        const nonce = 'sdkjs-' + STAMP + '-replay';
        const headers = {
            'x-access-key': accessKey, 'x-tenant-guid': OPTS.tenant, 'x-date': date, 'x-nonce': nonce,
            'x-signature': signRequest({ method: 'GET', pathAndQuery: path, tenantId: OPTS.tenant, date, nonce, body: '', secretKey: created.secretKey })
        };
        const raw = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        assertEqual((await raw.request('GET', path, { headers, auth: false })).id, S.userId, 'hand-signed request accepted');
        await expectError(raw.request('GET', path, { headers, auth: false }), 401, 'Unauthorized');
        const tampered = { ...headers, 'x-nonce': nonce + '-2', 'x-signature': '0'.repeat(64) };
        await expectError(raw.request('GET', path, { headers: tampered, auth: false }), 401, 'Unauthorized');
        const wrongSecret = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, accessKey, secretKey: 'wrong-' + STAMP, tenantId: OPTS.tenant, signedRequests: true });
        await expectError(wrongSecret.me(), 401, 'Unauthorized');
        await client.request('DELETE', credPath);
        await expectError(signed.me(), 401, 'Unauthorized');
    });

    // ---- Topics ----------------------------------------------------------------------------------------------------

    await run('topics.create', async () => {
        const t = await client.topics.create({ name: name('topic'), description: 'Created by the JavaScript SDK harness.', seedQueries: ['sdk harness'] });
        assert(t.id && t.id.startsWith('top_'), 'topic ID');
        assertEqual(t.status, 'Draft', 'new topics are drafts');
        S.topic = t;
        onCleanup('topic', () => client.topics.remove(t.id));
    });

    await run('topics.create without a name is a typed 400', async () => {
        await expectError(client.topics.create({ description: 'no name' }), 400, 'ValidationFailed');
    });

    await run('topics.get', async () => {
        if (!S.topic) skip('no topic');
        const d = await client.topics.get(S.topic.id);
        assertEqual(d.topic.id, S.topic.id, 'topic');
        assert(Array.isArray(d.dimensions) && Array.isArray(d.constraints) && Array.isArray(d.recipients), 'detail lists');
    });

    await run('topics.update', async () => {
        if (!S.topic) skip('no topic');
        const t = await client.topics.update(S.topic.id, { description: 'Updated by the SDK harness.', displayThreshold: 40 });
        assertEqual(t.description, 'Updated by the SDK harness.', 'description updated');
        assertEqual(t.displayThreshold, 40, 'threshold updated');
    });

    await run('topics.list contains the topic', async () => {
        if (!S.topic) skip('no topic');
        const page = await client.topics.list({ search: S.topic.name, pageSize: 50 });
        assertPage(page, 'topics.list');
        assert(page.items.some((t) => t.id === S.topic.id), 'topic listed');
        const drafts = await client.topics.list({ status: 'Draft', mine: true, pageSize: 100 });
        assert(drafts.items.every((t) => t.status === 'Draft'), 'status filter applied');
    });

    await run('topics.resume a draft without a brief is a typed 409', async () => {
        if (!S.topic) skip('no topic');
        await expectError(client.topics.resume(S.topic.id), 409, 'Conflict');
    });

    await run('topics.pause, resume, archive (setStatus)', async () => {
        if (!S.topic) skip('no topic');
        assertEqual((await client.topics.pause(S.topic.id)).status, 'Paused', 'paused');
        assertEqual((await client.topics.setStatus(S.topic.id, 'Active')).status, 'Active', 'resumed');
        assertEqual((await client.topics.archive(S.topic.id)).status, 'Archived', 'archived');
    });

    await run('topics dimensions: add, list, update, remove', async () => {
        if (!S.topic) skip('no topic');
        const d = await client.topics.addDimension(S.topic.id, { name: name('dimension'), description: 'SDK dimension', weight: 'High', includeTerms: ['alpha'] });
        assert(d.id, 'dimension ID');
        const list = await client.topics.listDimensions(S.topic.id);
        assert(list.some((x) => x.id === d.id), 'dimension listed');
        const u = await client.topics.updateDimension(S.topic.id, d.id, { weight: 'Low', excludeTerms: ['beta'] });
        assertEqual(u.weight, 'Low', 'weight updated');
        assertEqual(u.excludeTerms[0], 'beta', 'exclude terms updated');
        S.dimensionId = d.id;
        await client.topics.removeDimension(S.topic.id, (await client.topics.addDimension(S.topic.id, { name: name('dimension-2') })).id);
        await expectError(client.topics.updateDimension(S.topic.id, bogus('dim'), { weight: 'Low' }), 404, 'NotFound');
    });

    await run('topics constraints: add, list by kind, update, remove', async () => {
        if (!S.topic) skip('no topic');
        const c = await client.topics.addConstraint(S.topic.id, { kind: 'IncludeKeyword', value: 'sdk-keyword', note: 'harness', dimensionId: S.dimensionId });
        assertEqual(c.kind, 'IncludeKeyword', 'kind');
        const byKind = await client.topics.listConstraints(S.topic.id, { kind: 'IncludeKeyword' });
        assert(byKind.some((x) => x.id === c.id), 'constraint listed by kind');
        const other = await client.topics.listConstraints(S.topic.id, { kind: 'BlockedDomain' });
        assert(!other.some((x) => x.id === c.id), 'kind filter excludes it');
        const u = await client.topics.updateConstraint(S.topic.id, c.id, { value: 'sdk-keyword-2' });
        assertEqual(u.value, 'sdk-keyword-2', 'value updated');
        await client.topics.removeConstraint(S.topic.id, c.id);
        assert(!(await client.topics.listConstraints(S.topic.id)).some((x) => x.id === c.id), 'constraint removed');
        await expectError(client.topics.addConstraint(S.topic.id, { kind: 'FeedUrl', value: 'not a url' }), 400, 'ValidationFailed');
    });

    await run('topics recipients: add, list, remove', async () => {
        if (!S.topic) skip('no topic');
        const r = await client.topics.addRecipient(S.topic.id, { email: 'sdk-js-' + STAMP + '@example.test' });
        assert(r.id, 'recipient ID');
        assert((await client.topics.listRecipients(S.topic.id)).some((x) => x.id === r.id), 'recipient listed');
        await client.topics.removeRecipient(S.topic.id, r.id);
        assert(!(await client.topics.listRecipients(S.topic.id)).some((x) => x.id === r.id), 'recipient removed');
        await expectError(client.topics.addRecipient(S.topic.id, { email: 'nope' }), 400, 'ValidationFailed');
    });

    await run('topics.duplicate', async () => {
        if (!S.topic) skip('no topic');
        const copy = await client.topics.duplicate(S.topic.id);
        onCleanup('topic copy', () => client.topics.remove(copy.topic.id));
        assert(copy.topic.id !== S.topic.id, 'new topic');
        assert(copy.topic.name.endsWith('(copy)'), 'copy name');
        assert(copy.dimensions.length >= 1, 'dimensions copied');
        await client.topics.remove(copy.topic.id);
        await expectError(client.topics.get(copy.topic.id), 404, 'NotFound');
    });

    await run('topics.getSearchProviders', async () => {
        if (!S.topic) skip('no topic');
        const p = await client.topics.getSearchProviders(S.topic.id);
        assert(Array.isArray(p), 'providers array');
    });

    await run('topics.rankingRecommendation', async () => {
        if (!S.topic) skip('no topic');
        const r = await client.topics.rankingRecommendation(S.topic.id);
        assert(r && typeof r === 'object', 'recommendation object');
    });

    await run('topics.learn (no feedback, no model call)', async () => {
        if (!S.topic) skip('no topic');
        const r = await client.topics.learn(S.topic.id);
        assertEqual(r.proposed, 0, 'nothing proposed without feedback');
        assert(Array.isArray(r.preferences), 'preferences array');
    });

    await run('topics.refine then refinement.abandon', async () => {
        if (!S.topic) skip('no topic');
        const state = await client.topics.refine(S.topic.id);
        assertEqual(state.topic.id, S.topic.id, 'session for the topic');
        await client.refinement.abandon(state.session.id);
        const still = await client.topics.get(S.topic.id);
        assertEqual(still.topic.id, S.topic.id, 'abandoning a re-refinement keeps the topic');
    });

    await run('cross-user: demo cannot see the admin topic', async () => {
        if (!S.topic) skip('no topic');
        const demo = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        try {
            await demo.login(OPTS.demoEmail, OPTS.demoPassword, { tenantId: OPTS.tenant });
        } catch (err) {
            if (err instanceof MyMorningNewsError && err.status === 401) skip('demo user not available');
            throw err;
        }
        try {
            const d = await demo.tokenDetails();
            if (d.isAdmin || d.isTenantAdmin) skip('demo user is an administrator');
            const err = await expectError(demo.topics.get(S.topic.id));
            assert(err.status === 404 || err.status === 403, 'read is 404 or 403 (got ' + err.status + ')');
            const page = await demo.topics.list({ search: S.topic.name, pageSize: 100 });
            assert(!page.items.some((t) => t.id === S.topic.id), 'topic not listed for demo');
            const upd = await expectError(demo.topics.update(S.topic.id, { description: 'hijack' }));
            assert(upd.status === 404 || upd.status === 403, 'update is 404 or 403');
        } finally {
            await demo.logout().catch(() => null);
        }
    });

    // ---- Refinement ------------------------------------------------------------------------------------------------

    await run('refinement.start, get, list, abandon', async () => {
        const state = await client.refinement.start({ seedText: name('refinement') + ' covering open source vector databases', goal: 'SDK harness' });
        assert(state.session.id, 'session ID');
        assertEqual(state.topic.status, 'Draft', 'draft topic');
        onCleanup('refinement topic', () => client.topics.remove(state.topic.id));
        onCleanup('refinement session', () => client.refinement.abandon(state.session.id));
        const again = await client.refinement.get(state.session.id);
        assertEqual(again.session.id, state.session.id, 'read back');
        const page = await client.refinement.list({ topicId: state.topic.id });
        assertPage(page, 'refinement.list');
        assert(page.items.some((s) => s.id === state.session.id), 'session listed');
        await client.refinement.abandon(state.session.id);
        await expectError(client.refinement.get(state.session.id), 404, 'NotFound');
        await expectError(client.topics.get(state.topic.id), 404, 'NotFound');
    });

    await run('refinement.start without seed text is a typed 400', async () => {
        await expectError(client.refinement.start({ seedText: '' }), 400, 'ValidationFailed');
    });

    await run('refinement generate (dimensions, questions, context)', async () => {
        const state = await client.refinement.start({ seedText: name('refinement-llm') + ' covering battery chemistry research' });
        onCleanup('refinement llm topic', () => client.topics.remove(state.topic.id));
        try {
            const d = await client.refinement.generateDimensions(state.session.id);
            assert(d.dimensions.length > 0, 'dimensions proposed');
            const q = await client.refinement.generateQuestions(state.session.id);
            assert(Array.isArray(q.questions), 'questions');
        } finally {
            await client.refinement.abandon(state.session.id).catch(() => null);
        }
    }, { skip: withRun });

    // ---- Research requests -----------------------------------------------------------------------------------------

    await run('requests.list and get missing is a typed 404', async () => {
        const page = await client.requests.list({ pageSize: 5 });
        assertPage(page, 'requests.list');
        await expectError(client.requests.get(bogus('rrq')), 404, 'NotFound');
        await expectError(client.requests.cancel(bogus('rrq')), 404, 'NotFound');
    });

    await run('requests.create, get, cancel, remove', async () => {
        const r = await client.requests.create({ requestText: name('request') + ': summarize this week in open source databases', skipQuestions: true, length: 'Short' });
        const id = r.request.id;
        onCleanup('request', () => client.requests.remove(id));
        assertEqual((await client.requests.get(id)).request.id, id, 'read back');
        await client.requests.cancel(id).catch(() => null);
        await client.requests.remove(id);
        await expectError(client.requests.get(id), 404, 'NotFound');
    }, { skip: withRun });

    // ---- Schedules -------------------------------------------------------------------------------------------------

    await run('schedules.preview (valid and invalid)', async () => {
        const ok = await client.schedules.preview({ intervalType: 'EveryNDays', intervalValue: 1, timesOfDay: ['07:00'], timeZone: 'America/Los_Angeles', previewCount: 3 });
        assertEqual(ok.valid, true, 'valid schedule');
        assertEqual(ok.nextRunsUtc.length, 3, 'three runs previewed');
        assertEqual(ok.nextRunsLocal.length, 3, 'local times');
        const bad = await client.schedules.preview({ intervalType: 'Cron', cronExpression: 'not a cron' });
        assertEqual(bad.valid, false, 'invalid schedule');
        assert(bad.errors.length > 0, 'errors listed');
    });

    await run('schedules CRUD (disabled schedule)', async () => {
        if (!S.topic) skip('no topic');
        const s = await client.schedules.create({ ownerType: 'Topic', ownerId: S.topic.id, name: name('schedule'), intervalType: 'EveryNDays', intervalValue: 1, timesOfDay: ['06:30'], timeZone: 'UTC', enabled: false });
        onCleanup('schedule', () => client.schedules.remove(s.id));
        assertEqual(s.enabled, false, 'created disabled');
        assertEqual((await client.schedules.get(s.id)).id, s.id, 'read back');
        const u = await client.schedules.update(s.id, { name: name('schedule-renamed'), timesOfDay: ['06:45'] });
        assertEqual(u.timesOfDay[0], '06:45', 'times updated');
        const page = await client.schedules.list({ ownerType: 'Topic', ownerId: S.topic.id });
        assert(page.items.some((x) => x.id === s.id), 'schedule listed');
        await client.schedules.remove(s.id);
        await expectError(client.schedules.get(s.id), 404, 'NotFound');
    });

    await run('schedules.create without intervalType is a typed 400', async () => {
        if (!S.topic) skip('no topic');
        await expectError(client.schedules.create({ ownerId: S.topic.id }), 400, 'ValidationFailed');
    });

    // ---- Runs ------------------------------------------------------------------------------------------------------

    await run('runs.list and read-only run routes', async () => {
        const page = await client.runs.list({ pageSize: 5, sortDirection: 'Descending' });
        assertPage(page, 'runs.list');
        await expectError(client.runs.get(bogus('run')), 404, 'NotFound');
        await expectError(client.runs.stages(bogus('run')), 404, 'NotFound');
        const r = page.items.find((x) => RunsResource.isTerminal(x.status));
        if (!r) skip('no finished run to read');
        assertEqual((await client.runs.get(r.id)).id, r.id, 'run read back');
        assert(Array.isArray(await client.runs.stages(r.id)), 'stages array');
        assert(Array.isArray(await client.runs.queries(r.id)), 'queries array');
        assert(Array.isArray(await client.runs.results(r.id)), 'results array');
        const topicExists = await client.topics.get(r.topicId).then(() => true, (err) => {
            if (err instanceof MyMorningNewsError && err.status === 404) return false;
            throw err;
        });
        if (topicExists) {
            const preview = await client.email.runPreview(r.id);
            assert(preview && typeof preview === 'object', 'email preview object');
        } else {
            verbose('run ' + r.id + ' has no topic; email preview is a typed 404');
            await expectError(client.email.runPreview(r.id), 404, 'NotFound');
        }
        const waited = await client.runs.wait(r.id, { timeoutMs: 5000, intervalMs: 200 });
        assertEqual(waited.id, r.id, 'wait returns a finished run at once');
    });

    await run('runs.llmCalls (paged, oldest first)', async () => {
        await expectError(client.runs.llmCalls(bogus('run')), 404, 'NotFound');
        const page = await client.runs.list({ pageSize: 20 });
        let checked = 0;
        for (const r of page.items) {
            const calls = await client.runs.llmCalls(r.id, { pageSize: 50 });
            assertPage(calls, 'runs.llmCalls');
            assert(calls.items.every((c) => c.runId === r.id), 'calls belong to the run');
            for (let i = 1; i < calls.items.length; i++) assert(calls.items[i - 1].createdUtc <= calls.items[i].createdUtc, 'oldest first');
            if (calls.items.length > 1) {
                const desc = await client.runs.llmCalls(r.id, { pageSize: 50, sortDirection: 'Descending' });
                assert(desc.items[0].createdUtc >= desc.items[desc.items.length - 1].createdUtc, 'sortDirection honoured');
                checked++;
                break;
            }
        }
        if (page.items.length === 0) skip('no runs');
        verbose('runs with several model calls checked: ' + checked);
    });

    await run('runs.wait times out with a typed error', async () => {
        let calls = 0;
        const fake = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1',
            fetch: async () => {
                calls++;
                return new Response(JSON.stringify({ id: 'run_fake', status: 'Running' }), { status: 200, headers: { 'content-type': 'application/json' } });
            }
        });
        const err = await expectError(fake.runs.wait('run_fake', { timeoutMs: 350, intervalMs: 100 }), 0, 'Timeout');
        assert(err.body && err.body.status === 'Running', 'last run attached');
        assert(calls >= 2, 'polled more than once');
    });

    await run('topics.run (dry run) and runs.wait', async () => {
        const t = await client.topics.create({ name: name('run-topic'), brief: 'Track notable releases of open source vector databases this week.', status: 'Active' });
        onCleanup('run topic', () => client.topics.remove(t.id));
        await client.topics.addDimension(t.id, { name: 'Releases', description: 'New versions and features' });
        const queued = await client.topics.dryRun(t.id);
        assert(queued.isDryRun, 'dry run');
        const done = await client.runs.wait(queued.id, { timeoutMs: 15 * 60000, intervalMs: 5000 });
        assert(RunsResource.isTerminal(done.status), 'terminal');
        assert(Array.isArray(await client.runs.stages(done.id)), 'stages');
    }, { skip: withRun });

    // ---- Findings, sources, predictions, suggestions ---------------------------------------------------------------

    await run('findings.list and get', async () => {
        const page = await client.findings.list({ pageSize: 5, minScore: 0, includeDryRun: true });
        assertPage(page, 'findings.list');
        await expectError(client.findings.get(bogus('fnd')), 404, 'NotFound');
        if (page.items.length > 0) {
            const d = await client.findings.get(page.items[0].id);
            assertEqual(d.finding.id, page.items[0].id, 'finding read back');
            assert(Array.isArray(d.evidence), 'evidence array');
        }
    });

    await run('findings update, hide, pin, remove (missing finding)', async () => {
        await expectError(client.findings.update(bogus('fnd'), { pinned: true }), 404, 'NotFound');
        await expectError(client.findings.hide(bogus('fnd')), 404, 'NotFound');
        await expectError(client.findings.remove(bogus('fnd')), 404, 'NotFound');
        await expectError(client.evidence.get(bogus('evd')), 404, 'NotFound');
        await expectError(client.findings.getEvidence(bogus('evd')), 404, 'NotFound');
        const hidden = await client.findings.list({ includeHidden: true, pinned: true, includeBelowThreshold: true, pageSize: 5 });
        assertPage(hidden, 'findings.list with includeHidden and pinned');
        assert(hidden.items.every((f) => f.pinned === true), 'pinned filter applied');
    });

    await run('findings pin and hide on a real finding', async () => {
        const page = await client.findings.list({ includeBelowThreshold: true, includeDryRun: true, pageSize: 1 });
        if (page.items.length === 0) skip('no findings to edit');
        const f = page.items[0];
        const was = { pinned: Boolean(f.pinned), hidden: Boolean(f.hidden) };
        try {
            await expectError(client.findings.update(f.id, {}), 400, 'ValidationFailed');
            assertEqual((await client.findings.pin(f.id)).pinned, true, 'pinned');
            assertEqual((await client.findings.unpin(f.id)).pinned, false, 'unpinned');
            assertEqual((await client.findings.hide(f.id)).hidden, true, 'hidden');
            const def = await client.findings.list({ runId: f.runId, includeBelowThreshold: true, includeDryRun: true, pageSize: 200 });
            assert(!def.items.some((x) => x.id === f.id), 'hidden finding left out by default');
            const all = await client.findings.list({ runId: f.runId, includeBelowThreshold: true, includeDryRun: true, includeHidden: true, pageSize: 200 });
            assert(all.items.some((x) => x.id === f.id), 'includeHidden lists it');
            assertEqual((await client.findings.unhide(f.id)).hidden, false, 'shown again');
            const d = await client.findings.get(f.id);
            if (d.evidence.length > 0) {
                const ev = await client.evidence.get(d.evidence[0].evidence.id);
                assertEqual(ev.evidence.id, d.evidence[0].evidence.id, 'evidence read back');
            }
        } finally {
            await client.findings.update(f.id, was).catch(() => null);
        }
    });

    await run('sources.list and get', async () => {
        const page = await client.sources.list({ pageSize: 5 });
        assertPage(page, 'sources.list');
        await expectError(client.sources.get(bogus('src')), 404, 'NotFound');
        if (page.items.length > 0) assertEqual((await client.sources.get(page.items[0].id)).id, page.items[0].id, 'source read back');
    });

    await run('predictions.list, get, resolve, reviews', async () => {
        const page = await client.predictions.list({ pageSize: 5, status: 'Open' });
        assertPage(page, 'predictions.list');
        await expectError(client.predictions.get(bogus('prd')), 404, 'NotFound');
        await expectError(client.predictions.resolve(bogus('prd'), { status: 'Accurate' }), 404, 'NotFound');
        await expectError(client.predictions.acceptReview(bogus('prv')), 404, 'NotFound');
        await expectError(client.predictions.rejectReview(bogus('prv')), 404, 'NotFound');
        if (page.items.length > 0) assertEqual((await client.predictions.get(page.items[0].id)).prediction.id, page.items[0].id, 'prediction read back');
    });

    await run('suggestions.list, get, setStatus', async () => {
        const page = await client.suggestions.list({ pageSize: 5 });
        assertPage(page, 'suggestions.list');
        await expectError(client.suggestions.get(bogus('sug')), 404, 'NotFound');
        await expectError(client.suggestions.setStatus(bogus('sug'), 'Dismissed'), 404, 'NotFound');
        if (page.items.length > 0) assertEqual((await client.suggestions.get(page.items[0].id)).id, page.items[0].id, 'suggestion read back');
    });

    // ---- Learned preferences and feedback --------------------------------------------------------------------------

    await run('preferences CRUD, enable, disable', async () => {
        if (!S.topic) skip('no topic');
        const p = await client.preferences.create({ statement: name('preference') + ': prefer primary sources', kind: 'PreferSource', topicId: S.topic.id });
        onCleanup('preference', () => client.preferences.remove(p.id));
        assertEqual(p.status, 'Active', 'user preferences start active');
        const d = await client.preferences.get(p.id);
        assertEqual(d.preference.id, p.id, 'read back');
        assert(Array.isArray(d.support), 'support array');
        const u = await client.preferences.update(p.id, { statement: name('preference') + ': prefer official changelogs', kind: 'Prefer' });
        assertEqual(u.kind, 'Prefer', 'kind updated');
        const page = await client.preferences.list({ topicId: S.topic.id });
        assert(page.items.some((x) => x.id === p.id), 'listed by topic');
        assertEqual((await client.preferences.disable(p.id)).status, 'Disabled', 'disabled');
        assertEqual((await client.preferences.enable(p.id)).status, 'Active', 'enabled');
        await expectError(client.preferences.accept(p.id), 409, 'Conflict');
        await expectError(client.preferences.reject(p.id), 409, 'Conflict');
        await client.preferences.remove(p.id);
        await expectError(client.preferences.get(p.id), 404, 'NotFound');
    });

    await run('preferences.create without a statement is a typed 400', async () => {
        await expectError(client.preferences.create({ kind: 'Prefer' }), 400, 'ValidationFailed');
    });

    await run('feedback submit with reason, list, update, remove', async () => {
        const target = { id: 'sdk-js-' + STAMP + '.example.test' };
        const f = await client.feedback.submit({ targetType: 'Domain', targetId: target.id, action: 'relevant', reason: 'SDK harness reason' });
        onCleanup('feedback', () => client.feedback.remove(f.id));
        assertEqual(f.relevance, 'Relevant', 'action mapped to a rating');
        assertEqual(f.reason, 'SDK harness reason', 'reason saved');
        assertEqual(f.isCurrent, true, 'current');
        const page = await client.feedback.list({ targetType: 'Domain', targetId: target.id });
        assert(page.items.some((x) => x.id === f.id), 'feedback listed');
        const u = await client.feedback.update(f.id, { reason: 'SDK harness reason, edited' });
        assertEqual(u.reason, 'SDK harness reason, edited', 'reason edited');
        await client.feedback.remove(f.id);
        const after = await client.feedback.list({ targetId: target.id, includeHistory: true });
        assert(!after.items.some((x) => x.id === f.id), 'feedback retracted');
        await expectError(client.feedback.update(f.id, { reason: 'gone' }), 404, 'NotFound');
    });

    await run('feedback channel Mcp', async () => {
        const f = await client.feedback.submit({ targetType: 'Domain', targetId: 'sdk-js-mcp-' + STAMP + '.example.test', action: 'not-relevant', channel: 'Mcp' });
        onCleanup('mcp feedback', () => client.feedback.remove(f.id));
        assertEqual(f.channel, 'Mcp', 'channel saved');
        const page = await client.feedback.list({ channel: 'Mcp', targetId: f.targetId });
        assert(page.items.some((x) => x.id === f.id), 'listed by channel');
        await client.feedback.remove(f.id);
    });

    await run('feedback without a rating or reason is a typed 400', async () => {
        await expectError(client.feedback.submit({ targetType: 'Domain', targetId: 'example.test' }), 400, 'ValidationFailed');
        await expectError(client.feedback.submit({ targetType: 'Finding', targetId: bogus('fnd'), action: 'relevant' }), 404, 'NotFound');
    });

    // ---- Templates -------------------------------------------------------------------------------------------------

    await run('templates.validate', async () => {
        const ok = await client.templates.validate(templateMarkdown(name('template-check')));
        assertEqual(ok.valid, true, 'valid template');
        assert(ok.sections.length >= 1, 'sections parsed');
        const bad = await client.templates.validate({ content: '---\nname: x\n---\n## S\n<!-- mmn:section source=nonsense -->\n' });
        assert(bad.valid === false || bad.problems.length > 0, 'problems reported for a bad template');
    });

    await run('templates CRUD, versions, rollback, export', async () => {
        const tname = name('template');
        const t = await client.templates.create({ content: templateMarkdown(tname), name: tname, shared: false });
        const id = t.template.id;
        S.templateId = id;
        onCleanup('template', () => client.templates.remove(id));
        assertEqual(t.shared, false, 'personal template');
        const v1 = t.template.currentVersion;
        assertEqual((await client.templates.get(id)).template.id, id, 'read back');
        const saved = await client.templates.update(id, { content: templateMarkdown(tname, ' Keep it brief.'), changeNote: 'SDK edit' });
        assertEqual(saved.template.currentVersion, v1 + 1, 'new version');
        assert(saved.content.includes('Keep it brief.'), 'content saved');
        const versions = await client.templates.versions(id);
        assert(Array.isArray(versions) && versions.length >= 2, 'two versions');
        const rolled = await client.templates.rollback(id, v1);
        assert(!rolled.content.includes('Keep it brief.'), 'rolled back');
        const exported = await client.templates.export(id);
        assert(String(exported.contentType).startsWith('text/markdown'), 'markdown content type');
        assert(exported.fileName && exported.fileName.endsWith('.md'), 'file name');
        assert(exported.text.includes('mmn:section'), 'export text');
        await expectError(client.templates.reset(id), 409, 'Conflict');
        const mine = await client.templates.list({ scope: 'mine', search: tname });
        assert(mine.items.some((x) => x.id === id), 'listed in my templates');
    });

    // ---- Prompts ---------------------------------------------------------------------------------------------------

    await run('prompts.list, get, versions', async () => {
        const page = await client.prompts.list({ key: 'briefing.compose', tenantOnly: true });
        assertPage(page, 'prompts.list');
        assert(page.items.length >= 1, 'tenant prompt present');
        S.promptId = page.items[0].id;
        const p = await client.prompts.get(S.promptId);
        assertEqual(p.template.key, 'briefing.compose', 'key');
        assert(p.content.length > 0 && Array.isArray(p.variables), 'content and variables');
        assert(Array.isArray(await client.prompts.versions(S.promptId)), 'versions');
        await expectError(client.prompts.get(bogus('pmt')), 404, 'NotFound');
    });

    await run('prompts topic override: create, update, rollback, reset, remove', async () => {
        if (!S.topic) skip('no topic');
        const o = await client.prompts.createOverride({ key: 'briefing.compose', topicId: S.topic.id });
        const id = o.template.id;
        onCleanup('prompt override', () => client.prompts.remove(id));
        assertEqual(o.template.topicId, S.topic.id, 'override for the topic');
        await expectError(client.prompts.createOverride({ key: 'briefing.compose', topicId: S.topic.id }), 409, 'Conflict');
        const v1 = o.template.currentVersion;
        const edited = o.content.replace('Write the opening paragraph only, in Markdown.', 'Write the opening paragraph only, in Markdown. Keep it short.');
        const u = await client.prompts.update(id, { content: edited, changeNote: 'SDK edit' });
        assertEqual(u.template.currentVersion, v1 + 1, 'new version');
        const rb = await client.prompts.rollback(id, v1);
        assert(!rb.content.includes('Keep it short.'), 'rolled back');
        const reset = await client.prompts.reset(id);
        assertEqual(reset.content.trim(), reset.defaultContent.trim(), 'reset to default');
        await client.prompts.remove(id);
        await expectError(client.prompts.get(id), 404, 'NotFound');
    });

    await run('prompts: tenant prompts cannot be deleted (typed 409)', async () => {
        if (!S.promptId) skip('no prompt');
        await expectError(client.prompts.remove(S.promptId), 409, 'Conflict');
        await expectError(client.prompts.rollback(S.promptId, 999999), 404, 'NotFound');
    });

    await run('prompts.lab', async () => {
        const r = await client.prompts.lab({ key: 'briefing.compose', variables: { 'edition.name': 'SDK', sections: '' } });
        assert(typeof r.renderedSystem === 'string', 'rendered');
    }, { skip: withRun });

    // ---- Editions, briefings, artifacts ----------------------------------------------------------------------------

    await run('editions CRUD and setTopics', async () => {
        if (!S.topic) skip('no topic');
        const ename = name('edition');
        const e = await client.editions.create({ name: ename, description: 'SDK edition', audioEnabled: false, emailEnabled: false, enabled: false, freshResearchBeforeCompose: false, briefingTemplateId: S.templateId });
        S.editionId = e.edition.id;
        onCleanup('edition', () => client.editions.remove(e.edition.id));
        assertEqual(e.edition.name, ename, 'name');
        await expectError(client.editions.create({ name: ename }), 409, 'Conflict');
        const u = await client.editions.update(S.editionId, { description: 'SDK edition, updated', lookbackMode: 'FixedHours', lookbackHours: 48 });
        assertEqual(u.edition.lookbackHours, 48, 'lookback updated');
        const t = await client.editions.setTopics(S.editionId, [S.topic.id]);
        assertEqual(t.topics.length, 1, 'one linked topic');
        assertEqual(t.topics[0].topicId, S.topic.id, 'linked topic');
        assertEqual(t.coveredTopicIds.length, 0, 'archived topic is not covered');
        assertEqual((await client.editions.get(S.editionId)).edition.id, S.editionId, 'read back');
        const page = await client.editions.list({ pageSize: 100 });
        assert(page.items.some((x) => x.id === S.editionId), 'edition listed');
    });

    await run('editions.preview (empty edition, no model calls)', async () => {
        if (!S.editionId) skip('no edition');
        const covered = (await client.editions.get(S.editionId)).coveredTopicIds;
        if (covered.length > 0) skip('edition covers active topics; preview would call a model');
        const p = await client.editions.preview(S.editionId);
        assertEqual(p.isDryRun, true, 'preview is a dry run');
        assert(typeof p.markdown === 'string' && p.markdown.length > 0, 'markdown rendered');
        const p2 = await client.editions.preview(S.editionId, { templateContent: templateMarkdown('SDK preview') });
        assert(typeof p2.markdown === 'string', 'preview with unsaved template');
        await expectError(client.editions.preview(S.editionId, { templateContent: '---\nname: x\n---\n## S\n<!-- mmn:section source=nonsense -->\n' }), 400, 'TemplateInvalid');
    });

    await run('editions.compose and briefing ready', async () => {
        if (!S.editionId) skip('no edition');
        const covered = (await client.editions.get(S.editionId)).coveredTopicIds;
        if (covered.length > 0) skip('edition covers active topics; compose would call a model');
        const queued = await client.editions.compose(S.editionId);
        S.briefingId = queued.id;
        onCleanup('briefing', () => client.briefings.remove(queued.id));
        assert(['Queued', 'Composing', 'Ready'].includes(queued.status), 'queued');
        const d = await poll(() => client.briefings.get(queued.id), (x) => x.briefing.status === 'Ready' || x.briefing.status === 'Failed', 90000, 1000);
        assertEqual(d.briefing.status, 'Ready', 'briefing composed (error: ' + d.briefing.errorMessage + ')');
        const calls = await client.llmCalls.list({ briefingId: queued.id });
        assertEqual(calls.totalCount, 0, 'composing an empty edition made no model calls');
    });

    await run('briefings.list, get, download Markdown and HTML', async () => {
        if (!S.briefingId) skip('no briefing');
        const page = await client.briefings.list({ editionId: S.editionId });
        assertPage(page, 'briefings.list');
        assert(page.items.some((b) => b.id === S.briefingId), 'briefing listed');
        assert(page.items.every((b) => b.markdown === null || b.markdown === undefined), 'list leaves Markdown out');
        const d = await client.briefings.get(S.briefingId);
        assert(d.files.length >= 2, 'Markdown and HTML files stored');
        const md = await client.briefings.download(S.briefingId);
        assertEqual(md.status, 200, 'status');
        assert(md.fileName && md.fileName.endsWith('.md'), 'markdown file name (' + md.fileName + ')');
        assert(md.data.byteLength > 0, 'markdown bytes');
        const mdText = await client.briefings.markdown(S.briefingId);
        assertEqual(mdText, text(md.data), 'markdown helper matches');
        const html = await client.briefings.html(S.briefingId);
        assert(/<html|<!doctype/i.test(html), 'html document');
        await expectError(client.briefings.download(S.briefingId, { format: 'pdf' }), 400, 'ValidationFailed');
    });

    await run('briefings.download with a byte range (206)', async () => {
        if (!S.briefingId) skip('no briefing');
        const full = await client.briefings.download(S.briefingId);
        const part = await client.briefings.download(S.briefingId, { range: { start: 0, end: 9 } });
        assertEqual(part.status, 206, 'partial content');
        assertEqual(part.data.byteLength, Math.min(10, full.data.byteLength), 'ten bytes');
        assertEqual(part.totalBytes, full.data.byteLength, 'total size from Content-Range');
        assertEqual(text(part.data), text(full.data.slice(0, 10)), 'same bytes');
        const tail = await client.briefings.download(S.briefingId, { range: { suffix: 5 } });
        assertEqual(tail.data.byteLength, Math.min(5, full.data.byteLength), 'suffix range');
        await expectError(client.briefings.download(S.briefingId, { range: { start: full.data.byteLength + 100 } }), 416, 'RangeNotSatisfiable');
    });

    await run('briefings.pin, unpin, regenerate', async () => {
        if (!S.briefingId) skip('no briefing');
        assertEqual((await client.briefings.pin(S.briefingId)).pinned, true, 'pinned');
        assertEqual((await client.briefings.unpin(S.briefingId)).pinned, false, 'unpinned');
        const r = await client.briefings.regenerate(S.briefingId);
        assertEqual(r.id, S.briefingId, 'regenerated');
        assert(r.markdownArtifactId && r.htmlArtifactId, 'files present after regenerate');
    });

    await run('artifacts.list, get, content, download, downloadUrl', async () => {
        if (!S.briefingId) skip('no briefing');
        const page = await client.artifacts.list({ briefingId: S.briefingId });
        assertPage(page, 'artifacts.list');
        assert(page.items.length >= 1, 'artifact listed');
        const a = page.items[0];
        assertEqual((await client.artifacts.get(a.id)).id, a.id, 'metadata read back');
        const c = await client.artifacts.content(a.id);
        assertEqual(c.data.byteLength, a.bytes, 'content size matches metadata');
        assertEqual(c.etag, '"' + a.sha256 + '"', 'ETag is the SHA-256');
        const part = await client.artifacts.download(a.id, { range: { start: 1, end: 4 } });
        assertEqual(part.status, 206, 'range on content');
        assertEqual(part.data.byteLength, Math.min(4, Math.max(0, a.bytes - 1)), 'four bytes');
        const u = await client.artifacts.downloadUrl(a.id);
        assertEqual(u.contentPath, '/v1.0/api/artifacts/' + a.id + '/content', 'content path');
        await expectError(client.artifacts.get(bogus('art')), 404, 'NotFound');
    });

    await run('briefings.generateAudio and downloadAudio', async () => {
        if (!S.briefingId) skip('no briefing');
        const m = await client.briefings.generateAudio(S.briefingId);
        onCleanup('audio', () => client.media.remove(m.id));
        const d = await poll(() => client.media.get(m.id), (x) => x.media.status === 'Ready' || x.media.status === 'Failed', 10 * 60000, 3000);
        assertEqual(d.media.status, 'Ready', 'audio ready');
        const audio = await client.briefings.downloadAudio(S.briefingId, { range: { start: 0, end: 99 } });
        assertEqual(audio.status, 206, 'audio range');
    }, { skip: withRun });

    await run('briefings.remove then get is a typed 404', async () => {
        if (!S.briefingId) skip('no briefing');
        await client.briefings.remove(S.briefingId);
        await expectError(client.briefings.get(S.briefingId), 404, 'NotFound');
        await expectError(client.briefings.downloadAudio(bogus('brf')), 404, 'NotFound');
    });

    await run('editions.remove then get is a typed 404', async () => {
        if (!S.editionId) skip('no edition');
        await client.editions.remove(S.editionId);
        await expectError(client.editions.get(S.editionId), 404, 'NotFound');
    });

    await run('templates.remove then get is a typed 404', async () => {
        if (!S.templateId) skip('no template');
        await client.templates.remove(S.templateId);
        await expectError(client.templates.get(S.templateId), 404, 'NotFound');
    });

    // ---- Media -----------------------------------------------------------------------------------------------------

    await run('media.list and missing episode', async () => {
        const page = await client.media.list({ pageSize: 5 });
        assertPage(page, 'media.list');
        await expectError(client.media.get(bogus('med')), 404, 'NotFound');
        await expectError(client.media.download(bogus('med')), 404, 'NotFound');
    });

    // ---- Feed tokens and public routes -----------------------------------------------------------------------------

    await run('feedTokens create, public feed, revoke, purge', async () => {
        const created = await client.feedTokens.create({ name: name('feed') });
        const id = created.token.id;
        onCleanup('feed token', async () => {
            await client.feedTokens.revoke(id).catch(() => null);
            await client.feedTokens.purge(id);
        });
        assert(created.secret && created.feedUrl, 'secret and URL returned once');
        assertEqual(created.token.tokenHash, 'redacted', 'hash redacted');
        const listed = await client.feedTokens.list();
        assert(listed.some((t) => t.id === id && t.tokenHash === 'redacted'), 'token listed, redacted');
        const xml = await client.publicApi.podcastFeed(created.secret);
        assert(xml.includes('<rss'), 'RSS feed');
        await client.feedTokens.revoke(id);
        const revoked = (await client.feedTokens.list()).find((t) => t.id === id);
        assert(revoked && revoked.revokedUtc, 'revoked');
        await expectError(client.publicApi.podcastFeed(created.secret), 404, 'NotFound');
        await client.feedTokens.purge(id);
        assert(!(await client.feedTokens.list()).some((t) => t.id === id), 'purged');
    });

    await run('publicApi.checkPromoCode and asset', async () => {
        const check = await client.publicApi.checkPromoCode('SDK-JS-NOPE-' + STAMP);
        assert(check && typeof check === 'object', 'check result');
        const logo = await client.publicApi.asset('logo.png');
        assertEqual(logo.contentType, 'image/png', 'png');
        assert(logo.data.byteLength > 100, 'image bytes');
        await expectError(client.publicApi.asset('nope.png'), 404, 'NotFound');
    });

    // ---- Email -----------------------------------------------------------------------------------------------------

    await run('email.test, getMessage, resend, removeMessage', async () => {
        const m = await client.email.test();
        onCleanup('email', () => client.email.removeMessage(m.id));
        assertEqual(m.template, 'Test', 'test template');
        const page = await client.email.listMessages({ template: 'Test', pageSize: 20 });
        assert(page.items.some((x) => x.id === m.id), 'listed in the outbox');
        assert(page.items.every((x) => x.renderedHtml === null || x.renderedHtml === undefined), 'list leaves bodies out');
        const sent = await poll(() => client.email.getMessage(m.id), (x) => x.status !== 'Queued' && x.status !== 'Sending', 30000, 1000);
        assert(typeof sent.renderedHtml === 'string' && sent.renderedHtml.length > 0, 'rendered body on read');
        if (sent.status === 'Queued' || sent.status === 'Sending') skip('email still sending after 30 s');
        const again = await client.email.resend(m.id);
        assertEqual(again.status, 'Queued', 'requeued');
        await poll(() => client.email.getMessage(m.id), (x) => x.status !== 'Queued' && x.status !== 'Sending', 30000, 1000);
        await client.email.removeMessage(m.id);
        await expectError(client.email.getMessage(m.id), 404, 'NotFound');
    });

    await run('email preferences get and put', async () => {
        const p = await client.email.getPreferences();
        assert(typeof p.runEmails === 'boolean' && Array.isArray(p.unsubscribedTopicIds), 'preferences shape');
        const same = await client.email.updatePreferences({ runEmails: p.runEmails, briefingEmails: p.briefingEmails, failureEmails: p.failureEmails });
        assertEqual(same.runEmails, p.runEmails, 'unchanged after round trip');
    });

    // ---- Analytics, usage, model calls -----------------------------------------------------------------------------

    await run('analytics reports', async () => {
        const from = new Date(Date.now() - 30 * 86400000);
        for (const r of ['relevance', 'accuracy', 'feedback', 'reasons', 'suggestions', 'quality']) {
            const report = await client.analytics[r]({ from, to: new Date() });
            assert(report && typeof report === 'object', r + ' report');
        }
        await expectError(client.analytics.relevance({ topicId: bogus('top') }), 404, 'NotFound');
    });

    await run('analytics overview, email, feedback byChannelDay', async () => {
        const o = await client.analytics.overview({ from: new Date(Date.now() - 30 * 86400000) });
        for (const k of ['activeTopics', 'runs', 'runsCompleted', 'findingsShown', 'briefingsReady', 'emailsSent', 'feedbackTotal', 'modelCalls', 'modelCostUsd']) {
            assert(typeof o[k] === 'number', 'overview.' + k + ' is a number');
        }
        assert(typeof o.fromUtc === 'string' && typeof o.toUtc === 'string', 'overview window');
        const e = await client.analytics.email();
        for (const k of ['total', 'sent', 'failed', 'pending', 'skipped', 'unsubscribes']) assert(typeof e[k] === 'number', 'email.' + k + ' is a number');
        assert(Array.isArray(e.byTemplate) && Array.isArray(e.byDay) && Array.isArray(e.byStatus), 'email breakdowns');
        const fb = await client.analytics.feedback();
        assert(Array.isArray(fb.byChannelDay), 'feedback.byChannelDay');
        assert(typeof (await client.analytics.report('overview')) === 'object', 'report by name');
    });

    await run('usage.me, report, timeseries', async () => {
        assert(typeof (await client.usage.me()) === 'object', 'me');
        assert(typeof (await client.usage.report({ groupBy: 'Model' })) === 'object', 'report');
        assert(typeof (await client.usage.timeseries({ bucketMinutes: 1440 })) === 'object', 'timeseries');
        await expectError(client.usage.report({ groupBy: 'Tenant' }), 400, 'BadRequest');
    });

    await run('llmCalls.list and get', async () => {
        const page = await client.llmCalls.list({ pageSize: 2 });
        assertPage(page, 'llmCalls.list');
        await expectError(client.llmCalls.get(bogus('llm')), 404, 'NotFound');
        if (page.items.length > 0) {
            const c = await client.llmCalls.get(page.items[0].id);
            assertEqual(c.id, page.items[0].id, 'call read back');
        }
    });

    // ---- Model endpoints and assignments ---------------------------------------------------------------------------

    await run('modelEndpoints CRUD, validate, models, test (unreachable fake)', async () => {
        const e = await client.modelEndpoints.create({ name: name('model'), provider: 'OpenAiCompatible', endpoint: 'http://127.0.0.1:9/v1', model: 'sdk-fake-model', supportsChat: true, timeoutMs: 2000, active: false });
        S.endpointId = e.id;
        onCleanup('model endpoint', () => client.modelEndpoints.remove(e.id));
        assertEqual((await client.modelEndpoints.get(e.id)).id, e.id, 'read back');
        const u = await client.modelEndpoints.update(e.id, { maxTokens: 512 });
        assertEqual(u.maxTokens, 512, 'updated');
        const page = await client.modelEndpoints.list({ provider: 'OpenAiCompatible', pageSize: 100 });
        assert(page.items.some((x) => x.id === e.id), 'listed');
        const v = await client.modelEndpoints.validate(e.id);
        assertEqual(v.reachable, false, 'fake endpoint is unreachable');
        const t = await client.modelEndpoints.test(e.id, { prompt: 'ping' });
        assertEqual(t.success, false, 'test fails on a fake endpoint');
        try {
            const models = await client.modelEndpoints.models(e.id);
            assert(Array.isArray(models), 'models array');
        } catch (err) {
            assert(err instanceof MyMorningNewsError && err.status === 502, 'models is a typed 502 when unreachable');
        }
        await expectError(client.modelEndpoints.create({ name: name('model-bad') }), 400, 'ValidationFailed');
    });

    await run('modelAssignments upsert, list, remove (topic override)', async () => {
        if (!S.topic || !S.endpointId) skip('no topic or endpoint');
        const a = await client.modelAssignments.upsert({ task: 'Synthesis', topicId: S.topic.id, modelEndpointId: S.endpointId });
        onCleanup('model assignment', () => client.modelAssignments.remove(a.id));
        assertEqual(a.topicId, S.topic.id, 'topic override');
        const page = await client.modelAssignments.list({ topicId: S.topic.id });
        assert(page.items.some((x) => x.id === a.id), 'listed');
        const again = await client.modelAssignments.upsert({ task: 'Synthesis', topicId: S.topic.id, modelEndpointId: S.endpointId, modelOverride: 'sdk-other' });
        assertEqual(again.id, a.id, 'upsert updates in place');
        await client.modelAssignments.remove(a.id);
        const tenantDefault = (await client.modelAssignments.list({ pageSize: 100 })).items.find((x) => !x.topicId);
        if (tenantDefault) await expectError(client.modelAssignments.remove(tenantDefault.id), 409, 'Conflict');
    });

    await run('modelEndpoints.remove then get is a typed 404', async () => {
        if (!S.endpointId) skip('no endpoint');
        await client.modelEndpoints.remove(S.endpointId);
        await expectError(client.modelEndpoints.get(S.endpointId), 404, 'NotFound');
    });

    // ---- Search providers ------------------------------------------------------------------------------------------

    await run('searchProviders CRUD, test, topic providers', async () => {
        const p = await client.searchProviders.create({ name: name('search'), kind: 'Rss', isDefault: false, maxResults: 5, active: false });
        onCleanup('search provider', () => client.searchProviders.remove(p.id));
        assertEqual((await client.searchProviders.get(p.id)).id, p.id, 'read back');
        const u = await client.searchProviders.update(p.id, { maxResults: 7 });
        assertEqual(u.maxResults, 7, 'updated');
        assert((await client.searchProviders.list({ kind: 'Rss', pageSize: 100 })).items.some((x) => x.id === p.id), 'listed');
        const outcome = await client.searchProviders.test(p.id);
        assert(outcome && typeof outcome === 'object', 'test outcome');
        if (S.topic) {
            const set = await client.topics.setSearchProviders(S.topic.id, [{ searchProviderId: p.id, enabled: true, maxResults: 3 }]);
            assert(Array.isArray(set), 'topic providers set');
            await client.topics.setSearchProviders(S.topic.id, []);
            await expectError(client.topics.setSearchProviders(S.topic.id, [{ searchProviderId: bogus('spv') }]), 400, 'ValidationFailed');
        }
        await client.searchProviders.remove(p.id);
        await expectError(client.searchProviders.get(p.id), 404, 'NotFound');
        await expectError(client.searchProviders.create({ name: name('search-bad'), kind: 'HostedModelWebSearch' }), 400, 'ValidationFailed');
    });

    // ---- Voices ----------------------------------------------------------------------------------------------------

    await run('voices CRUD and test', async () => {
        const existing = (await client.voices.list({ pageSize: 100 })).items.find((v) => v.provider === 'OpenAiCompatibleSpeech' && v.endpoint);
        const v = await client.voices.create({ name: name('voice'), provider: 'OpenAiCompatibleSpeech', endpoint: existing?.endpoint ?? undefined, model: existing?.model ?? 'kokoro', voice: existing?.voice ?? 'af_heart', speed: 1, isDefault: false });
        onCleanup('voice', () => client.voices.remove(v.id));
        assertEqual(v.isDefault, false, 'not default');
        assertEqual((await client.voices.get(v.id)).id, v.id, 'read back');
        const u = await client.voices.update(v.id, { speed: 1.1 });
        assert(Math.abs(u.speed - 1.1) < 0.001, 'speed updated');
        assert((await client.voices.list({ pageSize: 100 })).items.some((x) => x.id === v.id), 'listed');
        try {
            const audio = await client.voices.test(v.id, { text: 'SDK harness test.' });
            assert(audio.data.byteLength > 0, 'audio bytes');
            assert(String(audio.contentType).startsWith('audio/'), 'audio content type');
        } catch (err) {
            if (err instanceof MyMorningNewsError && err.status === 502) verbose('voice test: TTS endpoint failed (' + err.message + ')');
            else throw err;
        }
        await client.voices.remove(v.id);
        await expectError(client.voices.get(v.id), 404, 'NotFound');
        await expectError(client.voices.create({ name: name('voice-bad') }), 400, 'ValidationFailed');
    });

    // ---- Vault -----------------------------------------------------------------------------------------------------

    await run('vault CRUD and usage (no secret returned)', async () => {
        const c = await client.vault.create({ name: name('vault'), kind: 'ApiKey', provider: 'Custom', secret: 'sdk-js-placeholder-' + STAMP, description: 'SDK harness' });
        onCleanup('vault credential', () => client.vault.remove(c.id));
        assert(!('secret' in c), 'no secret in the response');
        assert(typeof c.secretPreview === 'string', 'masked preview');
        assertEqual((await client.vault.get(c.id)).id, c.id, 'read back');
        const u = await client.vault.update(c.id, { description: 'SDK harness, updated' });
        assertEqual(u.description, 'SDK harness, updated', 'updated');
        assert((await client.vault.list({ kind: 'ApiKey', pageSize: 100 })).items.some((x) => x.id === c.id), 'listed');
        assert(Array.isArray(await client.vault.usage(c.id)), 'usage array');
        await client.vault.remove(c.id);
        await expectError(client.vault.get(c.id), 404, 'NotFound');
        const err = await expectError(client.vault.create({ name: name('vault-bad'), kind: 'ApiKey', provider: 'Custom' }), 400, 'ValidationFailed');
        assert(err.details && err.details.secret, 'details name the missing field');
    });

    // ---- Devices ---------------------------------------------------------------------------------------------------

    await run('devices register, re-register, list, remove', async () => {
        const token = 'ExponentPushToken[sdkjs' + STAMP + ']';
        const d = await client.devices.register({ expoPushToken: token, platform: 'ios', name: name('device'), locale: 'en-US' });
        onCleanup('device', () => client.devices.remove(d.id));
        const again = await client.devices.register({ expoPushToken: token, platform: 'ios', name: name('device-renamed') });
        assertEqual(again.id, d.id, 'same device on re-register');
        assert((await client.devices.list()).some((x) => x.id === d.id), 'listed');
        await client.devices.remove(d.id);
        assert(!(await client.devices.list()).some((x) => x.id === d.id), 'removed');
        await expectError(client.devices.register({ expoPushToken: token, platform: 'windows' }), 400, 'BadRequest');
        await expectError(client.devices.remove(d.id), 404, 'NotFound');
    });

    // ---- Account ---------------------------------------------------------------------------------------------------

    await run('account.plan and redeem a bad code', async () => {
        const p = await client.account.plan();
        assert(p.plan && typeof p.plan.key === 'string', 'plan key');
        assert(p.features && typeof p.features === 'object', 'features');
        const err = await expectError(client.account.redeem('SDK-JS-NOPE-' + STAMP), 409, 'PromoCodeInvalid');
        assert(err.details && err.details.reason, 'reason in details');
    });

    // ---- Topic delete and logout -----------------------------------------------------------------------------------

    await run('topics.remove then get is a typed 404', async () => {
        if (!S.topic) skip('no topic');
        await client.topics.remove(S.topic.id);
        await expectError(client.topics.get(S.topic.id), 404, 'NotFound');
    });

    await run('login reports a tenant choice when needed', async () => {
        const c = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        try {
            await c.login(OPTS.email, OPTS.password);
            await c.logout();
            skip('the user belongs to one tenant');
        } catch (err) {
            if (err instanceof SkipCase) throw err;
            assert(err instanceof TenantSelectionRequiredError, 'TenantSelectionRequiredError');
            assert(err.tenants.length > 1, 'tenants listed');
        }
    });

    await run('logout revokes the session', async () => {
        const c = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        await c.login(OPTS.email, OPTS.password, { tenantId: OPTS.tenant });
        const revoked = c.token;
        await c.logout();
        assertEqual(c.token, null, 'token cleared');
        const stale = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, token: revoked });
        await expectError(stale.tokenDetails(), 401, 'Unauthorized');
    });

    S.client = client;
    return S;
}

// ---------------------------------------------------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------------------------------------------------

const started = performance.now();
let state;
try {
    state = await main();
} catch (err) {
    log('FAIL  harness aborted: ' + redact(describe(err)));
    results.push({ name: 'harness', status: 'FAIL', ms: 0 });
}

const cleanupFailures = await runCleanups();
if (state?.client?.token) await state.client.logout().catch(() => null);

const passed = results.filter((r) => r.status === 'PASS').length;
const failed = results.filter((r) => r.status === 'FAIL').length;
const skipped = results.filter((r) => r.status === 'SKIP').length;
const totalMs = Math.round(performance.now() - started);
log('');
log('Summary: ' + results.length + ' cases, ' + passed + ' passed, ' + failed + ' failed, ' + skipped + ' skipped in ' + totalMs + ' ms' + (cleanupFailures ? ', ' + cleanupFailures + ' cleanup warnings' : ''));
if (failed > 0) {
    log('Failed:');
    for (const r of results.filter((x) => x.status === 'FAIL')) log('  - ' + r.name);
}
log(failed === 0 ? 'RESULT: PASS' : 'RESULT: FAIL');
process.exit(failed === 0 ? 0 : 1);
