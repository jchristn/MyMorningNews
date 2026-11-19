import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError, filenameFromDisposition } from './api.js';

function response(status, body, headers = {}) {
  const text = body === undefined ? '' : typeof body === 'string' ? body : JSON.stringify(body);
  return {
    status,
    ok: status >= 200 && status < 300,
    text: async () => text,
    blob: async () => new Blob([text], { type: headers['Content-Type'] || 'application/octet-stream' }),
    headers: { get: (name) => headers[Object.keys(headers).find((k) => k.toLowerCase() === name.toLowerCase())] || null }
  };
}

function client(body = {}, status = 200, headers = {}) {
  const fetch = vi.fn(async () => response(status, body, headers));
  return { api: new ApiClient('http://127.0.0.1:12100', 'tok', { fetch }), fetch };
}

function call(fetch, index = 0) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined, headers: init.headers };
}

describe('briefing client methods', () => {
  it('lists with every filter, reads, pins, regenerates, and deletes', async () => {
    const { api, fetch } = client();
    await api.listBriefings({ kind: 'Edition', editionId: 'edn_1', topicId: 'top_1', runId: 'run_1', status: 'Ready', pinned: true, includeDryRun: true, pageSize: 5 });
    await api.getBriefing('brf 1');
    await api.pinBriefing('brf_1', true);
    await api.pinBriefing('brf_1', 0);
    await api.regenerateBriefing('brf_1');
    await api.deleteBriefing('brf_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/briefings?kind=Edition&editionId=edn_1&topicId=top_1&runId=run_1&status=Ready&pinned=true&includeDryRun=true&pageSize=5', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/briefings/brf%201', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/briefings/brf_1/pin', method: 'PUT', body: { pinned: true } });
    expect(call(fetch, 3).body).toEqual({ pinned: false });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/briefings/brf_1/regenerate', method: 'POST' });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/briefings/brf_1', method: 'DELETE' });
  });

  it('downloads a briefing as a blob with the file name from Content-Disposition', async () => {
    const { api, fetch } = client('# Title\n', 200, { 'Content-Type': 'text/markdown', 'Content-Disposition': 'attachment; filename="morning-news.md"' });
    const file = await api.downloadBriefing('brf_1', 'html');
    expect(call(fetch)).toMatchObject({ url: '/v1.0/api/briefings/brf_1/download?format=html', method: 'GET' });
    expect(call(fetch).headers.Authorization).toBe('Bearer tok');
    expect(file.filename).toBe('morning-news.md');
    expect(await file.blob.text()).toBe('# Title\n');
  });

  it('raises ArtifactExpired from a 410 download with its stable code', async () => {
    const { api } = client({ error: 'ArtifactExpired', message: 'The report file is not available.' }, 410);
    const err = await api.downloadBriefing('brf_1').catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(410);
    expect(err.code).toBe('ArtifactExpired');
  });

  it('streams artifact content with a byte range and reads the download URL', async () => {
    const { api, fetch } = client('abc', 206);
    await api.getArtifactContent('art_1', { range: { start: 0, end: 99 } });
    await api.getArtifactContent('art_1', { range: { start: 100 }, download: true });
    await api.getArtifactDownloadUrl('art_1');
    await api.listArtifacts({ briefingId: 'brf_1', kind: 'BriefingHtml' });
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/artifacts/art_1/content', method: 'GET' });
    expect(call(fetch, 0).headers.Range).toBe('bytes=0-99');
    expect(call(fetch, 1).url).toBe('/v1.0/api/artifacts/art_1/content?download=true');
    expect(call(fetch, 1).headers.Range).toBe('bytes=100-');
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/artifacts/art_1/download-url', method: 'GET' });
    expect(call(fetch, 3).url).toBe('/v1.0/api/artifacts?briefingId=brf_1&kind=BriefingHtml');
  });

  it('parses quoted, bare, and RFC 5987 file names', () => {
    expect(filenameFromDisposition('attachment; filename="a b.md"')).toBe('a b.md');
    expect(filenameFromDisposition('attachment; filename=plain.html')).toBe('plain.html');
    expect(filenameFromDisposition("attachment; filename*=UTF-8''caf%C3%A9.md")).toBe('café.md');
    expect(filenameFromDisposition(null)).toBeNull();
  });
});

describe('edition client methods', () => {
  it('covers CRUD, topics, compose, and preview', async () => {
    const { api, fetch } = client();
    await api.listEditions({ pageSize: 10 });
    await api.getEdition('edn_1');
    await api.createEdition({ name: 'Evening' });
    await api.updateEdition('edn_1', { enabled: false });
    await api.deleteEdition('edn_1');
    await api.setEditionTopics('edn_1', ['top_2', 'top_1']);
    await api.setEditionTopics('edn_1');
    await api.composeEdition('edn_1');
    await api.previewEdition('edn_1');
    expect(call(fetch, 0).url).toBe('/v1.0/api/editions?pageSize=10');
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/editions/edn_1', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/editions', method: 'POST', body: { name: 'Evening' } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/editions/edn_1', method: 'PUT', body: { enabled: false } });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/editions/edn_1', method: 'DELETE' });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/editions/edn_1/topics', method: 'PUT', body: { topicIds: ['top_2', 'top_1'] } });
    expect(call(fetch, 6).body).toEqual({ topicIds: [] });
    expect(call(fetch, 7)).toMatchObject({ url: '/v1.0/api/editions/edn_1/compose', method: 'POST' });
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/editions/edn_1/preview', method: 'POST' });
  });
});

describe('briefing template client methods', () => {
  it('covers list, create, validate, save, versions, rollback, reset, and delete', async () => {
    const { api, fetch } = client();
    await api.listBriefingTemplates({ pageSize: 100 });
    await api.createBriefingTemplate({ content: '---\nname: A\n---\n', name: 'A', shared: true });
    await api.createBriefingTemplate({ content: 'x' });
    await api.validateBriefingTemplate('# Title');
    await api.getBriefingTemplate('btp_1');
    await api.saveBriefingTemplate('btp_1', 'body', 'Tighter');
    await api.saveBriefingTemplate('btp_1', 'body');
    await api.listBriefingTemplateVersions('btp_1');
    await api.rollbackBriefingTemplate('btp_1', '2');
    await api.resetBriefingTemplate('btp_1');
    await api.deleteBriefingTemplate('btp_1');
    expect(call(fetch, 0).url).toBe('/v1.0/api/briefing-templates?pageSize=100');
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/briefing-templates', method: 'POST', body: { content: '---\nname: A\n---\n', name: 'A', shared: true } });
    expect(call(fetch, 2).body).toEqual({ content: 'x', name: null, shared: false });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/briefing-templates/validate', method: 'POST', body: { content: '# Title' } });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1', method: 'GET' });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1', method: 'PUT', body: { content: 'body', changeNote: 'Tighter' } });
    expect(call(fetch, 6).body).toEqual({ content: 'body', changeNote: null });
    expect(call(fetch, 7)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1/versions', method: 'GET' });
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1/rollback', method: 'POST', body: { version: 2 } });
    expect(call(fetch, 9)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1/reset', method: 'POST' });
    expect(call(fetch, 10)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1', method: 'DELETE' });
  });

  it('exports the current version as a Markdown file', async () => {
    const { api, fetch } = client('---\nname: A\n---\n', 200, { 'Content-Type': 'text/markdown; charset=utf-8' });
    const file = await api.exportBriefingTemplate('btp_1');
    expect(call(fetch)).toMatchObject({ url: '/v1.0/api/briefing-templates/btp_1/export', method: 'GET' });
    expect(file.filename).toBeNull();
    expect(await file.blob.text()).toBe('---\nname: A\n---\n');
  });

  it('carries TemplateInvalid details keyed by line', async () => {
    const { api } = client({ error: 'TemplateInvalid', message: 'The template has problems.', details: { 'line 3': 'Unknown option foo.' } }, 400);
    const err = await api.saveBriefingTemplate('btp_1', 'x').catch((e) => e);
    expect(err.code).toBe('TemplateInvalid');
    expect(err.details).toEqual({ 'line 3': 'Unknown option foo.' });
  });
});
