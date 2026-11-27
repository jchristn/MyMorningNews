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

import { createServer } from 'node:http';
import {
    MyMorningNewsClient, MyMorningNewsError, TenantSelectionRequiredError, CapReachedError, EmailNotVerifiedError, SetupRequiredError, RunsResource, paginate,
    SDK_VERSION, DEFAULT_BASE_URL, signRequest, stringToSign, SIGNATURE_ALGORITHM, parseServerSentEvents
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
    assert(page && Array.isArray(page.objects), what + ' returns a page with items');
    assert(typeof page.pageNumber === 'number' && typeof page.pageSize === 'number', what + ' has pageNumber and pageSize');
    assert(typeof page.totalRecords === 'number' && typeof page.totalPages === 'number', what + ' has totalCount and totalPages');
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

/**
 * Start a minimal Streamable HTTP MCP server on 127.0.0.1 for the registry cases. It answers initialize, ping, and
 * tools/list with JSON, accepts notifications with 202, and lists two tools it never runs.
 * @returns {Promise<{ url: string, toolNames: string[], requests: () => number, close: () => Promise<void> }>} Server.
 */
async function startFakeMcpServer() {
    const toolNames = ['harness_echo', 'harness_time'];
    let count = 0;
    const server = createServer((req, res) => {
        count++;
        if (req.method === 'DELETE') {
            res.writeHead(200).end();
            return;
        }
        if (req.method !== 'POST') {
            res.writeHead(405).end();
            return;
        }
        let raw = '';
        req.setEncoding('utf8');
        req.on('data', (chunk) => { raw += chunk; });
        req.on('end', () => {
            let msg;
            try {
                msg = JSON.parse(raw);
            } catch {
                res.writeHead(400).end();
                return;
            }
            if (msg.id === undefined || msg.id === null) {
                res.writeHead(202).end();
                return;
            }
            const reply = { jsonrpc: '2.0', id: msg.id };
            const headers = { 'Content-Type': 'application/json' };
            if (msg.method === 'initialize') {
                reply.result = {
                    protocolVersion: msg.params?.protocolVersion ?? '2025-11-25',
                    capabilities: { tools: {} },
                    serverInfo: { name: 'sdk-harness-fake', version: '1.0.0' }
                };
                headers['Mcp-Session-Id'] = 'js-' + STAMP;
            } else if (msg.method === 'tools/list') {
                reply.result = { tools: toolNames.map((n) => ({ name: n, description: 'Harness tool ' + n + '; never called.', inputSchema: { type: 'object', properties: {} } })) };
            } else if (msg.method === 'ping') {
                reply.result = {};
            } else {
                reply.error = { code: -32601, message: 'Method not found: ' + msg.method };
            }
            res.writeHead(200, headers).end(JSON.stringify(reply));
        });
    });
    await new Promise((resolve, reject) => {
        server.once('error', reject);
        server.listen(0, '127.0.0.1', resolve);
    });
    const { port } = server.address();
    return {
        url: 'http://127.0.0.1:' + port + '/mcp',
        toolNames,
        requests: () => count,
        close: () => new Promise((resolve) => {
            server.closeAllConnections?.();
            server.close(() => resolve());
        })
    };
}

/**
 * Read the newest link token of a template from an outbox, through a client that can read that tenant's email.
 * @param {MyMorningNewsClient} outbox Client.
 * @param {string} template Email template.
 * @returns {Promise<string>} Token.
 */
async function linkToken(outbox, template) {
    const page = await outbox.email.list({ template, pageSize: 20 });
    const latest = [...page.objects].sort((a, b) => String(b.createdUtc).localeCompare(String(a.createdUtc)))[0];
    assert(latest, 'a ' + template + ' email is in the outbox');
    const full = await outbox.email.get(latest.id);
    const m = /token=([A-Za-z0-9%_\-.~]+)/.exec(full.renderedText ?? '');
    assert(m, 'the ' + template + ' email carries a link token');
    return decodeURIComponent(m[1]);
}

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
        assertEqual(ids.length, first.totalRecords, 'items yielded across pages');
        assertEqual(new Set(ids).size, ids.length, 'no duplicates across pages');
        let limited = 0;
        for await (const _ of client.paginate(client.prompts.list, { pageSize: 2 }, { maxItems: 3 })) limited++;
        assertEqual(limited, Math.min(3, first.totalRecords), 'maxItems stops early');
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
                return new Response('{"objects":[],"pageNumber":1,"pageSize":1,"totalRecords":0,"totalPages":0,"totalMs":0}', { status: 200, headers: { 'content-type': 'application/json' } });
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
        assert(page.objects.some((t) => t.id === S.topic.id), 'topic listed');
        const drafts = await client.topics.list({ status: 'Draft', mine: true, pageSize: 100 });
        assert(drafts.objects.every((t) => t.status === 'Draft'), 'status filter applied');
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
        const list = (await client.topics.listDimensions(S.topic.id)).objects;
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
        const byKind = (await client.topics.listConstraints(S.topic.id, { kind: 'IncludeKeyword' })).objects;
        assert(byKind.some((x) => x.id === c.id), 'constraint listed by kind');
        const other = (await client.topics.listConstraints(S.topic.id, { kind: 'BlockedDomain' })).objects;
        assert(!other.some((x) => x.id === c.id), 'kind filter excludes it');
        const u = await client.topics.updateConstraint(S.topic.id, c.id, { value: 'sdk-keyword-2' });
        assertEqual(u.value, 'sdk-keyword-2', 'value updated');
        await client.topics.removeConstraint(S.topic.id, c.id);
        assert(!((await client.topics.listConstraints(S.topic.id)).objects).some((x) => x.id === c.id), 'constraint removed');
        await expectError(client.topics.addConstraint(S.topic.id, { kind: 'FeedUrl', value: 'not a url' }), 400, 'ValidationFailed');
    });

    await run('topics recipients: add, list, remove', async () => {
        if (!S.topic) skip('no topic');
        const r = await client.topics.addRecipient(S.topic.id, { email: 'sdk-js-' + STAMP + '@example.test' });
        assert(r.id, 'recipient ID');
        assert(((await client.topics.listRecipients(S.topic.id)).objects).some((x) => x.id === r.id), 'recipient listed');
        await client.topics.removeRecipient(S.topic.id, r.id);
        assert(!((await client.topics.listRecipients(S.topic.id)).objects).some((x) => x.id === r.id), 'recipient removed');
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
        const p = (await client.topics.getSearchProviders(S.topic.id)).objects;
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
            assert(!page.objects.some((t) => t.id === S.topic.id), 'topic not listed for demo');
            const upd = await expectError(demo.topics.update(S.topic.id, { description: 'hijack' }));
            assert(upd.status === 404 || upd.status === 403, 'update is 404 or 403');
        } finally {
            await demo.logout().catch(() => null);
        }
    });

    // ---- Refinement ------------------------------------------------------------------------------------------------

    await run('refinement.start, get, list, abandon', async () => {
        let state;
        try {
            state = await client.refinement.start({ seedText: name('refinement') + ' covering open source vector databases', goal: 'SDK harness' });
        } catch (err) {
            if (!(err instanceof SetupRequiredError)) throw err;
            assert(typeof err.check === 'string' && err.check.length > 0, 'SetupRequiredError names the setup check');
            S.setupRequired = err.check;
            skip('the workspace is not set up for model work (409 SetupRequired, check ' + err.check + '); store a model key with setup.setModelKey first');
        }
        assert(state.session.id, 'session ID');
        assertEqual(state.topic.status, 'Draft', 'draft topic');
        onCleanup('refinement topic', () => client.topics.remove(state.topic.id));
        onCleanup('refinement session', () => client.refinement.abandon(state.session.id));
        const again = await client.refinement.get(state.session.id);
        assertEqual(again.session.id, state.session.id, 'read back');
        const page = await client.refinement.list({ topicId: state.topic.id });
        assertPage(page, 'refinement.list');
        assert(page.objects.some((s) => s.id === state.session.id), 'session listed');
        await client.refinement.abandon(state.session.id);
        await expectError(client.refinement.get(state.session.id), 404, 'NotFound');
        await expectError(client.topics.get(state.topic.id), 404, 'NotFound');
    });

    await run('refinement.start without seed text is a typed 400', async () => {
        if (S.setupRequired) skip('the workspace is not set up for model work (409 SetupRequired, check ' + S.setupRequired + ')');
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
        assert(page.objects.some((x) => x.id === s.id), 'schedule listed');
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
        const r = page.objects.find((x) => RunsResource.isTerminal(x.status));
        if (!r) skip('no finished run to read');
        assertEqual((await client.runs.get(r.id)).id, r.id, 'run read back');
        assert(Array.isArray((await client.runs.stages(r.id)).objects), 'stages array');
        assert(Array.isArray((await client.runs.queries(r.id)).objects), 'queries array');
        assert(Array.isArray((await client.runs.results(r.id)).objects), 'results array');
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
        for (const r of page.objects) {
            const calls = await client.runs.llmCalls(r.id, { pageSize: 50 });
            assertPage(calls, 'runs.llmCalls');
            assert(calls.objects.every((c) => c.runId === r.id), 'calls belong to the run');
            for (let i = 1; i < calls.objects.length; i++) assert(calls.objects[i - 1].createdUtc <= calls.objects[i].createdUtc, 'oldest first');
            if (calls.objects.length > 1) {
                const desc = await client.runs.llmCalls(r.id, { pageSize: 50, sortDirection: 'Descending' });
                assert(desc.objects[0].createdUtc >= desc.objects[desc.objects.length - 1].createdUtc, 'sortDirection honoured');
                checked++;
                break;
            }
        }
        if (page.objects.length === 0) skip('no runs');
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

    await run('runs.costs and cost filters (offline, fake fetch)', async () => {
        const seen = [];
        const report = {
            runId: 'run_fake', topicId: 'top_fake', status: 'Completed', currency: 'USD',
            totals: { estimatedCostUsd: 0.4215, llmCallCount: 12, unpricedCallCount: 1, inputTokens: 90000, outputTokens: 8000 },
            costPerNewFindingUsd: 0.0527,
            stages: [{ runStageId: 'stg_a', stage: 'Plan', status: 'Succeeded', sortOrder: 0, estimatedCostUsd: 0.03, llmCallCount: 1, shareOfCost: 0.071 }],
            unattributed: null,
            byModel: [{ provider: 'Anthropic', model: 'claude-opus-5-5', estimatedCostUsd: 0.4215, llmCallCount: 12 }],
            byTask: [{ task: 'Synthesis', estimatedCostUsd: 0.2 }]
        };
        const fake = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1', token: 't',
            fetch: async (url, init) => {
                seen.push({ url, init });
                const body = url.includes('/costs') ? report : { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 };
                return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
            }
        });
        const got = await fake.runs.costs('run_fake');
        assert(seen[0].url.endsWith('/v1.0/api/runs/run_fake/costs'), 'costs path');
        assertEqual(seen[0].init.method, 'GET', 'costs is a GET');
        assertEqual(got.totals.estimatedCostUsd, 0.4215, 'total cost');
        assertEqual(got.stages[0].shareOfCost, 0.071, 'stage share');
        assertEqual(got.byModel[0].model, 'claude-opus-5-5', 'model row');
        await fake.llmCalls.list({ runId: 'run_fake', runStageId: 'stg_a' });
        assertEqual(new URL(seen[1].url).searchParams.get('runStageId'), 'stg_a', 'runStageId filter sent');
        await fake.runs.list({ sortBy: 'estimatedCostUsd', sortDirection: 'Descending' });
        assertEqual(new URL(seen[2].url).searchParams.get('sortBy'), 'estimatedCostUsd', 'sortBy sent');
        let threw = false;
        try {
            await fake.runs.costs('');
        } catch {
            threw = true;
        }
        assert(threw, 'costs rejects an empty id');
    });

    await run('runs.costs (live: totals, stages, llm calls by stage)', async () => {
        await expectError(client.runs.costs(bogus('run')), 404, 'NotFound');
        const byCost = await client.runs.list({ pageSize: 5, sortBy: 'estimatedCostUsd', sortDirection: 'Descending' });
        assertPage(byCost, 'runs.list sorted by cost');
        const priced = byCost.objects.filter((r) => r.estimatedCostUsd !== null && r.estimatedCostUsd !== undefined);
        for (let i = 1; i < priced.length; i++) assert(priced[i - 1].estimatedCostUsd >= priced[i].estimatedCostUsd, 'most expensive first');
        const r = (await client.runs.list({ pageSize: 25 })).objects.find((x) => RunsResource.isTerminal(x.status));
        if (!r) skip('no finished run to cost');
        const costs = await client.runs.costs(r.id);
        assertEqual(costs.runId, r.id, 'report run');
        assertEqual(costs.currency, 'USD', 'currency');
        const stages = (await client.runs.stages(r.id)).objects;
        assertEqual(costs.stages.length, stages.length, 'a cost row per stage');
        const stageCalls = costs.stages.reduce((n, s) => n + s.llmCallCount, 0) + (costs.unattributed ? costs.unattributed.llmCallCount : 0);
        assertEqual(stageCalls, costs.totals.llmCallCount, 'stage and unattributed calls add up to the total');
        const costed = stages.find((s) => s.llmCallCount > 0);
        if (costed) {
            const calls = await client.llmCalls.list({ runStageId: costed.id, pageSize: 100 });
            assert(calls.objects.every((c) => c.runStageId === costed.id), 'calls belong to the stage');
        } else {
            verbose('run ' + r.id + ' has no stage with model calls');
        }
    });

    await run('SSE parser: canned stream (offline)', async () => {
        const enc = new TextEncoder();
        const stream = '\uFEFF: comment line\r\n'
            + 'id: 1\r\nevent: run\r\ndata: {"id":"run_1","status":"Running"}\r\n\r\n'
            + 'event: stage\ndata: {"stage":"Search",\ndata: "status":"Running"}\nid: 2\n\n'
            + ':keep-alive\n\n'
            + 'event: ping\rdata: {}\r\r'
            + 'event: noData\n\n'
            + 'data:plain text no space\n\n'
            + 'event: note\ndata: caf\u00e9 \u2713\n\n'
            + 'event: done\nid: 3\ndata: {"id":"run_1","status":"Completed"}';
        const bytes = enc.encode(stream);
        const sizes = [1, 2, 3, 5, 7, 11, 13];
        const chunks = [];
        for (let i = 0, k = 0; i < bytes.length; k++) {
            const n = sizes[k % sizes.length];
            chunks.push(bytes.slice(i, i + n));
            i += n;
        }
        const crSplit = [];
        const crIndex = stream.indexOf('\r\n');
        crSplit.push(stream.slice(0, crIndex + 1), stream.slice(crIndex + 1));
        async function* asAsync(list) {
            for (const c of list) yield c;
        }
        for (const [label, input] of [['byte chunks', asAsync(chunks)], ['one string', [stream]], ['CR LF split', crSplit]]) {
            const events = [];
            for await (const ev of parseServerSentEvents(input)) events.push(ev);
            assertEqual(events.map((e) => e.event).join(','), 'run,stage,ping,message,note,done', label + ': event order');
            assertEqual(events[0].id, '1', label + ': id');
            assertEqual(events[0].data.status, 'Running', label + ': JSON data');
            assertEqual(events[1].raw, '{"stage":"Search",\n"status":"Running"}', label + ': multi-line data joined with a line feed');
            assertEqual(events[1].data.stage, 'Search', label + ': multi-line JSON parsed');
            assertEqual(events[1].id, '2', label + ': id after data');
            assertEqual(events[2].id, '2', label + ': id carries over');
            assertEqual(events[3].data, 'plain text no space', label + ': non-JSON data stays text');
            assertEqual(events[4].data, 'caf\u00e9 \u2713', label + ': UTF-8 split across chunks');
            assertEqual(events[5].data.status, 'Completed', label + ': last event without a trailing blank line');
        }
    });

    await run('runs.events, follow (offline, fake stream)', async () => {
        const sse = (text, chunkSize = 4) => new ReadableStream({
            start(controller) {
                const b = new TextEncoder().encode(text);
                for (let i = 0; i < b.length; i += chunkSize) controller.enqueue(b.slice(i, i + chunkSize));
                controller.close();
            }
        });
        const seen = [];
        let call = 0;
        const fake = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1', token: 'test-token',
            fetch: async (url, init) => {
                seen.push({ url, init });
                call++;
                if (url.includes('/runs/run_missing/')) return new Response('{"error":"NotFound","message":"Run was not found.","requestId":"rqi_x"}', { status: 404, headers: { 'content-type': 'application/json' } });
                if (url.includes('/runs/run_json/')) return new Response('{}', { status: 200, headers: { 'content-type': 'application/json', 'x-request-id': 'rqi_fake' } });
                const body = call === 1 || url.includes('run_one')
                    ? 'id: 1\nevent: stage\ndata: {"stage":"Plan","status":"Completed"}\n\nid: 2\nevent: run\ndata: {"id":"run_a","status":"Running"}\n\nevent: timeout\ndata: {"reconnect":true}\n\n'
                    : 'event: ping\ndata: {}\n\nid: 3\nevent: done\ndata: {"id":"run_a","status":"Completed"}\n\n';
                return new Response(sse(body), { status: 200, headers: { 'content-type': 'text/event-stream' } });
            }
        });
        const evs = [];
        for await (const ev of fake.runs.events('run_one')) evs.push(ev.event);
        assertEqual(evs.join(','), 'stage,run,timeout', 'events streamed');
        assertEqual(seen[0].init.headers.Accept, 'text/event-stream', 'Accept header');
        assertEqual(seen[0].init.headers.Authorization, 'Bearer test-token', 'bearer token sent');
        assert(seen[0].url.endsWith('/v1.0/api/runs/run_one/events'), 'events path');
        call = 0;
        const followed = [];
        const final = await fake.runs.follow('run_a', (ev) => followed.push(ev.event));
        assertEqual(final.status, 'Completed', 'follow returns the final run');
        assertEqual(followed.join(','), 'stage,run,timeout,ping,done', 'follow reconnects after timeout');
        await expectError((async () => {
            for await (const _ of fake.runs.events('run_missing')) { /* none */ }
        })(), 404, 'NotFound');
        await expectError((async () => {
            for await (const _ of fake.runs.events('run_json')) { /* none */ }
        })(), 200, 'NotEventStream');
        let brokeEarly = 0;
        for await (const _ of fake.runs.events('run_one')) {
            brokeEarly++;
            break;
        }
        assertEqual(brokeEarly, 1, 'breaking out of the loop stops the stream');
        const signedSeen = [];
        const signed = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1', accessKey: 'ak_test', secretKey: 'secret_test', tenantId: 'default', signedRequests: true,
            fetch: async (url, init) => {
                signedSeen.push({ url, init });
                return new Response(sse('event: done\ndata: {"id":"run_s","status":"Failed"}\n\n'), { status: 200, headers: { 'content-type': 'text/event-stream' } });
            }
        });
        for await (const _ of signed.runs.events('run_s')) { /* drain */ }
        const h = signedSeen[0].init.headers;
        assert(!('x-secret-key' in h), 'signed stream sends no secret');
        const u = new URL(signedSeen[0].url);
        assertEqual(h['x-signature'], signRequest({ method: 'GET', pathAndQuery: u.pathname + u.search, tenantId: 'default', date: h['x-date'], nonce: h['x-nonce'], body: '', secretKey: 'secret_test' }), 'stream request signed');
        const controller = new AbortController();
        const slow = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1', token: 't',
            fetch: async (url, init) => new Response(new ReadableStream({
                start(c) {
                    c.enqueue(new TextEncoder().encode('event: run\ndata: {"status":"Running"}\n\n'));
                    init.signal.addEventListener('abort', () => c.error(Object.assign(new Error('aborted'), { name: 'AbortError' })));
                }
            }), { status: 200, headers: { 'content-type': 'text/event-stream' } })
        });
        const err = await expectError((async () => {
            for await (const _ of slow.runs.events('run_x', { signal: controller.signal })) controller.abort();
        })(), 0, 'Aborted');
        assert(err.path.endsWith('/events'), 'abort error names the path');
    });

    await run('runs.events and retry (live 404, 409, 400, replay)', async () => {
        let probe;
        try {
            for await (const _ of client.runs.events(bogus('run'))) { /* none expected */ }
        } catch (err) {
            probe = err;
        }
        assert(probe instanceof MyMorningNewsError, 'typed error for an unknown run');
        if (probe.code === 'RouteNotFound') skip('server has no run events route yet (rebuild pending)');
        assertEqual(probe.status, 404, 'events on an unknown run is 404');
        assertEqual(probe.code, 'NotFound', 'NotFound code');
        await expectError(client.runs.retry(bogus('run')), 404, 'NotFound');
        await expectError(client.runs.retry(bogus('run'), { fromStage: 'Synthesize' }), 404, 'NotFound');
        const all = (await client.runs.list({ pageSize: 100 })).objects;
        const unfinished = all.find((r) => !RunsResource.isTerminal(r.status));
        if (unfinished) await expectError(client.runs.retry(unfinished.id), 409, 'Conflict');
        else verbose('no unfinished run for the live 409 check');
        const finished = all.find((r) => RunsResource.isTerminal(r.status));
        if (!finished) skip('no finished run to replay or to check 400 on');
        const topicExists = await client.topics.get(finished.topicId).then(() => true, () => false);
        if (topicExists) await expectError(client.runs.retry(finished.id, { fromStage: 'NotAStage' }), 400, 'ValidationFailed');
        else await expectError(client.runs.retry(finished.id, { fromStage: 'NotAStage' }), 409, 'Conflict');
        const events = [];
        const controller = new AbortController();
        const guard = setTimeout(() => controller.abort(), 20000);
        try {
            for await (const ev of client.runs.events(finished.id, { signal: controller.signal })) events.push(ev);
        } finally {
            clearTimeout(guard);
        }
        assert(events.length >= 1, 'replay sent events');
        const last = events[events.length - 1];
        assertEqual(last.event, 'done', 'a finished run ends with done');
        assertEqual(last.data.id, finished.id, 'done carries the run');
        assert(events.slice(0, -1).every((e) => e.event === 'stage'), 'stages come before done');
        const stages = (await client.runs.stages(finished.id)).objects;
        assertEqual(events.length - 1, stages.length, 'one stage event per stage');
        const final = await client.runs.follow(finished.id);
        assertEqual(final.id, finished.id, 'follow returns the finished run');
    });

    await run('topics.run (dry run) and runs.wait', async () => {
        const t = await client.topics.create({ name: name('run-topic'), brief: 'Track notable releases of open source vector databases this week.', status: 'Active' });
        onCleanup('run topic', () => client.topics.remove(t.id));
        await client.topics.addDimension(t.id, { name: 'Releases', description: 'New versions and features' });
        const queued = await client.topics.dryRun(t.id);
        assert(queued.isDryRun, 'dry run');
        const seenEvents = [];
        const followed = await client.runs.follow(queued.id, (ev) => seenEvents.push(ev.event)).catch((err) => {
            if (err instanceof MyMorningNewsError && err.code === 'RouteNotFound') return null;
            throw err;
        });
        if (followed) assert(seenEvents.includes('done'), 'followed to done');
        const done = await client.runs.wait(queued.id, { timeoutMs: 15 * 60000, intervalMs: 5000 });
        assert(RunsResource.isTerminal(done.status), 'terminal');
        assert(Array.isArray((await client.runs.stages(done.id)).objects), 'stages');
    }, { skip: withRun });

    // ---- Findings, sources, predictions, suggestions ---------------------------------------------------------------

    await run('findings.list and get', async () => {
        const page = await client.findings.list({ pageSize: 5, minScore: 0, includeDryRun: true });
        assertPage(page, 'findings.list');
        await expectError(client.findings.get(bogus('fnd')), 404, 'NotFound');
        if (page.objects.length > 0) {
            const d = await client.findings.get(page.objects[0].id);
            assertEqual(d.finding.id, page.objects[0].id, 'finding read back');
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
        assert(hidden.objects.every((f) => f.pinned === true), 'pinned filter applied');
    });

    await run('findings pin and hide on a real finding', async () => {
        const page = await client.findings.list({ includeBelowThreshold: true, includeDryRun: true, pageSize: 1 });
        if (page.objects.length === 0) skip('no findings to edit');
        const f = page.objects[0];
        const was = { pinned: Boolean(f.pinned), hidden: Boolean(f.hidden) };
        try {
            await expectError(client.findings.update(f.id, {}), 400, 'ValidationFailed');
            assertEqual((await client.findings.pin(f.id)).pinned, true, 'pinned');
            assertEqual((await client.findings.unpin(f.id)).pinned, false, 'unpinned');
            assertEqual((await client.findings.hide(f.id)).hidden, true, 'hidden');
            const def = await client.findings.list({ runId: f.runId, includeBelowThreshold: true, includeDryRun: true, pageSize: 200 });
            assert(!def.objects.some((x) => x.id === f.id), 'hidden finding left out by default');
            const all = await client.findings.list({ runId: f.runId, includeBelowThreshold: true, includeDryRun: true, includeHidden: true, pageSize: 200 });
            assert(all.objects.some((x) => x.id === f.id), 'includeHidden lists it');
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
        if (page.objects.length > 0) assertEqual((await client.sources.get(page.objects[0].id)).id, page.objects[0].id, 'source read back');
    });

    await run('predictions.list, get, resolve, reviews', async () => {
        const page = await client.predictions.list({ pageSize: 5, status: 'Open' });
        assertPage(page, 'predictions.list');
        await expectError(client.predictions.get(bogus('prd')), 404, 'NotFound');
        await expectError(client.predictions.resolve(bogus('prd'), { status: 'Accurate' }), 404, 'NotFound');
        await expectError(client.predictions.acceptReview(bogus('prv')), 404, 'NotFound');
        await expectError(client.predictions.rejectReview(bogus('prv')), 404, 'NotFound');
        if (page.objects.length > 0) assertEqual((await client.predictions.get(page.objects[0].id)).prediction.id, page.objects[0].id, 'prediction read back');
    });

    await run('suggestions.list, get, setStatus', async () => {
        const page = await client.suggestions.list({ pageSize: 5 });
        assertPage(page, 'suggestions.list');
        await expectError(client.suggestions.get(bogus('sug')), 404, 'NotFound');
        await expectError(client.suggestions.setStatus(bogus('sug'), 'Dismissed'), 404, 'NotFound');
        if (page.objects.length > 0) assertEqual((await client.suggestions.get(page.objects[0].id)).id, page.objects[0].id, 'suggestion read back');
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
        assert(page.objects.some((x) => x.id === p.id), 'listed by topic');
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
        assert(page.objects.some((x) => x.id === f.id), 'feedback listed');
        const u = await client.feedback.update(f.id, { reason: 'SDK harness reason, edited' });
        assertEqual(u.reason, 'SDK harness reason, edited', 'reason edited');
        await client.feedback.remove(f.id);
        const after = await client.feedback.list({ targetId: target.id, includeHistory: true });
        assert(!after.objects.some((x) => x.id === f.id), 'feedback retracted');
        await expectError(client.feedback.update(f.id, { reason: 'gone' }), 404, 'NotFound');
    });

    await run('feedback channel Mcp', async () => {
        const f = await client.feedback.submit({ targetType: 'Domain', targetId: 'sdk-js-mcp-' + STAMP + '.example.test', action: 'not-relevant', channel: 'Mcp' });
        onCleanup('mcp feedback', () => client.feedback.remove(f.id));
        assertEqual(f.channel, 'Mcp', 'channel saved');
        const page = await client.feedback.list({ channel: 'Mcp', targetId: f.targetId });
        assert(page.objects.some((x) => x.id === f.id), 'listed by channel');
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
        const versions = (await client.templates.versions(id)).objects;
        assert(Array.isArray(versions) && versions.length >= 2, 'two versions');
        const rolled = await client.templates.rollback(id, v1);
        assert(!rolled.content.includes('Keep it brief.'), 'rolled back');
        const exported = await client.templates.export(id);
        assert(String(exported.contentType).startsWith('text/markdown'), 'markdown content type');
        assert(exported.fileName && exported.fileName.endsWith('.md'), 'file name');
        assert(exported.text.includes('mmn:section'), 'export text');
        await expectError(client.templates.reset(id), 409, 'Conflict');
        const mine = await client.templates.list({ scope: 'mine', search: tname });
        assert(mine.objects.some((x) => x.id === id), 'listed in my templates');
    });

    // ---- Prompts ---------------------------------------------------------------------------------------------------

    await run('prompts.list, get, versions', async () => {
        const page = await client.prompts.list({ key: 'briefing.compose', tenantOnly: true });
        assertPage(page, 'prompts.list');
        assert(page.objects.length >= 1, 'tenant prompt present');
        S.promptId = page.objects[0].id;
        const p = await client.prompts.get(S.promptId);
        assertEqual(p.template.key, 'briefing.compose', 'key');
        assert(p.content.length > 0 && Array.isArray(p.variables), 'content and variables');
        assert(Array.isArray((await client.prompts.versions(S.promptId)).objects), 'versions');
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
        assert(page.objects.some((x) => x.id === S.editionId), 'edition listed');
    });

    await run('topics.previewHistory (nothing saved, bounds enforced)', async () => {
        if (!S.topic) skip('no topic');
        const before = (await client.topics.get(S.topic.id)).topic;
        const saved = await client.topics.previewHistory(S.topic.id);
        assert(Array.isArray(saved.baselines), 'baselines array');
        assert(typeof saved.budgetBytes === 'number' && typeof saved.contextBytes === 'number', 'byte counts');
        const recent = await client.topics.previewHistory(S.topic.id, { historyMode: 'RecentIssues', historyCount: 3 });
        assertEqual(recent.mode, 'RecentIssues', 'unsaved mode previewed');
        await expectError(client.topics.previewHistory(S.topic.id, { historyMode: 'RecentIssues', historyCount: 99 }), 400, 'ValidationFailed');
        await expectError(client.topics.previewHistory(bogus('top')), 404, 'NotFound');
        assertEqual((await client.topics.get(S.topic.id)).topic.historyMode, before.historyMode, 'preview saves nothing');
    });

    await run('editions history settings and previewHistory', async () => {
        if (!S.editionId) skip('no edition');
        const u = await client.editions.update(S.editionId, { historyMode: 'PreviousIssue', historyTokenBudget: 4000 });
        assertEqual(u.edition.historyMode, 'PreviousIssue', 'edition history mode');
        assertEqual(u.edition.historyTokenBudget, 4000, 'edition history budget');
        const p = await client.editions.previewHistory(S.editionId);
        assertEqual(p.mode, 'PreviousIssue', 'saved mode previewed');
        assertEqual(p.baselines.length, 0, 'no earlier issues yet');
        await expectError(client.editions.previewHistory(S.editionId, { historyMode: 'SpecificIssue', historyBriefingId: bogus('brf') }), 400, 'ValidationFailed');
        await expectError(client.editions.previewHistory(bogus('edn')), 404, 'NotFound');
        assertEqual((await client.editions.update(S.editionId, { historyMode: 'Off' })).edition.historyMode, 'Off', 'back to Off');
    });

    await run('editions.publications and setPublications (empty list)', async () => {
        if (!S.editionId) skip('no edition');
        const list = await client.editions.publications(S.editionId);
        assertPage(list, 'editions.publications');
        assert(list.objects.length === 0 && list.totalRecords === 0, 'no system briefings yet');
        const e = await client.editions.setPublications(S.editionId, []);
        assert(Array.isArray(e.publications) && e.publications.length === 0, 'edition response lists no publications');
        await expectError(client.editions.setPublications(S.editionId, [bogus('cat')]), 400, 'ValidationFailed');
        await expectError(client.editions.publications(bogus('edn')), 404, 'NotFound');
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
        assertEqual(calls.totalRecords, 0, 'composing an empty edition made no model calls');
    });

    await run('briefings.comparison (standalone edition briefing is not compared)', async () => {
        await expectError(client.briefings.comparison(bogus('brf')), 404, 'NotFound');
        if (!S.briefingId) skip('no briefing');
        const c = await client.briefings.comparison(S.briefingId);
        assertEqual(c.briefingId, S.briefingId, 'briefing id');
        assertEqual(c.compared, false, 'not compared');
        assert(typeof c.note === 'string' && c.note.length > 0, 'note explains why');
        assertEqual(c.baselines.length, 0, 'no baselines');
        assertEqual(c.deltas.length, 0, 'no deltas');
        for (const k of ['NewDevelopment', 'ChangedFact', 'Resolved', 'Correction', 'Unchanged', 'Unavailable']) assert(typeof c.counts[k] === 'number', 'count for ' + k);
    });

    await run('briefings.list, get, download Markdown and HTML', async () => {
        if (!S.briefingId) skip('no briefing');
        const page = await client.briefings.list({ editionId: S.editionId });
        assertPage(page, 'briefings.list');
        assert(page.objects.some((b) => b.id === S.briefingId), 'briefing listed');
        assert(page.objects.every((b) => b.markdown === null || b.markdown === undefined), 'list leaves Markdown out');
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

    await run('briefings.share, listShares, revokeShare (1 to 14 days)', async () => {
        if (!S.briefingId) skip('no briefing');
        const cfg = await client.clientConfig();
        if (cfg.sharingEnabled === false) skip('sharing is turned off on this server');
        const max = cfg.sharingMaxExpiryDays || 14;
        await expectError(client.briefings.share(S.briefingId, { expiresInDays: max + 1 }), 400, 'ValidationFailed');
        await expectError(client.briefings.share(S.briefingId, { expiresInDays: 0 }), 400, 'ValidationFailed');
        const created = await client.briefings.share(S.briefingId, { expiresInDays: max });
        const id = created.share.id;
        onCleanup('briefing share', async () => {
            await client.briefings.revokeShare(S.briefingId, id).catch(() => null);
            await client.briefings.revokeShare(S.briefingId, id, { purge: true });
        });
        assert(created.secret && created.url.includes('/v1.0/public/shares/') && created.readUrl === created.url && created.listenUrl === created.url, 'one link returned once');
        assertEqual(created.share.tokenHash, 'redacted', 'hash redacted');
        assert(new Date(created.share.expiresUtc).getTime() > Date.now() + (max - 1) * 86400000, 'expiry stored');
        const byDefault = await client.briefings.share(S.briefingId);
        onCleanup('briefing share (default)', async () => {
            await client.briefings.revokeShare(S.briefingId, byDefault.share.id).catch(() => null);
            await client.briefings.revokeShare(S.briefingId, byDefault.share.id, { purge: true });
        });
        assert(new Date(byDefault.share.expiresUtc).getTime() <= Date.now() + max * 86400000 + 60000, 'a share always has an expiry');
        const page = await client.briefings.listShares(S.briefingId);
        assertPage(page, 'briefings.listShares');
        assert(page.objects.some((s) => s.id === id && s.tokenHash === 'redacted' && !s.revokedUtc), 'share listed, redacted');
        await client.briefings.revokeShare(S.briefingId, id);
        const revoked = (await client.briefings.listShares(S.briefingId)).objects.find((s) => s.id === id);
        assert(revoked && revoked.revokedUtc, 'revoked');
        await client.briefings.revokeShare(S.briefingId, id, { purge: true });
        assert(!(await client.briefings.listShares(S.briefingId)).objects.some((s) => s.id === id), 'purged');
        await expectError(client.briefings.revokeShare(S.briefingId, id), 404, 'NotFound');
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
        assert(page.objects.length >= 1, 'artifact listed');
        const a = page.objects[0];
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
        const listed = (await client.feedTokens.list()).objects;
        assert(listed.some((t) => t.id === id && t.tokenHash === 'redacted'), 'token listed, redacted');
        const xml = await client.publicApi.podcastFeed(created.secret);
        assert(xml.includes('<rss'), 'RSS feed');
        await client.feedTokens.revoke(id);
        const revoked = ((await client.feedTokens.list()).objects).find((t) => t.id === id);
        assert(revoked && revoked.revokedUtc, 'revoked');
        await expectError(client.publicApi.podcastFeed(created.secret), 404, 'NotFound');
        await client.feedTokens.purge(id);
        assert(!((await client.feedTokens.list()).objects).some((t) => t.id === id), 'purged');
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
        assert(page.objects.some((x) => x.id === m.id), 'listed in the outbox');
        assert(page.objects.every((x) => x.renderedHtml === null || x.renderedHtml === undefined), 'list leaves bodies out');
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
        const plain = await client.usage.timeseries({ bucketMinutes: 1440 });
        // The server leaves null properties out of its JSON, so an absent value and null both mean "not grouped".
        assert(plain.groupBy == null && Array.isArray(plain.groups) && plain.groups.length === 0, 'a plain series is not grouped');
        assert(plain.buckets.length > 0 && plain.buckets.every((b) => typeof b.cacheWriteTokens === 'number' && typeof b.reasoningTokens === 'number' && typeof b.toolCalls === 'number' && Array.isArray(b.rows)), 'bucket measures');
        const grouped = await client.usage.timeseries({ bucketMinutes: 1440, groupBy: 'Model', top: 5 });
        assertEqual(grouped.groupBy, 'Model', 'grouped series');
        assert(grouped.groups.every((g) => g.key === '(other)' || (g.key.includes('|') && typeof g.model === 'string')), 'model groups are keyed by endpoint and model');
        assertEqual(grouped.buckets.reduce((n, b) => n + b.rows.reduce((m, r) => m + r.calls, 0), 0), grouped.buckets.reduce((n, b) => n + b.calls, 0), 'rows add up to the buckets');
        await expectError(client.usage.timeseries({ groupBy: 'Day' }), 400, 'BadRequest');
        await expectError(client.usage.report({ groupBy: 'Tenant' }), 400, 'BadRequest');
    });

    await run('llmCalls.list and get', async () => {
        const page = await client.llmCalls.list({ pageSize: 2 });
        assertPage(page, 'llmCalls.list');
        await expectError(client.llmCalls.get(bogus('llm')), 404, 'NotFound');
        if (page.objects.length > 0) {
            const c = await client.llmCalls.get(page.objects[0].id);
            assertEqual(c.id, page.objects[0].id, 'call read back');
        }
    });

    // ---- Model endpoints and assignments ---------------------------------------------------------------------------

    await run('modelEndpoints CRUD, validate, models, test (unreachable fake)', async () => {
        const e = await client.modelEndpoints.create({ name: name('model'), provider: 'OpenAiCompatible', endpoint: 'http://127.0.0.1:9/v1', model: 'sdk-fake-model', supportsChat: true, timeoutMs: 2000, active: false });
        S.endpointId = e.id;
        onCleanup('model endpoint', () => client.modelEndpoints.remove(e.id));
        assertEqual((await client.modelEndpoints.get(e.id)).id, e.id, 'read back');
        const u = await client.modelEndpoints.update(e.id, { maxTokens: 512, temperature: 0.3, contextLength: 4096 });
        assert(u.temperature === 0.3 && u.contextLength === 4096, 'optional values set');
        const cleared = await client.modelEndpoints.update(e.id, { clearFields: ['temperature', 'contextLength'] });
        assert(cleared.temperature == null && cleared.contextLength == null && cleared.maxTokens === 512, 'clearFields sets optional values to null');
        await expectError(client.modelEndpoints.update(e.id, { clearFields: ['name'] }), 400, 'ValidationFailed');
        await expectError(client.modelEndpoints.update(e.id, { contextLength: 1, clearFields: ['contextLength'] }), 400, 'ValidationFailed');
        assertEqual(u.maxTokens, 512, 'updated');
        const page = await client.modelEndpoints.list({ provider: 'OpenAiCompatible', pageSize: 100 });
        assert(page.objects.some((x) => x.id === e.id), 'listed');
        const v = await client.modelEndpoints.validate(e.id);
        assertEqual(v.reachable, false, 'fake endpoint is unreachable');
        assertEqual(v.succeeded, false, 'validation of a fake endpoint does not succeed');
        assertEqual((await client.modelEndpoints.get(e.id)).lastValidationSucceeded, false, 'the failed validation is recorded');
        const t = await client.modelEndpoints.test(e.id, { prompt: 'ping' });
        assertEqual(t.success, false, 'test fails on a fake endpoint');
        try {
            const models = (await client.modelEndpoints.models(e.id)).objects;
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
        assert(page.objects.some((x) => x.id === a.id), 'listed');
        const again = await client.modelAssignments.upsert({ task: 'Synthesis', topicId: S.topic.id, modelEndpointId: S.endpointId, modelOverride: 'sdk-other' });
        assertEqual(again.id, a.id, 'upsert updates in place');
        await client.modelAssignments.remove(a.id);
        const tenantDefault = (await client.modelAssignments.list({ pageSize: 100 })).objects.find((x) => !x.topicId);
        if (tenantDefault) await expectError(client.modelAssignments.remove(tenantDefault.id), 409, 'Conflict');
    });

    // ---- Workspace setup -------------------------------------------------------------------------------------------

    await run('setup paths and SetupRequiredError (offline, fake fetch)', async () => {
        const seen = [];
        const status = { ready: false, canConfigure: true, checks: [{ key: 'model:mdl_x', kind: 'Model', required: true, state: 'Missing', tasks: [], needsApiKey: true, hasApiKey: false, usesPlatformKey: false }], checkedUtc: '2026-01-01T00:00:00Z' };
        const fake = new MyMorningNewsClient({
            baseUrl: 'http://127.0.0.1:1', token: 'test-token',
            fetch: async (url, init) => {
                seen.push({ url, init });
                if (url.endsWith('/setup/connect')) return new Response(JSON.stringify({ ...status, connection: { modelEndpointId: 'mdl_y', succeeded: false, keptPrevious: true, message: '401 Unauthorized' } }), { status: 200, headers: { 'content-type': 'application/json' } });
                if (url.includes('/refinement-sessions')) return new Response('{"error":"SetupRequired","message":"Add a key on the Setup page.","requestId":"rqi_s","details":{"check":"model:mdl_x"}}', { status: 409, headers: { 'content-type': 'application/json' } });
                return new Response(JSON.stringify(status), { status: 200, headers: { 'content-type': 'application/json' } });
            }
        });
        const st = await fake.setup.status();
        assertEqual(st.checks[0].key, 'model:mdl_x', 'status parsed');
        await fake.setup.verify();
        await fake.setup.setModelKey('mdl_x', 'sk-test');
        assertEqual(seen[0].init.method, 'GET', 'status method');
        assert(seen[0].url.endsWith('/v1.0/api/setup'), 'status path');
        assertEqual(seen[1].init.method, 'POST', 'verify method');
        assert(seen[1].url.endsWith('/v1.0/api/setup/verify'), 'verify path');
        assert(seen[2].url.endsWith('/v1.0/api/setup/model-key'), 'model-key path');
        assertEqual(JSON.parse(seen[2].init.body).modelEndpointId, 'mdl_x', 'model-key body');
        let threw = false;
        try {
            fake.setup.setModelKey('mdl_x', '');
        } catch (e) {
            threw = e instanceof TypeError;
        }
        assert(threw, 'setModelKey requires an API key');
        await fake.setup.options();
        const tried = await fake.setup.connect({ provider: 'ClaudeCode', authMethod: 'AccessToken', secret: 'sk-ant-oat01-test' });
        assert(tried.connection && tried.connection.succeeded === false && tried.connection.keptPrevious === true, 'connect result carries the connection outcome');
        assertEqual(seen[3].init.method, 'GET', 'options method');
        assert(seen[3].url.endsWith('/v1.0/api/setup/options'), 'options path');
        assertEqual(seen[4].init.method, 'POST', 'connect method');
        assert(seen[4].url.endsWith('/v1.0/api/setup/connect'), 'connect path');
        const connectBody = JSON.parse(seen[4].init.body);
        assertEqual(connectBody.provider, 'ClaudeCode', 'connect body provider');
        assertEqual(connectBody.authMethod, 'AccessToken', 'connect body method');
        threw = false;
        try {
            fake.setup.connect({ authMethod: 'ApiKey', secret: 'sk-test' });
        } catch (e) {
            threw = e instanceof TypeError;
        }
        assert(threw, 'connect requires a provider');
        const err = await expectError(fake.refinement.start({ seedText: 'x' }), 409, 'SetupRequired');
        assert(err instanceof SetupRequiredError, 'SetupRequired is a SetupRequiredError (got ' + err.name + ')');
        assertEqual(err.check, 'model:mdl_x', 'setup check key');
    });

    await run('setup.status, setModelKey (unreachable fake), 403 for a plain user', async () => {
        if (!S.isAdmin) skip('needs an administrator');
        const st = await client.setup.status();
        assertEqual(st.canConfigure, true, 'the administrator can configure');
        assert(Array.isArray(st.checks) && st.checks.length > 0, 'status lists checks');
        assert(st.checks.some((c) => c.kind === 'Search'), 'status has a search check');
        assertEqual(st.ready, st.checks.filter((c) => c.required).every((c) => c.state === 'Ready'), 'ready matches the required checks');
        await expectError(client.setup.setModelKey('mdl_missing', 'sk-harness'), 404);
        const e = await client.modelEndpoints.create({ name: name('setup'), provider: 'OpenAiCompatible', endpoint: 'http://127.0.0.1:9/v1', model: 'sdk-fake-model', supportsChat: true, timeoutMs: 2000, active: false });
        let credentialId = null;
        onCleanup('setup endpoint and key', async () => {
            try {
                await client.modelEndpoints.remove(e.id);
            } catch (err) {
                if (!(err instanceof MyMorningNewsError && err.status === 404)) throw err;
            }
            if (credentialId) await client.vault.remove(credentialId);
        });
        assertEqual(e.lastValidationSucceeded ?? null, null, 'a new endpoint has never been checked');
        const after = await client.setup.setModelKey(e.id, 'sk-harness-' + STAMP);
        assertEqual(after.canConfigure, true, 'status returned after the key');
        const keyed = await client.modelEndpoints.get(e.id);
        credentialId = keyed.credentialId;
        assert(typeof credentialId === 'string' && credentialId.length > 0, 'credential attached to the endpoint');
        assertEqual(keyed.lastValidationSucceeded, false, 'the failed live check is recorded');
        const check = after.checks.find((c) => c.resourceId === e.id);
        if (check) assert(check.state !== 'Ready', 'an unreachable endpoint is not Ready');

        const tenant = OPTS.tenant;
        const email = 'sdk-js-setup-' + STAMP + '@example.com';
        const password = 'Harness-' + STAMP + '-pw';
        const user = await client.request('POST', '/v1.0/tenants/' + encodeURIComponent(tenant) + '/users', {
            body: { email, password, firstName: 'SDK', lastName: 'Harness', active: true }
        });
        const removeUser = async () => {
            const eds = await client.editions.list({ userId: user.id, pageSize: 100 });
            for (const ed of eds.objects.filter((x) => x.userId === user.id)) await client.editions.remove(ed.id);
            await client.request('DELETE', '/v1.0/tenants/' + encodeURIComponent(tenant) + '/users/' + encodeURIComponent(user.id));
        };
        onCleanup('setup user', removeUser);
        const u = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        await u.login(email, password, { tenantId: tenant });
        assertEqual((await u.setup.status()).canConfigure, false, 'a plain user cannot configure');
        await expectError(u.setup.verify(), 403);
        await expectError(u.setup.setModelKey(e.id, 'sk-harness'), 403);
        await expectError(u.setup.options(), 403);
        await expectError(u.setup.connect({ provider: 'Anthropic', authMethod: 'ApiKey', secret: 'sk-harness' }), 403);
        await u.logout();
        await removeUser();
        await client.modelEndpoints.remove(e.id);
        await client.vault.remove(credentialId);
        credentialId = null;
    });

    await run('setup.options lists Claude Code with a token first; connect refuses bad requests without changes', async () => {
        if (!S.isAdmin) skip('needs an administrator');
        const opts = await client.setup.options();
        assert(Array.isArray(opts.providers) && opts.providers.length > 0, 'options list providers');
        assert(typeof opts.serverContainer === 'string' && opts.serverContainer.length > 0, 'options name the server container');
        const cc = opts.providers.find((p) => p.provider === 'ClaudeCode');
        assert(cc, 'Claude Code is offered');
        assertEqual(cc.group, 'AgentCli', 'Claude Code is an agent CLI');
        assert(Array.isArray(cc.methods) && cc.methods.length > 0, 'Claude Code has sign-in methods');
        assertEqual(cc.methods[0].method, 'AccessToken', 'Claude Code lists AccessToken first');
        assertEqual(cc.methods[0].recommended, true, 'the first method is recommended');
        if (!cc.available) assert(typeof cc.unavailableReason === 'string' && cc.unavailableReason.length > 0, 'an unavailable CLI says why');
        assert(typeof cc.turnedOff === 'boolean', 'turnedOff is reported');
        if (cc.turnedOff) assertEqual(cc.available, false, 'a turned-off CLI is unavailable');
        const anthropic = opts.providers.find((p) => p.provider === 'Anthropic');
        assert(anthropic && anthropic.group === 'Api', 'Anthropic is a hosted API');
        assert(anthropic.methods.some((m) => m.method === 'ApiKey'), 'Anthropic signs in with an API key');
        assertEqual(anthropic.recommended, true, 'Anthropic is the recommended provider');
        assertEqual((await client.setup.status()).connection ?? null, null, 'GET setup carries no connection result');
        // Both are refused before anything is stored: an API key pasted as a setup-token token, and an unknown endpoint.
        const badToken = await expectError(client.setup.connect({ provider: 'ClaudeCode', authMethod: 'AccessToken', secret: 'sk-ant-api03-harness' }), 400, 'ValidationFailed');
        assertEqual(badToken.details && badToken.details.field, cc.available ? 'secret' : 'provider', 'details.field names the bad field');
        await expectError(client.setup.connect({ provider: 'Anthropic', authMethod: 'ApiKey', secret: 'sk-harness', modelEndpointId: 'mdl_missing' }), 404);
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
        assert((await client.searchProviders.list({ kind: 'Rss', pageSize: 100 })).objects.some((x) => x.id === p.id), 'listed');
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
        const existing = (await client.voices.list({ pageSize: 100 })).objects.find((v) => v.provider === 'OpenAiCompatibleSpeech' && v.endpoint);
        const v = await client.voices.create({ name: name('voice'), provider: 'OpenAiCompatibleSpeech', endpoint: existing?.endpoint ?? undefined, model: existing?.model ?? 'kokoro', voice: existing?.voice ?? 'af_heart', speed: 1, isDefault: false });
        onCleanup('voice', () => client.voices.remove(v.id));
        assertEqual(v.isDefault, false, 'not default');
        assertEqual((await client.voices.get(v.id)).id, v.id, 'read back');
        const u = await client.voices.update(v.id, { speed: 1.1 });
        assert(Math.abs(u.speed - 1.1) < 0.001, 'speed updated');
        assert((await client.voices.list({ pageSize: 100 })).objects.some((x) => x.id === v.id), 'listed');
        try {
            const audio = await client.voices.test(v.id, { text: 'SDK harness test.' });
            assert(audio.data.byteLength > 0, 'audio bytes');
            assert(String(audio.contentType).startsWith('audio/'), 'audio content type');
        } catch (err) {
            if (err instanceof MyMorningNewsError && err.status === 502) verbose('voice test: TTS endpoint failed (' + err.message + ')');
            else throw err;
        }
        const available = await client.voices.available();
        assert(available.voices.some((x) => x.voiceProfileId === v.id && x.label), 'voice profile among the available voices');
        await expectError(client.voices.preview({}), 400);
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
        const noDescription = await client.vault.update(c.id, { clearFields: ['description'] });
        assert(noDescription.description == null, 'clearFields removes the description');
        await expectError(client.vault.update(c.id, { clearFields: ['secret'] }), 400, 'ValidationFailed');
        assertEqual(u.description, 'SDK harness, updated', 'updated');
        assert((await client.vault.list({ kind: 'ApiKey', pageSize: 100 })).objects.some((x) => x.id === c.id), 'listed');
        assert(Array.isArray((await client.vault.usage(c.id)).objects), 'usage array');
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
        assert(((await client.devices.list()).objects).some((x) => x.id === d.id), 'listed');
        await client.devices.remove(d.id);
        assert(!((await client.devices.list()).objects).some((x) => x.id === d.id), 'removed');
        await expectError(client.devices.register({ expoPushToken: token, platform: 'windows' }), 400, 'BadRequest');
        await expectError(client.devices.remove(d.id), 404, 'NotFound');
    });

    // ---- Notifications ---------------------------------------------------------------------------------------------

    await run('notification preferences matrix, round trip, and the older email preferences', async () => {
        const cell = (p, kind, channel) => p.preferences.find((x) => x.kind === kind && x.channel === channel);
        const before = await client.notifications.getPreferences();
        assertEqual(before.preferences.length, 24, 'eight kinds on three channels');
        assertEqual(before.channels.map((c) => c.channel).join(','), 'Email,Push,InApp', 'channel order');
        assert(before.channels[2].available === true, 'in-app is always available');
        if (((await client.devices.list()).objects).length === 0) {
            assert(before.channels[1].available === false && ['NoDevice', 'PushNotConfigured'].includes(before.channels[1].reason), 'push needs a device');
        }
        assert(typeof before.address === 'string' && Array.isArray(before.unsubscribedTopicIds), 'address and topic list');
        const inApp = cell(before, 'AudioReady', 'InApp').enabled;
        const runEmail = cell(before, 'RunResults', 'Email').enabled;
        try {
            const saved = await client.notifications.updatePreferences({ preferences: [
                { kind: 'AudioReady', channel: 'InApp', enabled: !inApp },
                { kind: 'RunResults', channel: 'Email', enabled: !runEmail },
            ] });
            assertEqual(cell(saved, 'AudioReady', 'InApp').enabled, !inApp, 'in-app cell saved');
            assertEqual(cell(saved, 'BriefingReady', 'Email').enabled, cell(before, 'BriefingReady', 'Email').enabled, 'unsent cells unchanged');
            assertEqual(cell(await client.notifications.getPreferences(), 'RunResults', 'Email').enabled, !runEmail, 'persisted');
            assertEqual((await client.email.getPreferences()).runEmails, !runEmail, 'the older email preferences show the same cell');
            await client.email.updatePreferences({ runEmails: runEmail });
            assertEqual(cell(await client.notifications.getPreferences(), 'RunResults', 'Email').enabled, runEmail, 'and the matrix shows what the older route saved');
        } finally {
            await client.notifications.updatePreferences({ preferences: [
                { kind: 'AudioReady', channel: 'InApp', enabled: inApp },
                { kind: 'RunResults', channel: 'Email', enabled: runEmail },
            ] });
        }
        await expectError(client.notifications.updatePreferences({ preferences: [{ kind: 'Bogus', channel: 'Email', enabled: true }] }), 400);
    });

    await run('notifications list, unread count, read, read all, remove', async () => {
        const page = await client.notifications.list({ pageSize: 5 });
        assert(Array.isArray(page.objects) && typeof page.totalRecords === 'number', 'page shape');
        const unread = await client.notifications.list({ unread: true, kind: 'AudioReady', pageSize: 100 });
        assert(unread.objects.every((n) => n.kind === 'AudioReady' && !n.readUtc), 'filters apply');
        const count = await client.notifications.unreadCount();
        assert(typeof count.count === 'number' && count.count >= unread.objects.length, 'unread count');
        for (const n of page.objects.filter((x) => !x.readUtc).slice(0, 1)) {
            assert((await client.notifications.markRead(n.id)).readUtc, 'marked read');
        }
        const marked = await client.notifications.markAllRead();
        assert(typeof marked.count === 'number', 'read-all count');
        assertEqual((await client.notifications.unreadCount()).count, 0, 'none unread after read-all');
        await expectError(client.notifications.markRead('ntf_missing'), 404, 'NotFound');
        await expectError(client.notifications.remove('ntf_missing'), 404, 'NotFound');
    });

    // ---- Account ---------------------------------------------------------------------------------------------------

    const allSlotTopics = async () => {
        const all = [];
        for await (const x of paginate((o) => client.account.topicSlotTopics(o), { pageSize: 1000 })) all.push(x);
        return all;
    };

    await run('account.topicSlots, topicSlotTopics, setTopicSlots, clearTopicSlots', async () => {
        const original = await client.account.topicSlots();
        assert(typeof original.counted === 'number' && original.topics === undefined, 'summary read without a topic list');
        const originalTopics = await allSlotTopics();
        const keep = originalTopics.filter((t) => t.chosen).sort((a, b) => a.position - b.position).map((t) => t.topicId);
        onCleanup('topic slots', () => client.account.setTopicSlots(keep));
        assertEqual(original.customized, keep.length > 0, 'customized matches the chosen topics');
        const t = await client.topics.create({ name: name('slot-topic'), description: 'SDK slot topic', brief: 'Follow open source database releases. Harness data; safe to delete.' });
        onCleanup('slot topic', () => client.topics.remove(t.id));
        const chosen = await client.account.setTopicSlots([t.id]);
        assertEqual(chosen.customized, true, 'customized');
        const firstPage = await client.account.topicSlotTopics({ pageSize: 1 });
        const first = firstPage.objects[0];
        assert(first && first.topicId === t.id && first.position === 1 && first.chosen, 'chosen topic is kept first');
        assertEqual(firstPage.totalRecords, chosen.counted, 'the topic list covers every counted topic');
        const searched = await client.account.topicSlotTopics({ search: t.name, pageSize: 10 });
        assert(searched.objects.some((x) => x.topicId === t.id && x.position === 1), 'search matches the name and keeps the whole-order position');
        await expectError(client.account.setTopicSlots([t.id, t.id]), 400, 'ValidationFailed');
        await expectError(client.account.setTopicSlots([bogus('top')]), 400, 'ValidationFailed');
        const cleared = await client.account.clearTopicSlots();
        assert(!cleared.customized && (await allSlotTopics()).every((x) => !x.chosen), 'cleared');
        await client.account.setTopicSlots(keep);
    });

    await run('catalog.list subscribed filter and title sort', async () => {
        const all = await client.catalog.list({ pageSize: 1000 });
        const mine = await client.catalog.list({ subscribed: true, sortBy: 'title', pageSize: 1000 });
        const rest = await client.catalog.list({ subscribed: false, sortBy: 'title', pageSize: 1000 });
        assertEqual(mine.totalRecords + rest.totalRecords, all.totalRecords, 'subscribed and unsubscribed partition the catalog');
        assert(mine.objects.every((e) => e.subscribed) && rest.objects.every((e) => !e.subscribed), 'subscribed filter matches each entry');
    });

    await run('account.plan and redeem a bad code', async () => {
        const p = await client.account.plan();
        assert(p.plan && typeof p.plan.key === 'string', 'plan key');
        assert(p.features && typeof p.features === 'object', 'features');
        const err = await expectError(client.account.redeem('SDK-JS-NOPE-' + STAMP), 409, 'PromoCodeInvalid');
        assert(err.details && err.details.reason, 'reason in details');
    });

    await run('account usage, export, delete, and 402 CapReached (Free plan)', async () => {
        if (!S.isAdmin) skip('needs an administrator to create a user and grant a plan');
        const mine = await client.account.usage();
        assert(typeof mine.planKey === 'string' && mine.planKey.length > 0, 'admin plan key');
        await expectError(client.account.delete(), 409, 'Conflict');
        const plans = await client.request('GET', '/v1.0/api/admin/plans');
        const free = plans.objects.find((p) => p.plan.key === 'free');
        if (!free) skip('the server has no Free plan');
        const tenant = OPTS.tenant;
        const email = 'sdk-js-cap-' + STAMP + '@example.com';
        const password = 'Harness-' + STAMP + '-pw';
        const user = await client.request('POST', '/v1.0/tenants/' + encodeURIComponent(tenant) + '/users', {
            body: { email, password, firstName: 'SDK', lastName: 'Harness', active: true }
        });
        const removeUser = async () => {
            const eds = await client.editions.list({ userId: user.id, pageSize: 100 });
            for (const e of eds.objects.filter((x) => x.userId === user.id)) await client.editions.remove(e.id);
            await client.request('DELETE', '/v1.0/tenants/' + encodeURIComponent(tenant) + '/users/' + encodeURIComponent(user.id));
        };
        onCleanup('cap user', removeUser);
        const grant = await client.request('POST', '/v1.0/api/admin/plan-assignments', {
            body: { tenantId: tenant, userId: user.id, planId: free.plan.id, durationDays: 1, note: 'SDK JS harness' }
        });
        onCleanup('cap plan assignment', () => client.request('DELETE', '/v1.0/api/admin/plan-assignments/' + encodeURIComponent(grant.id), { query: { tenantId: tenant } }));

        const u = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        await u.login(email, password, { tenantId: tenant });
        const meter = await u.account.usage();
        assertEqual(meter.planKey, 'free', 'plan key');
        assert(typeof meter.topicsMax === 'number', 'the Free plan limits topics');
        const zip = await u.account.export();
        assertEqual(zip.contentType, 'application/zip', 'export content type');
        const head = new Uint8Array(zip.data, 0, 2);
        assert(head[0] === 0x50 && head[1] === 0x4b, 'ZIP signature');
        const created = [];
        try {
            for (let i = meter.topics; i < meter.topicsMax; i++) created.push((await u.topics.create({ name: name('cap'), brief: 'Harness data; plan limit check.' })).id);
            const err = await expectError(u.topics.create({ name: name('cap'), brief: 'Over the limit.' }), 402, 'CapReached');
            assert(err instanceof CapReachedError, 'CapReached is a CapReachedError (got ' + err.name + ')');
            assertEqual(err.key, 'topics.max', 'cap key');
            assertEqual(err.limit, String(meter.topicsMax), 'cap limit');
        } finally {
            for (const id of created) await u.topics.remove(id);
        }
        const deletion = await u.account.delete();
        assert(Date.parse(deletion.purgeAfterUtc) > Date.now(), 'purge is in the future');
        await expectError(u.me(), 401);
        await u.login(email, password, { tenantId: tenant });
        assertEqual((await u.me()).id, user.id, 'signing in again keeps the account');
        await u.logout();
        await client.request('DELETE', '/v1.0/api/admin/plan-assignments/' + encodeURIComponent(grant.id), { query: { tenantId: tenant } });
        await removeUser();
    });

    await run('public sign-up, EmailNotVerifiedError, verify, resend, forgot, reset', async () => {
        if (!S.isAdmin) skip('needs an administrator to open sign-up through the settings API');
        const current = await client.settings.get();
        const original = JSON.parse(JSON.stringify(current.settings));
        if (!current.settings.hosting) skip('the server has no hosting settings');
        if ((current.fields ?? []).some((f) => f.path === 'hosting.publicSignupEnabled' && f.overriddenByEnvironment)) skip('Hosting.PublicSignupEnabled is set by the environment');
        const anon = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl });
        const email = 'sdk-js-signup-' + STAMP + '@example.com';
        const display = PREFIX + 'signup-' + STAMP;
        const password = 'Harness signup ' + STAMP;
        const body = { email, password, displayName: display, timeZone: 'UTC', acceptTerms: true, confirmAge: true };
        const wasOpen = Boolean(current.settings.hosting.publicSignupEnabled);
        if (!wasOpen) await expectError(anon.account.signup(body), 404, 'SignupClosed');

        const restore = () => client.request('PUT', '/v1.0/api/settings', { body: original });
        onCleanup('settings (sign-up)', restore);
        const open = JSON.parse(JSON.stringify(current.settings));
        Object.assign(open.hosting, { publicSignupEnabled: true, requireEmailVerification: true, signupsPerIpPerHour: 1000 });
        const changed = await client.request('PUT', '/v1.0/api/settings', { body: open });
        assert(changed.success, 'settings accepted');
        try {
            const first = await anon.account.signup(body);
            assert(first.verificationRequired === true, 'verification required');
            const again = await anon.account.signup(body);
            assertEqual(again.message, first.message, 'same answer for an existing account');
            await expectError(anon.account.signup({ ...body, email: 'x' + email, acceptTerms: false }), 400, 'ValidationFailed');
            const blocked = await expectError(anon.login(email, password), 403, 'EmailNotVerified');
            assert(blocked instanceof EmailNotVerifiedError, 'EmailNotVerified is an EmailNotVerifiedError (got ' + blocked.name + ')');
            assertEqual(anon.token, null, 'no session after a refused sign-in');
            assert((await anon.account.resendVerification(email)).message, 'resend message');
            await anon.account.resendVerification('nobody-' + STAMP + '@example.com');
            await anon.account.forgotPassword('nobody-' + STAMP + '@example.com');
            // A token the server never issued is LinkInvalid on both link routes.
            await expectError(anon.account.verifyEmail('not-a-token'), 400, 'LinkInvalid');
            await expectError(anon.account.resetPassword('not-a-token', 'a long new password'), 400, 'LinkInvalid');

            const tenants = await client.request('GET', '/v1.0/tenants', { query: { pageSize: 1000 } });
            const personal = tenants.objects.find((t) => t.name === display + ' (personal)');
            if (!personal) throw new Error('the personal tenant was not found');
            onCleanup('personal tenant', () => client.request('DELETE', '/v1.0/tenants/' + encodeURIComponent(personal.id)));
            assert(personal.isPersonal === true, 'personal tenant');
            const users = await client.request('GET', '/v1.0/tenants/' + encodeURIComponent(personal.id) + '/users');
            const signedUp = users.objects.find((x) => x.email === email);
            const key = await client.request('POST', '/v1.0/tenants/' + encodeURIComponent(personal.id) + '/credentials', {
                body: { userId: signedUp.id, name: name('outbox'), authMode: 'Hybrid' }
            });
            const outbox = new MyMorningNewsClient({ baseUrl: OPTS.baseUrl, accessKey: key.credential.accessKey, secretKey: key.secretKey, tenantId: personal.id });

            const verifyToken = await linkToken(outbox, 'VerifyEmail');
            const verified = await anon.account.verifyEmail(verifyToken);
            assert(verified.verified === true && verified.email === email, 'verified');
            await expectError(anon.account.verifyEmail(verifyToken), 400, 'LinkUsed');
            const session = await anon.login(email, password);
            assertEqual(session.tenantId, personal.id, 'signed in to the personal tenant');
            const meter = await anon.account.usage();
            assertEqual(meter.topics, 0, 'no topics yet');
            await anon.logout();

            await anon.account.forgotPassword(email);
            const resetToken = await linkToken(outbox, 'PasswordReset');
            await expectError(anon.account.resetPassword(verifyToken, 'a long new password'), 400, 'LinkInvalid');
            await expectError(anon.account.resetPassword(resetToken, 'short'), 400, 'ValidationFailed');
            const next = password + ' renewed';
            const reset = await anon.account.resetPassword(resetToken, next);
            assert(reset.reset === true, 'reset');
            await expectError(anon.account.resetPassword(resetToken, next + ' again'), 400, 'LinkUsed');
            await expectError(anon.login(email, password), 401);
            await anon.login(email, next);
            await anon.logout();
            await client.request('DELETE', '/v1.0/tenants/' + encodeURIComponent(personal.id));
        } finally {
            await restore();
        }
        if (!wasOpen) await expectError(anon.account.signup(body), 404, 'SignupClosed');
    });

    await run('mcpServers CRUD, test, discover, tools, agentic topic, run tool calls', async () => {
        await expectError(client.mcpServers.create({ name: name('mcp') }), 400, 'ValidationFailed');
        await expectError(client.mcpServers.create({ name: name('mcp'), url: 'ftp://127.0.0.1/mcp' }), 400, 'ValidationFailed');
        await expectError(client.runs.toolCalls(bogus('run')), 404);
        const runs = await client.runs.list({ pageSize: 1 });
        if (runs.objects.length > 0) {
            const calls = (await client.runs.toolCalls(runs.objects[0].id)).objects;
            assert(Array.isArray(calls) && calls.every((x) => x.runId === runs.objects[0].id), 'tool calls belong to the run');
        }

        const dead = await client.mcpServers.create({ name: name('mcp-dead'), url: 'http://127.0.0.1:9/mcp', enabled: false });
        onCleanup('mcp server (dead)', () => client.mcpServers.remove(dead.id));
        assert(dead.id.startsWith('mcs_') && dead.enabled === false && dead.headerName === 'Authorization', 'created disabled with defaults');
        const failed = await client.mcpServers.test(dead.id);
        assert(failed.success === false && typeof failed.error === 'string', 'a failed test is success false');
        await expectError(client.mcpServers.discover(dead.id), 502, 'McpServerUnavailable');
        assert((await client.mcpServers.get(dead.id)).server.lastError, 'lastError is saved');
        await expectError(client.mcpServers.setToolEnabled(dead.id, bogus('mct'), true), 404);
        const disabled = await client.mcpServers.list({ enabled: false, pageSize: 100 });
        assertPage(disabled, 'mcpServers.list');
        assert(disabled.objects.some((s) => s.id === dead.id), 'the disabled server is listed');
        await client.mcpServers.remove(dead.id);
        await expectError(client.mcpServers.get(dead.id), 404);

        // Topics refuse tool IDs that are not tools of an MCP server in the tenant. Checked before discovery, so it runs
        // even when the server cannot reach the fake MCP server.
        const unknownTool = bogus('mct');
        const refused = await expectError(client.topics.create({ name: name('agentic-bad'), brief: 'Harness data; refused before it is saved.', agenticSearch: true, mcpToolIds: [unknownTool] }), 400, 'ValidationFailed');
        assert(refused.message.includes(unknownTool), 'the refusal names the unknown tool');

        const fake = await startFakeMcpServer();
        try {
            const server = await client.mcpServers.create({ name: name('mcp'), url: fake.url, transport: 'StreamableHttp' });
            onCleanup('mcp server', () => client.mcpServers.remove(server.id));
            const renamed = await client.mcpServers.update(server.id, { name: server.name + '-renamed', headerPrefix: 'Token ' });
            assert(renamed.name === server.name + '-renamed' && renamed.headerPrefix === 'Token ', 'updated');
            await expectError(client.mcpServers.update(server.id, { name: ' ' }), 400, 'ValidationFailed');
            const ok = await client.mcpServers.test(server.id);
            if (!ok.success) skip('the server cannot reach the fake MCP server at ' + fake.url + ' (server in a container?): ' + ok.error);
            assertEqual(ok.toolCount, fake.toolNames.length, 'tool count');
            assertEqual((await client.mcpServers.get(server.id)).tools.length, 0, 'test saves nothing');
            const found = await client.mcpServers.discover(server.id);
            assertEqual(found.tools.length, fake.toolNames.length, 'discovered tools');
            assert(found.server.lastDiscoveredUtc, 'lastDiscoveredUtc');
            assert(found.tools.every((t) => t.enabled === false && t.id.startsWith('mct_')), 'new tools start disabled');
            const echo = found.tools.find((t) => t.name === 'harness_echo');
            assert((await client.mcpServers.setToolEnabled(server.id, echo.id, true)).enabled === true, 'tool enabled');
            const again = await client.mcpServers.discover(server.id);
            assert(again.tools.find((t) => t.name === 'harness_echo').enabled === true, 'rediscovery keeps the setting');

            const topic = await client.topics.create({ name: name('agentic'), brief: 'Harness data for agentic fields; never run.', agenticSearch: true, mcpToolIds: [echo.id, echo.id] });
            onCleanup('agentic topic', () => client.topics.remove(topic.id));
            assert(topic.agenticSearch === true, 'agenticSearch saved');
            assert(topic.mcpToolIds.length === 1 && topic.mcpToolIds[0] === echo.id, 'mcpToolIds keeps the discovered tool once');
            await expectError(client.topics.update(topic.id, { mcpToolIds: [echo.id, unknownTool] }), 400, 'ValidationFailed');
            const read = await client.topics.get(topic.id);
            assert(read.topic.agenticSearch === true && read.topic.mcpToolIds.includes(echo.id), 'agentic fields read back');
            const cleared = await client.topics.update(topic.id, { agenticSearch: false, mcpToolIds: [] });
            assert(cleared.agenticSearch === false && cleared.mcpToolIds.length === 0, 'agentic fields cleared');
            assert(cleared.audioEnabled === false, 'automatic audio is off by default');
            const spoken = await client.topics.update(topic.id, { audioEnabled: true });
            assert(spoken.audioEnabled === true, 'automatic audio turned on');
            await client.topics.remove(topic.id);
            await client.mcpServers.remove(server.id);
            assert(fake.requests() > 0, 'the server talked to the fake MCP server');
        } finally {
            await fake.close();
        }
        await expectError(new MyMorningNewsClient({ baseUrl: OPTS.baseUrl }).mcpServers.list(), 401);
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
